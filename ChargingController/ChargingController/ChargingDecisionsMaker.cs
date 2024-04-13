using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharedContracts;

namespace ChargingController
{
    public class ChargingDecisionsMaker
    {
        private const int MinimumChargingPower = 230 * 6 * 3;
        private const int BatteryChargingMaxPower = 3800;

        public static async Task<ChargingResult> CalculateChargingData(ChargingSituation situation)
        {
            var calculatedIsideChargingPower = 0;
            var calculatedOutsideChargingPower = 0;

            if (situation.ManualCurrent >= 0)
            {
                calculatedIsideChargingPower = situation.ManualCurrent * 230 * 3;
                calculatedOutsideChargingPower = situation.ManualCurrent * 230 * 3;
            }

            var availableChargingPower = CalculateAvailableChargingPower(situation);

            if (situation.ManualCurrent >= 0)
            {
                calculatedIsideChargingPower = situation.ManualCurrent * 230 * 3;
                calculatedOutsideChargingPower = situation.ManualCurrent * 230 * 3;
            }
            else
            {
                if (availableChargingPower < MinimumChargingPower)
                {
                    if (availableChargingPower < MinimumChargingPower * (100 - situation.MaximumGridChargingPercent) / 100)
                    {
                        availableChargingPower = 0;
                    }
                    else
                    {
                        availableChargingPower = MinimumChargingPower;
                    }
                }

                if (situation.InsideConnected && !situation.OutsideConnected)
                {
                    calculatedIsideChargingPower = availableChargingPower;
                }
                else if (situation.OutsideConnected && !situation.InsideConnected)
                {
                    calculatedOutsideChargingPower = availableChargingPower;
                }
                else if (situation.InsideConnected && situation.OutsideConnected)
                {
                    if (availableChargingPower >= 2 * MinimumChargingPower)
                    {
                        calculatedIsideChargingPower = availableChargingPower / 2;
                        calculatedOutsideChargingPower = availableChargingPower / 2;
                    }
                    else
                    if (situation.PreferedChargingStation == ChargingStation.Outside)
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

        private static int CalculateAvailableChargingPower(ChargingSituation situation)
        {
            var availableChargingPower = situation.PowerFromGrid * -1 + situation.OutsideCurrentChargingPower + situation.InsideCurrentChargingPower - situation.PowerFromBattery;

            if (situation.BatteryLevel <= situation.PreferedChargingBatteryLevel)
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
            if (availableChargingPower < MinimumChargingPower && situation.BatteryLevel >= situation.BatteryMinLevel)
            {
                availableChargingPower = MinimumChargingPower;
            }
            return availableChargingPower;
        }
    }
}
