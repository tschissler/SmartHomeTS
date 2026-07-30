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
    }
}
