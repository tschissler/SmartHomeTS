using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChargingController
{
    public class ChargingDecisionsMaker
    {
        private const int MinimumChargingPower = 230 * 6 * 3;

        public static async Task<ChargingResult> CalculateChargingData(ChargingInput input)
        {
            var calculatedIsideChargingPower = 0;
            var calculatedOutsideChargingPower = 0;

            var availableChargingPower = CalculateAvailableChargingPower(input);

            if (availableChargingPower < MinimumChargingPower)
            {
                if (availableChargingPower < MinimumChargingPower * (100 - input.MaximumGridChargingPercent) / 100)
                {
                    availableChargingPower = 0;
                }
                else
                {
                    availableChargingPower = MinimumChargingPower;
                }
            }

            if (input.InsideConnected && !input.OutsideConnected)
            {
                calculatedIsideChargingPower = availableChargingPower;
            }
            else if (input.OutsideConnected && !input.InsideConnected)
            {
                calculatedOutsideChargingPower = availableChargingPower;
            }
            else if (input.InsideConnected && input.OutsideConnected)
            {
                if (availableChargingPower >= 2 * MinimumChargingPower)
                {
                    calculatedIsideChargingPower = availableChargingPower / 2;
                    calculatedOutsideChargingPower = availableChargingPower / 2;
                }
                else
                if (input.PreferedChargingStation == ChargingStation.Outside)
                {
                    calculatedOutsideChargingPower = availableChargingPower;
                }
                else
                {
                    calculatedIsideChargingPower = availableChargingPower;
                }
            }

            return new ChargingResult(calculatedIsideChargingPower, calculatedOutsideChargingPower);
        }

        private static int CalculateAvailableChargingPower(ChargingInput input)
        {
            var availableChargingPower = input.GridPower * -1 + input.OutsideCurrentChargingPower + input.InsideCurrentChargingPower;
            if (availableChargingPower < 0)
            {
                availableChargingPower = 0;
            }
            return availableChargingPower;
        }
    }
}
