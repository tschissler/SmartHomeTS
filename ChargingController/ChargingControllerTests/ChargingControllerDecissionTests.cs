using ChargingController;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChargingControllerTests
{
    public class ChargingControllerDecisionTests
    {
        [Theory]
        [ExcelData(fileName: @"TestCases/ChargingDecisions.xlsx", numberOfRowsToSkip:1)]
        public async void RunExcelTestCases(
            int Id,
            int insideConnected, 
            int outsideConnected, 
            int gridPower, 
            int pvPower,
            int powerFromBattery,
            int insideCurrentChargingPower, 
            int outsideCurrentChargingPower, 
            string Priority,
            int MaximumGridChargingPercent,
            int BatteryLevel,
            int BatteryMinLevel,
            int PrefereBatteryLevel,
            int ManualCurrent,
            int powerInsideExpected, 
            int powerOutsideExpected,
            int currentInsideExpected,
            int currentOutsideExpected)
        {
            var input = new ChargingInput()
            {
                InsideConnected =  insideConnected == 1,
                OutsideConnected = outsideConnected == 1,
                InsideCurrentChargingPower = insideCurrentChargingPower,
                OutsideCurrentChargingPower = outsideCurrentChargingPower,
                GridPower = gridPower,
                PowerFromBattery = powerFromBattery,
                PreferedChargingStation = Priority == "Inside" ? ChargingStation.Inside : ChargingStation.Outside,
                MaximumGridChargingPercent = MaximumGridChargingPercent,
                BatteryLevel = BatteryLevel,
                BatteryMinLevel = BatteryMinLevel,
                PreferedChargingBatteryLevel = PrefereBatteryLevel,
                ManualCurrent = ManualCurrent
                };

            var actual = await ChargingDecisionsMaker.CalculateChargingData(input);

            actual.InsideChargingPowerWatts.Should().Be(powerInsideExpected);
            actual.OutsideChargingPowerWatts.Should().Be(powerOutsideExpected);
            actual.InsideChargingCurrentmA.Should().Be(currentInsideExpected);
            actual.OutsideChargingCurrentmA.Should().Be(currentOutsideExpected);
        }
    }
}
