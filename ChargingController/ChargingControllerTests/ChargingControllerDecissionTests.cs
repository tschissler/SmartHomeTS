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
            int insideCurrentChargingPower, 
            int outsideCurrentChargingPower, 
            string Priority,
            int MaximumGridChargingPercent,
            int powerInsideShould, 
            int powerOutsideShould)
        {
            var input = new ChargingInput(
                insideConnected == 1,
                outsideConnected == 1,
                insideCurrentChargingPower,
                outsideCurrentChargingPower,
                gridPower,
                Priority == "Inside" ? ChargingStation.Inside : ChargingStation.Outside,
                MaximumGridChargingPercent
                );

            var actual = await ChargingDecisionsMaker.CalculateChargingData(input);

            actual.InsideChargingPowerWatts.Should().Be(powerInsideShould);
            actual.OutsideChargingPowerWatts.Should().Be(powerOutsideShould);
        }
    }
}
