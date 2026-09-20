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
        public const int MinimumChargingPower = 230 * 6 * 3;
        /// <summary>Charging current that corresponds to <see cref="MinimumChargingPower"/> (3 x 6 A).</summary>
        public const int MinimumChargingCurrentmA = 6000;
        private const int BatteryChargingMaxPower = 3800;
        private const int BatteryDischargingMaxPower = 3500;
        // Hysteresis for battery supported charging (level 3): switch off below the
        // lower level, switch on again only once the battery recovered to the upper level
        private const int BatterySupportOffBelowLevel = 25;
        private const int BatterySupportOnAboveLevel = 30;

        /// <summary>
        /// Quick charging (level 5) when the house cannot contribute anything worth mentioning.
        /// Deliberately independent of the surplus: level 5 means "charge now", and at night
        /// that power comes from the grid.
        /// </summary>
        private const int QuickChargingBasePower = 8000;

        /// <summary>
        /// Quick charging with own generation behind it. This is what the wallbox can deliver
        /// (3 x 16 A); the box limits itself as well, but the controller has to know the figure
        /// because it is the whole point of the rule - without it there is no "8 kW or 11 kW".
        /// A different wallbox means a different number here.
        /// </summary>
        private const int QuickChargingBoostPower = 11000;

        /// <summary>
        /// Own generation (PV plus battery discharge) from which level 5 charges with
        /// <see cref="QuickChargingBoostPower"/> instead of <see cref="QuickChargingBasePower"/>,
        /// and the level at which it falls back. A hysteresis for the same reason as the one
        /// above: the difference between the two levels is 4.348 mA, far beyond the deadband of
        /// the stabilizer, so without it a PV reading drifting around the threshold moves the
        /// load by 3 kW every 30 s.
        ///
        /// The lower level follows from the grid protection limit of the stabilizer (10.000 W):
        /// while at least 2 kW come from the house, charging with 11 kW keeps grid consumption
        /// below that limit for any house load up to about 1 kW, which is the 90th percentile
        /// of what this house draws.
        /// </summary>
        private const int QuickChargingBoostOnAbove = 3000;
        private const int QuickChargingBoostOffBelow = 2000;

        /// <summary>
        /// Whether level 5 is currently charging with <see cref="QuickChargingBoostPower"/>.
        /// </summary>
        /// <remarks>
        /// The hysteresis of level 3 keeps its state in
        /// <see cref="ChargingSituation.BatterySupportedChargingActive"/>, which is where this
        /// one belongs too. It cannot live there yet: ChargingSituation is part of
        /// SharedContracts, and a field added there rebuilds and redeploys every service that
        /// references it. Move it over the next time that contract changes for a real reason.
        ///
        /// Static state in a static class is safe here because one process runs one control
        /// loop. It is public for one reason only: a test has to be able to put it into a
        /// defined state, because the tests of one class would otherwise inherit it from each
        /// other. There is no InternalsVisibleTo in this project, or internal would do.
        ///
        /// A restart of the service loses the state and level 5 starts at
        /// <see cref="QuickChargingBasePower"/> until the own power exceeds
        /// <see cref="QuickChargingBoostOnAbove"/> once. That is the harmless direction - too
        /// little charging power, never too much - and it is not a bug.
        /// </remarks>
        public static bool QuickChargingBoostActive { get; set; }

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

        /// <summary>
        /// The surplus that would be available for the cars if they stopped charging and the
        /// house battery neither charged nor discharged. Independent of the current charging
        /// power, because that power is added back in.
        /// </summary>
        public static int CalculateRawAvailablePower(ChargingSituation situation)
        {
            return situation.PowerFromGrid * -1
                + situation.OutsideCurrentChargingPower
                + situation.InsideCurrentChargingPower
                - situation.PowerFromBattery;
        }

        private static int CalculateAvailableChargingPower(ChargingSituation situation, ChargingSettings settings)
        {
            var availableChargingPower = CalculateRawAvailablePower(situation);

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
                    if (situation.BatteryLevel < BatterySupportOffBelowLevel)
                    {
                        situation.BatterySupportedChargingActive = false;
                    }
                    else if (situation.BatteryLevel >= BatterySupportOnAboveLevel)
                    {
                        situation.BatterySupportedChargingActive = true;
                    }

                    if (!situation.BatterySupportedChargingActive)
                    {
                        // Battery gets priority until it has recovered: charge the car from real surplus only
                        if (availableChargingPower > MinimumChargingPower + BatteryChargingMaxPower)
                        {
                            availableChargingPower -= BatteryChargingMaxPower;
                        }
                        else if (availableChargingPower > MinimumChargingPower)
                        {
                            availableChargingPower = MinimumChargingPower;
                        }
                    }
                    else
                    {
                        if (availableChargingPower < MinimumChargingPower
                            && availableChargingPower + BatteryDischargingMaxPower >= MinimumChargingPower)
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
                    // Level 5 commands a fixed power, not a surplus: what the house cannot
                    // supply comes from the grid. The only question is how much that may be.
                    //
                    // The discharging house battery counts towards that own power although it
                    // generates nothing - AND THAT IS DELIBERATE, do not "fix" it. Yes, the
                    // battery is discharging precisely because the car is charging, so at night
                    // the condition stays true until the battery is empty and level 5 moves the
                    // house battery into the car instead of taking 8 kW from the grid. Thomas
                    // chose this knowing it: level 5 means "charge now", and a full house
                    // battery is the cheaper source for it than the grid.
                    //
                    // When the battery does run empty it stops abruptly, grid consumption jumps
                    // to about 11,4 kW and the grid protection of the stabilizer throttles to
                    // the minimum current for 30 s before charging continues at
                    // QuickChargingBasePower. Known, self-healing, accepted - it costs half a
                    // minute of slow charging, not a switching cycle.
                    var ownPower = situation.PowerFromPV + situation.PowerFromBattery;
                    if (ownPower >= QuickChargingBoostOnAbove)
                    {
                        QuickChargingBoostActive = true;
                    }
                    else if (ownPower < QuickChargingBoostOffBelow)
                    {
                        QuickChargingBoostActive = false;
                    }

                    availableChargingPower = QuickChargingBoostActive
                        ? QuickChargingBoostPower
                        : QuickChargingBasePower;
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
