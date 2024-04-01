using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChargingController
{
    public class ChargingDecisionsMaker
    {
        private const int MinimumChargingPower = 230 * 6 * 3 * 1000;
        private const int BatteryChargingMaxPower = 3800 * 1000;

        public static async Task<ChargingResult> CalculateChargingData(ChargingInput input)
        {
            var calculatedIsideChargingPower = 0;
            var calculatedOutsideChargingPower = 0;

            if (input.ManualCurrent >= 0)
            {
                calculatedIsideChargingPower = input.ManualCurrent * 230 * 3;
                calculatedOutsideChargingPower = input.ManualCurrent * 230 * 3;
            }

            var availableChargingPower = CalculateAvailableChargingPower(input);

            if (input.ManualCurrent >= 0)
            {
                calculatedIsideChargingPower = input.ManualCurrent * 230 * 3;
                calculatedOutsideChargingPower = input.ManualCurrent * 230 * 3;
            }
            else
            {
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
            }

            var insideChargingCurrent = (int)Math.Round((double)calculatedIsideChargingPower / 230 / 3, 0);
            var outsideChargingCurrent = (int)Math.Round((double)calculatedOutsideChargingPower / 230 / 3, 0);

            return new ChargingResult(calculatedIsideChargingPower, calculatedOutsideChargingPower, insideChargingCurrent, outsideChargingCurrent);
        }

        private static int CalculateAvailableChargingPower(ChargingInput input)
        {
            var availableChargingPower = input.GridPower * -1 + input.OutsideCurrentChargingPower + input.InsideCurrentChargingPower - input.PowerFromBattery;

            if (input.BatteryLevel <= input.PreferedChargingBatteryLevel)
            {
                if (availableChargingPower > BatteryChargingMaxPower)
                {
                    return MinimumChargingPower;
                }
                return 0;
            }
            if (availableChargingPower < 0)
            {
                availableChargingPower = 0;
            }
            if (availableChargingPower < MinimumChargingPower && input.BatteryLevel >= input.BatteryMinLevel)
            {
                availableChargingPower = MinimumChargingPower;
            }
            return availableChargingPower;
        }
    }
}
