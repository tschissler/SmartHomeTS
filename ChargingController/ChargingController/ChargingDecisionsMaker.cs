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
        private const int BatteryDischargingMaxPower = 3500;

        public static async Task<ChargingResult> CalculateChargingData(ChargingSituation situation, ChargingSettings settings)
        {
            var calculatedIsideChargingPower = 0;
            var calculatedOutsideChargingPower = 0;

            var availableChargingPower = CalculateAvailableChargingPower(situation, settings);

            if (situation.InsideConnected && settings.InsideChargingEnabled && 
                (!situation.OutsideConnected || !settings.OutsideChargingEnabled))
            {
                calculatedIsideChargingPower = availableChargingPower;
            }
            else if (situation.OutsideConnected && settings.OutsideChargingEnabled &&
                (!situation.InsideConnected || !settings.InsideChargingEnabled))
            {
                calculatedOutsideChargingPower = availableChargingPower;
            }
            else if (situation.InsideConnected && settings.InsideChargingEnabled &&
                situation.OutsideConnected && settings.OutsideChargingEnabled)
            {
                if (settings.ChargingLevel == 4)
                {
                    calculatedIsideChargingPower = MinimumChargingPower;
                    calculatedOutsideChargingPower = MinimumChargingPower;
                }
                else if (availableChargingPower >= 2 * MinimumChargingPower)
                {
                    calculatedIsideChargingPower = availableChargingPower / 2;
                    calculatedOutsideChargingPower = availableChargingPower / 2;
                }
                else if (settings.PreferedChargingStation == ChargingStation.Outside)
                {
                    calculatedOutsideChargingPower = availableChargingPower;
                }
                else
                {
                    calculatedIsideChargingPower = availableChargingPower;
                }
            }

            var insideChargingCurrent = (int)Math.Round((double)calculatedIsideChargingPower * 1000 / 230 / 3, 0);
            var outsideChargingCurrent = (int)Math.Round((double)calculatedOutsideChargingPower * 1000 / 230 / 3, 0);

            return new ChargingResult(calculatedIsideChargingPower, calculatedOutsideChargingPower, insideChargingCurrent, outsideChargingCurrent);
        }

        private static int CalculateAvailableChargingPower(ChargingSituation situation, ChargingSettings settings)
        {
            var availableChargingPower = situation.PowerFromGrid * -1 
                + situation.OutsideCurrentChargingPower 
                + situation.InsideCurrentChargingPower 
                - situation.PowerFromBattery;

            switch (settings.ChargingLevel)
            {
                case 0:
                    return 0;
                case 1:
                    if (situation.BatteryLevel < 90 && availableChargingPower > BatteryChargingMaxPower)
                    {
                        if (availableChargingPower < BatteryChargingMaxPower + MinimumChargingPower)
                            availableChargingPower = MinimumChargingPower;
                        else
                            availableChargingPower -= BatteryChargingMaxPower;
                    }
                    break;
                case 2:
                    break;
                case 3:
                    if (situation.BatteryLevel < 25)
                    {
                        if (availableChargingPower > MinimumChargingPower + BatteryChargingMaxPower)
                        {
                            availableChargingPower -= BatteryChargingMaxPower;
                        }
                        else if (availableChargingPower > MinimumChargingPower)
                        {
                            availableChargingPower = MinimumChargingPower;
                        }
                    }
                    else if (situation.BatteryLevel < 90)
                    {
                        if (availableChargingPower + BatteryDischargingMaxPower >= MinimumChargingPower
                            && availableChargingPower < MinimumChargingPower)
                        {
                            availableChargingPower = MinimumChargingPower;
                        }
                    }
                    else
                    {
                        if (availableChargingPower < MinimumChargingPower &&
                            availableChargingPower + BatteryDischargingMaxPower >= MinimumChargingPower)
                        {
                            availableChargingPower = MinimumChargingPower;
                        }
                    }
                    break;
                case 4:
                    if (availableChargingPower < MinimumChargingPower)
                    {
                        availableChargingPower = MinimumChargingPower;
                    }
                    break;
                case 5:
                    if (availableChargingPower < 8000)
                        availableChargingPower = 8000;
                    break;
                default:
                    break;
            }

            if (availableChargingPower < 0)
            {
                availableChargingPower = 0;
            }

            if (availableChargingPower < MinimumChargingPower)
            {
                availableChargingPower = 0;
            }

            return availableChargingPower;
        }
    }
}
