namespace ChargingController
{
    /// <summary>
    /// Timing parameters that keep the wallbox contactor from cycling on short fluctuations
    /// of house consumption or PV production. The defaults are the cautious setting: the
    /// controller still reacts within a few minutes, but no longer follows every spike.
    /// </summary>
    public class ChargingStabilizerOptions
    {
        /// <summary>Time constant of the moving average over the power readings.</summary>
        public TimeSpan SmoothingTimeConstant { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>The surplus has to be sufficient this long before charging starts.</summary>
        public TimeSpan StartDelay { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// The surplus has to be insufficient this long before charging stops, while the house
        /// battery is allowed to cover the gap (charging level 3 with battery support).
        /// </summary>
        public TimeSpan StopDelay { get; set; } = TimeSpan.FromSeconds(90);

        /// <summary>
        /// Shorter stop delay for the levels where a gap has to be covered from the grid
        /// (surplus charging, or level 3 after the battery dropped below its minimum level).
        /// </summary>
        public TimeSpan StopDelayWithoutBatterySupport { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>Once started, charging is not stopped again before this time has passed.</summary>
        public TimeSpan MinimumChargingDuration { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>Once stopped, charging is not started again before this time has passed.</summary>
        public TimeSpan MinimumPauseDuration { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Setpoint changes smaller than this are not sent: the wallbox works in 100 mA steps
        /// and the car follows a new setpoint with a delay of several seconds anyway.
        /// </summary>
        public int CurrentDeadbandmA { get; set; } = 500;

        /// <summary>
        /// Minimum time between two setpoint changes, so the control loop stays slower than
        /// the dead time of the car. Switching on and off is not delayed by this.
        /// </summary>
        public TimeSpan MinimumCurrentChangeInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Above this grid consumption the car is throttled to the minimum current immediately,
        /// bypassing all delays. Protects the house connection when a large load starts while
        /// the car is charging.
        /// </summary>
        /// <remarks>
        /// Raised from 8.000 to 10.000 W (about 14,5 A per phase) together with the 11 kW of
        /// level 5. At 8.000 the limit stood in the way of the very level it was meant to
        /// protect: level 5 commands 8.000 W on its own, so together with the house it crossed
        /// the limit whenever there was no sun - the emergency brake would have fired on the
        /// normal case, which only goes unnoticed because level 5 is rarely used at night.
        /// Over 90 days grid consumption exceeded 8.000 W 37 times, at most 10.613 W, so the
        /// new limit is still above what the house does by itself. Approved by Thomas.
        /// </remarks>
        public int GridProtectionLimitWatts { get; set; } = 10000;

        /// <summary>
        /// If the grid consumption stays above the limit for this long even at minimum current,
        /// charging is stopped regardless of the minimum charging duration.
        /// </summary>
        public TimeSpan GridProtectionStopDelay { get; set; } = TimeSpan.FromSeconds(30);
    }
}
