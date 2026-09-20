using SharedContracts;

namespace ChargingController
{
    /// <summary>
    /// Turns the target values of the <see cref="ChargingDecisionsMaker"/> into the commands
    /// that are actually sent to the wallboxes. The decision maker answers "what would be
    /// ideal right now", this class answers "what do we command, given what we did before".
    /// </summary>
    /// <remarks>
    /// Without this layer every fluctuation around the switching threshold opens and closes
    /// the wallbox contactor, which wears it out and makes the whole loop oscillate: the
    /// charging power jumps between zero and the minimum of 4140 W as soon as the available
    /// power crosses the threshold. The delays below trade a bit of grid or battery power for
    /// a hard limit on the number of switching cycles.
    /// </remarks>
    public class ChargingStabilizer
    {
        /// <summary>
        /// A three phase session at the minimum current measures around 4000 W, slightly below
        /// the nominal minimum charging power of 4140 W. Recognising a running session must not
        /// depend on that nominal value, everything above this threshold is a real session.
        /// </summary>
        private const int RunningSessionThresholdWatts = 1000;

        private readonly ChargingStabilizerOptions options;
        private readonly StationState insideState = new();
        private readonly StationState outsideState = new();

        public ChargingStabilizer(ChargingStabilizerOptions options)
        {
            this.options = options;
        }

        /// <summary>
        /// Applies the delays to the target result. The situation has to carry the raw (not
        /// smoothed) readings: the grid protection must not be damped, and an already running
        /// charging session is detected from the measured charging power.
        /// </summary>
        public ChargingResult Stabilize(ChargingResult target, ChargingSituation situation,
            ChargingSettings settings, DateTimeOffset now)
        {
            // Level 3 may cover a gap from the house battery, so waiting costs nothing but a
            // bit of battery cycling. In every other case waiting means consuming from the grid.
            var stopDelay = settings.ChargingLevel == 3 && situation.BatterySupportedChargingActive
                ? options.StopDelay
                : options.StopDelayWithoutBatterySupport;
            var gridLimitExceeded = situation.PowerFromGrid > options.GridProtectionLimitWatts;

            var insideCurrent = StabilizeStation(
                insideState,
                target.InsideChargingCurrentmA,
                chargingPossible: settings.ChargingLevel > 0 && settings.InsideChargingEnabled && situation.InsideConnected,
                measuredChargingPower: situation.InsideCurrentChargingPower,
                stopDelay, gridLimitExceeded, now, "KebaGarage");

            var outsideCurrent = StabilizeStation(
                outsideState,
                target.OutsideChargingCurrentmA,
                chargingPossible: settings.ChargingLevel > 0 && settings.OutsideChargingEnabled && situation.OutsideConnected,
                measuredChargingPower: situation.OutsideCurrentChargingPower,
                stopDelay, gridLimitExceeded, now, "KebaOutside");

            situation.InsideSwitchCycles = insideState.SwitchCycles;
            situation.OutsideSwitchCycles = outsideState.SwitchCycles;

            return new ChargingResult(
                PowerFromCurrent(insideCurrent), PowerFromCurrent(outsideCurrent),
                insideCurrent, outsideCurrent);
        }

        private int StabilizeStation(StationState state, int targetCurrentmA, bool chargingPossible,
            int measuredChargingPower, TimeSpan stopDelay, bool gridLimitExceeded,
            DateTimeOffset now, string stationName)
        {
            if (!state.Initialized)
            {
                state.Initialized = true;
                if (measuredChargingPower > RunningSessionThresholdWatts)
                {
                    // A session is already running, e.g. after a restart of the service. Adopt it
                    // instead of interrupting it; the delays below then decide how it continues.
                    // LastSwitchAt stays unset on purpose, so a restart cannot extend a charging
                    // session that should be stopped.
                    state.ChargingActive = true;
                    // Never adopt below what the wallbox can do, the measured power of a session
                    // at the minimum current is slightly below the nominal minimum
                    state.CommandedCurrentmA = Math.Max(
                        CurrentFromPower(measuredChargingPower),
                        ChargingDecisionsMaker.MinimumChargingCurrentmA);
                    Console.WriteLine($"Stabilizer {stationName}: adopting running charging session " +
                        $"at {state.CommandedCurrentmA} mA");
                }
            }

            // No car, station disabled or charging switched off completely: stop right away.
            // This is not an oscillation, so no pause is armed and the next session may start
            // after the normal start delay.
            if (!chargingPossible)
            {
                if (state.ChargingActive)
                    Console.WriteLine($"Stabilizer {stationName}: stopping, charging is not possible or not enabled");
                state.Reset();
                return 0;
            }

            if (state.ChargingActive)
                return StabilizeRunningSession(state, targetCurrentmA, stopDelay, gridLimitExceeded, now, stationName);

            return StabilizeStoppedSession(state, targetCurrentmA, gridLimitExceeded, now, stationName);
        }

        private int StabilizeRunningSession(StationState state, int targetCurrentmA, TimeSpan stopDelay,
            bool gridLimitExceeded, DateTimeOffset now, string stationName)
        {
            if (gridLimitExceeded)
            {
                state.GridLimitExceededSince ??= now;
                if (now - state.GridLimitExceededSince.Value >= options.GridProtectionStopDelay)
                    return Stop(state, now, stationName, "grid consumption stayed above the limit");
                // Throttling to the minimum is usually enough, so try that before switching off
                return SetCurrent(state, ChargingDecisionsMaker.MinimumChargingCurrentmA, now, immediately: true);
            }
            state.GridLimitExceededSince = null;

            if (targetCurrentmA <= 0)
            {
                state.InsufficientSince ??= now;
                var insufficientFor = now - state.InsufficientSince.Value;
                var minimumDurationElapsed = now - state.LastSwitchAt >= options.MinimumChargingDuration;
                if (insufficientFor >= stopDelay && minimumDurationElapsed)
                {
                    return Stop(state, now, stationName,
                        $"surplus insufficient for {insufficientFor.TotalSeconds:F0} s");
                }
                // Fall back to the minimum current while waiting: keeps the cost of the delay
                // as low as possible without opening the contactor.
                return SetCurrent(state, ChargingDecisionsMaker.MinimumChargingCurrentmA, now, immediately: true);
            }

            state.InsufficientSince = null;
            return SetCurrent(state, targetCurrentmA, now, immediately: false);
        }

        private int StabilizeStoppedSession(StationState state, int targetCurrentmA,
            bool gridLimitExceeded, DateTimeOffset now, string stationName)
        {
            state.GridLimitExceededSince = null;

            if (targetCurrentmA <= 0 || gridLimitExceeded)
            {
                state.SufficientSince = null;
                state.CommandedCurrentmA = 0;
                return 0;
            }

            state.SufficientSince ??= now;
            var sufficientFor = now - state.SufficientSince.Value;
            var pauseElapsed = now - state.LastSwitchAt >= options.MinimumPauseDuration;
            if (sufficientFor >= options.StartDelay && pauseElapsed)
            {
                state.ChargingActive = true;
                state.CommandedCurrentmA = targetCurrentmA;
                state.LastSwitchAt = now;
                state.LastCurrentChangeAt = now;
                state.SufficientSince = null;
                state.SwitchCycles++;
                Console.WriteLine($"Stabilizer {stationName}: starting charging at {targetCurrentmA} mA " +
                    $"(switch cycle {state.SwitchCycles})");
                return targetCurrentmA;
            }

            state.CommandedCurrentmA = 0;
            return 0;
        }

        private int Stop(StationState state, DateTimeOffset now, string stationName, string reason)
        {
            Console.WriteLine($"Stabilizer {stationName}: stopping charging, {reason}");
            state.ChargingActive = false;
            state.CommandedCurrentmA = 0;
            state.LastSwitchAt = now;
            state.LastCurrentChangeAt = now;
            state.InsufficientSince = null;
            state.SufficientSince = null;
            state.GridLimitExceededSince = null;
            return 0;
        }

        /// <summary>
        /// Changes the setpoint of a running session. Small changes are swallowed by the
        /// deadband and changes are rate limited, unless the new value is needed immediately
        /// (throttling down for protection or while waiting out a shortfall).
        /// </summary>
        private int SetCurrent(StationState state, int currentmA, DateTimeOffset now, bool immediately)
        {
            if (currentmA == state.CommandedCurrentmA)
                return currentmA;

            if (!immediately)
            {
                if (Math.Abs(currentmA - state.CommandedCurrentmA) < options.CurrentDeadbandmA)
                    return state.CommandedCurrentmA;
                if (now - state.LastCurrentChangeAt < options.MinimumCurrentChangeInterval)
                    return state.CommandedCurrentmA;
            }

            state.CommandedCurrentmA = currentmA;
            state.LastCurrentChangeAt = now;
            return currentmA;
        }

        private static int PowerFromCurrent(int currentmA) => currentmA * 230 * 3 / 1000;

        private static int CurrentFromPower(int powerWatts) => (int)Math.Round((double)powerWatts * 1000 / 230 / 3, 0);

        private class StationState
        {
            public bool Initialized;
            public bool ChargingActive;
            public int CommandedCurrentmA;
            /// <summary>Since when the surplus is sufficient to start charging (null = it is not).</summary>
            public DateTimeOffset? SufficientSince;
            /// <summary>Since when the surplus is insufficient to keep charging (null = it is sufficient).</summary>
            public DateTimeOffset? InsufficientSince;
            public DateTimeOffset? GridLimitExceededSince;
            /// <summary>When charging was last switched on or off by the control loop.</summary>
            public DateTimeOffset LastSwitchAt = DateTimeOffset.MinValue;
            public DateTimeOffset LastCurrentChangeAt = DateTimeOffset.MinValue;
            public int SwitchCycles;

            /// <summary>
            /// Back to the idle state without arming the minimum pause: used when charging
            /// became impossible from the outside (car unplugged, station disabled).
            /// </summary>
            public void Reset()
            {
                ChargingActive = false;
                CommandedCurrentmA = 0;
                SufficientSince = null;
                InsufficientSince = null;
                GridLimitExceededSince = null;
                LastSwitchAt = DateTimeOffset.MinValue;
            }
        }
    }
}
