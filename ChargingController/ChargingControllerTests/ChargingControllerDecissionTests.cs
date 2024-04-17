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
        [ExcelData(fileName: @"TestCases/ChargingDecisions.xlsx", numberOfRowsToSkip:1)]
        public async void RunExcelTestCases(
            int Id,
            int level,
            int insideConnected, 
            int outsideConnected, 
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
                PreferedChargingStation = Priority == "Inside" ? ChargingStation.Inside : ChargingStation.Outside
            };

            var actual = await ChargingDecisionsMaker.CalculateChargingData(inputSituation, inputSettings);

            actual.InsideChargingPowerWatts.Should().Be(powerInsideExpected);
            actual.OutsideChargingPowerWatts.Should().Be(powerOutsideExpected);
            //actual.InsideChargingCurrentmA.Should().Be(powerInsideExpected * 1000 / 3 / 230);
            //actual.OutsideChargingCurrentmA.Should().Be(powerOutsideExpected * 1000 / 3 / 230);
        }
    }
}
