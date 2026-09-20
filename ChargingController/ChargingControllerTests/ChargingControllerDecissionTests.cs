using ChargingController;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharedContracts;

namespace ChargingControllerTests
{
    public class ChargingControllerDecisionTests
    {
        [Theory]
        [ExcelData(fileName: @"../../../TestCases/ChargingDecisions.xlsx", numberOfRowsToSkip:1)]
        public async void RunExcelTestCases(
            int Id,
            int level,
            int insideConnected, 
            int outsideConnected, 
            int insideEnabled,
            int outsideEnabled,
            int gridPower, 
            int pvPower,
            int powerFromBattery,
            int insideCurrentChargingPower, 
            int outsideCurrentChargingPower, 
            int BatteryLevel,
            string Priority,
            int powerInsideExpected, 
            int powerOutsideExpected)
        {
            var inputSituation = new ChargingSituation()
            {
                InsideConnected =  insideConnected == 1,
                OutsideConnected = outsideConnected == 1,
                InsideCurrentChargingPower = insideCurrentChargingPower,
                OutsideCurrentChargingPower = outsideCurrentChargingPower,
                PowerFromGrid = gridPower,
                PowerFromBattery = powerFromBattery,
                // The PVPower column of the sheet used to be read and then dropped - which
                // xUnit1026 had been saying about the parameter all along. Harmless while no
                // level looked at PV; since level 5 decides between 8 and 11 kW by it, a
                // dropped column would mean the sheet tests something other than it states.
                PowerFromPV = pvPower,
                BatteryLevel = BatteryLevel,
                };

            var inputSettings = new ChargingSettings()
            {
                ChargingLevel = level,
                PreferedChargingStation = Priority == "Inside" ? ChargingStation.Inside : ChargingStation.Outside,
                InsideChargingEnabled = insideEnabled == 1,
                OutsideChargingEnabled = outsideEnabled == 1,
            };

            var actual = await ChargingDecisionsMaker.CalculateChargingData(inputSituation, inputSettings);

            actual.InsideChargingPowerWatts.Should().Be(powerInsideExpected);
            actual.OutsideChargingPowerWatts.Should().Be(powerOutsideExpected);
            //actual.InsideChargingCurrentmA.Should().Be(powerInsideExpected * 1000 / 3 / 230);
            //actual.OutsideChargingCurrentmA.Should().Be(powerOutsideExpected * 1000 / 3 / 230);
        }

        [Fact]
        public async void BatterySupportedChargingUsesHysteresisAroundMinimumBatteryLevel()
        {
            // Level 3 with 1000W surplus: below the minimum charging power on its own,
            // but enough when supported by battery discharge
            var situation = new ChargingSituation()
            {
                InsideConnected = true,
                PowerFromGrid = -1000,
            };
            var settings = new ChargingSettings()
            {
                ChargingLevel = 3,
                InsideChargingEnabled = true,
            };

            async Task<int> ChargingPowerAtBatteryLevel(int batteryLevel)
            {
                situation.BatteryLevel = batteryLevel;
                var result = await ChargingDecisionsMaker.CalculateChargingData(situation, settings);
                return result.InsideChargingPowerWatts;
            }

            const int minimumChargingPower = 230 * 6 * 3;
            (await ChargingPowerAtBatteryLevel(50)).Should().Be(minimumChargingPower, "battery is well above the minimum level");
            (await ChargingPowerAtBatteryLevel(24)).Should().Be(0, "battery dropped below 25%, support switches off");
            (await ChargingPowerAtBatteryLevel(27)).Should().Be(0, "battery has not yet recovered to 30%, support stays off");
            (await ChargingPowerAtBatteryLevel(29)).Should().Be(0, "battery has not yet recovered to 30%, support stays off");
            (await ChargingPowerAtBatteryLevel(30)).Should().Be(minimumChargingPower, "battery recovered to 30%, support switches on again");
            (await ChargingPowerAtBatteryLevel(27)).Should().Be(minimumChargingPower, "support stays on while the battery is above 25%");
        }

        /// <summary>
        /// Level 5 charges with 11 kW while the house contributes at least 3 kW of its own,
        /// and falls back to 8 kW once that drops below 2 kW. The hysteresis is what keeps a
        /// PV reading drifting around the threshold from moving the load by 3 kW every 30 s.
        /// </summary>
        [Fact]
        public async void QuickChargingUsesHysteresisBetweenBaseAndBoostPower()
        {
            var situation = new ChargingSituation()
            {
                InsideConnected = true,
                // Grid feed-in, but deliberately far too little to reach either figure on its
                // own: level 5 commands a fixed power, it does not follow the surplus.
                PowerFromGrid = -1000,
            };
            var settings = new ChargingSettings()
            {
                ChargingLevel = 5,
                InsideChargingEnabled = true,
            };

            async Task<int> ChargingPowerAtOwnPower(int pv, int battery)
            {
                situation.PowerFromPV = pv;
                situation.PowerFromBattery = battery;
                var result = await ChargingDecisionsMaker.CalculateChargingData(situation, settings);
                return result.InsideChargingPowerWatts;
            }

            ChargingDecisionsMaker.QuickChargingBoostActive = false;

            (await ChargingPowerAtOwnPower(0, 0)).Should().Be(8000, "nothing of its own, so the base power");
            (await ChargingPowerAtOwnPower(2900, 0)).Should().Be(8000, "still below the upper level");
            (await ChargingPowerAtOwnPower(3000, 0)).Should().Be(11000, "the upper level is reached");
            (await ChargingPowerAtOwnPower(2500, 0)).Should().Be(11000, "still above the lower level, so it stays");
            (await ChargingPowerAtOwnPower(2000, 0)).Should().Be(11000, "the lower level itself does not switch back");
            (await ChargingPowerAtOwnPower(1999, 0)).Should().Be(8000, "below the lower level it falls back");
            (await ChargingPowerAtOwnPower(2900, 0)).Should().Be(8000, "the upper level is needed to switch on again");

            // PV and battery discharge add up - 2,5 kW from each is five kilowatts of own power
            (await ChargingPowerAtOwnPower(2500, 2500)).Should().Be(11000, "PV and battery add up");
            // A charging battery consumes, so it is subtracted: 5 kW of PV with 3 kW going into
            // the battery leaves 2 kW, which is not enough to switch on again after a fallback
            ChargingDecisionsMaker.QuickChargingBoostActive = false;
            (await ChargingPowerAtOwnPower(5000, -3000)).Should().Be(8000, "the charging battery is a consumer");
        }

        /// <summary>
        /// At night the house battery alone carries the boost, which is the case the rule was
        /// written for - and when it runs empty, level 5 goes back to its base power.
        /// </summary>
        [Fact]
        public async void QuickChargingFallsBackToBasePowerWhenTheHouseBatteryIsEmpty()
        {
            var situation = new ChargingSituation()
            {
                InsideConnected = true,
                // Night: no PV, the house draws from the grid while the car charges
                PowerFromGrid = 8000,
                PowerFromPV = 0,
                PowerFromBattery = 3300,
                InsideCurrentChargingPower = 11000,
            };
            var settings = new ChargingSettings()
            {
                ChargingLevel = 5,
                InsideChargingEnabled = true,
            };

            ChargingDecisionsMaker.QuickChargingBoostActive = false;

            var withBattery = await ChargingDecisionsMaker.CalculateChargingData(situation, settings);
            withBattery.InsideChargingPowerWatts.Should().Be(11000,
                "the discharging house battery counts as own power, deliberately");

            situation.PowerFromBattery = 0;
            var batteryEmpty = await ChargingDecisionsMaker.CalculateChargingData(situation, settings);
            batteryEmpty.InsideChargingPowerWatts.Should().Be(8000,
                "with the battery empty there is nothing of its own left");
        }
    }
}
