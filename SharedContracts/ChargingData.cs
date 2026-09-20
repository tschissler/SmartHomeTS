namespace SharedContracts
{
    /// <summary>
    /// Setpoint for one wallbox, published to befehle/Laden/M3/&lt;Box&gt;/Ladestrom.
    /// </summary>
    public record LadestromKommando()
    {
        /// <summary>
        /// When the controller decided this setpoint. A command is published retained, so the
        /// broker replays it on every subscribe regardless of its age — the receiver has to
        /// judge the setpoint by this timestamp, not by when it arrived. See
        /// MQTT-Topic-Konvention.md, "Auch Befehle tragen einen Zeitpunkt".
        /// </summary>
        public DateTimeOffset Zeitpunkt { get; set; }

        /// <summary>
        /// The charging current in mA.
        /// </summary>
        public int LadestromMa { get; set; }
    }

    /// <summary>
    /// Everything the wallbox knows about itself, published retained to
    /// daten/Laden/M3/&lt;Box&gt;/Status by the KebaConnector.
    /// </summary>
    /// <remarks>
    /// The session fields exist so that the ChargingController can recognise session
    /// boundaries — it owns daten/Laden/M3/&lt;Box&gt;/Ladesitzung, not the connector.
    /// </remarks>
    public record WallboxStatus()
    {
        /// <summary>
        /// Plug state 7: cable plugged into station and vehicle, and locked. Charging is not
        /// possible in any other plug state.
        /// </summary>
        public const int PlugStatusFahrzeugVerbunden = 7;

        /// <summary>When the connector read these values from the box (UTC).</summary>
        public DateTimeOffset Zeitpunkt { get; set; }

        /// <summary>
        /// Raw plug status of the box: 0 = no cable, 1 = plugged into the station,
        /// 3 = plugged in and locked, 5 = plugged into station and vehicle but not locked,
        /// 7 = plugged into station and vehicle and locked (the only state that charges).
        /// Published raw instead of as a boolean so consumers can tell "cable dangling" from
        /// "vehicle connected".
        /// </summary>
        public int PlugStatus { get; set; }

        /// <summary>
        /// Device state of the box: 0 = startup, 1 = not ready for charging, 2 = ready and
        /// waiting for the vehicle, 3 = charging, 4 = error, 5 = temporarily interrupted.
        /// </summary>
        public int DeviceState { get; set; }

        /// <summary>Whether the box currently allows charging ("Enable sys").</summary>
        public bool Freigegeben { get; set; }

        /// <summary>
        /// Id of the charging session currently running in the box, null when none is running.
        /// Read from report 100.
        /// </summary>
        public int? SitzungsId { get; set; }

        /// <summary>
        /// Start of the running session. Primarily the plug-in edge observed by the connector
        /// itself; the clock of the box is only the fallback, see
        /// <see cref="SitzungsBeginnAusBoxZeit"/>.
        /// </summary>
        public DateTimeOffset? SitzungsBeginn { get; set; }

        /// <summary>
        /// True when <see cref="SitzungsBeginn"/> had to be taken from the box clock because the
        /// session was already running when the connector started. The box reports "timeQ": 0
        /// ("not synced time"), so such a value may be arbitrarily wrong — a consumer that
        /// calculates durations has to know which of the two sources it got.
        /// </summary>
        public bool SitzungsBeginnAusBoxZeit { get; set; }

        /// <summary>Energy charged in the running session in Wh (reset by the box per session).</summary>
        public int EnergieSitzungWh { get; set; }

        /// <summary>Total energy of the box in Wh, persistent across sessions.</summary>
        public int EnergieGesamtWh { get; set; }

        /// <summary>Current charging power in W.</summary>
        public int Ladeleistung { get; set; }

        /// <summary>Measured current on phase 1 in mA.</summary>
        public int StromPhase1Ma { get; set; }

        /// <summary>Measured current on phase 2 in mA.</summary>
        public int StromPhase2Ma { get; set; }

        /// <summary>Measured current on phase 3 in mA.</summary>
        public int StromPhase3Ma { get; set; }

        /// <summary>
        /// Current in mA the box offers to the vehicle via the control pilot ("Max curr").
        /// </summary>
        public int AngebotenerStromMa { get; set; }

        /// <summary>
        /// Setpoint in mA currently stored in the box ("Curr user"). The controller compares it
        /// with the value it commanded to see whether the box actually took it.
        /// </summary>
        public int SollstromMa { get; set; }

        /// <summary>
        /// Whether a vehicle is connected and ready to charge. Derived from
        /// <see cref="PlugStatus"/> so that the rule lives with the contract instead of being
        /// spelled out in every consumer. Not serialised — it carries no information the
        /// payload does not already have.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public bool FahrzeugVerbunden => PlugStatus == PlugStatusFahrzeugVerbunden;
    }
}
