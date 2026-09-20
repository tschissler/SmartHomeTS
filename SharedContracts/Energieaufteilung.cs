namespace SharedContracts
{
    /// <summary>
    /// The three virtual meters of one wallbox plus the momentary split of its charging
    /// power, published retained to daten/Laden/&lt;Ort&gt;/&lt;Box&gt;/Energieaufteilung by the
    /// ChargingController. The rule behind the numbers is in Docs/Ladeprotokoll.md.
    /// </summary>
    /// <remarks>
    /// The meters are cumulative and never reset — exactly like a Shelly or the Envoy, so the
    /// established query pattern MAX(value_cumulated_kwh) - MIN(...) answers "how much PV
    /// charging in February" without knowing anything about sessions.
    /// <para>
    /// The topic is retained for two reasons: it is state, and it is the controller's own
    /// backup. On startup the controller reads its last published stand back from here.
    /// Without that every rollout would restart the meters at zero.
    /// </para>
    /// <para>
    /// The counters only grow while the controller runs. If it is down while the box charges
    /// autonomously, the difference to the box's own EnergieGesamt stays visible as
    /// unattributed energy. That gap is honest information about data quality, not something
    /// to be extrapolated away.
    /// </para>
    /// </remarks>
    public record Energieaufteilung()
    {
        /// <summary>
        /// Above this charging power in W an interval counts as charging time. The box reports
        /// small noise values while idle, so the threshold is deliberately not zero.
        /// </summary>
        public const int LadeleistungsschwelleW = 100;

        /// <summary>
        /// When the control cycle that wrote these meter readings ran (UTC). Mandatory field
        /// of every payload, see MQTT-Topic-Konvention.md — the topic is retained, so without
        /// it a consumer cannot tell a live value from one the broker replayed.
        /// </summary>
        public DateTimeOffset Zeitpunkt { get; set; }

        /// <summary>Cumulative energy in kWh this box drew from the PV share of the mix.</summary>
        public decimal EnergieLadungPvKwh { get; set; }

        /// <summary>Cumulative energy in kWh this box drew from the battery share of the mix.</summary>
        public decimal EnergieLadungBatterieKwh { get; set; }

        /// <summary>Cumulative energy in kWh this box drew from the grid share of the mix.</summary>
        public decimal EnergieLadungNetzKwh { get; set; }

        /// <summary>
        /// The PV part of the charging power in W at the moment of the last cycle. Published so
        /// the history chart can stack the charging power by source without differentiating
        /// the meters.
        /// </summary>
        public decimal LadeleistungPvW { get; set; }

        /// <summary>The battery part of the charging power in W at the moment of the last cycle.</summary>
        public decimal LadeleistungBatterieW { get; set; }

        /// <summary>The grid part of the charging power in W at the moment of the last cycle.</summary>
        public decimal LadeleistungNetzW { get; set; }

        /// <summary>
        /// Cumulative seconds this box actually charged, counted separately from how long a
        /// car was plugged in. A car hangs on the box overnight and charges for two hours on
        /// surplus; the Keba still reports a single twelve hour session, and without this
        /// separation every surplus charge would look like an absurdly low average power.
        /// </summary>
        /// <remarks>
        /// Counted for intervals above <see cref="LadeleistungsschwelleW"/>, not above zero —
        /// the box reports small noise readings while idle which would otherwise accumulate
        /// as charging time. Cumulative like the energy meters, so a session's charging time
        /// is a difference between two readings.
        /// </remarks>
        public long LadezeitSekunden { get; set; }
    }
}
