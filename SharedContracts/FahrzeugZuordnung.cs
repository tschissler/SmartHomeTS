using System.Text.Json.Serialization;

namespace SharedContracts
{
    /// <summary>
    /// How sure the system is that a vehicle is the one charging at a box. Ordered: a higher
    /// value is a stronger statement, and an assignment may only ever be raised, never lowered
    /// within a session. See Docs/Fahrzeug-Wallbox-Zuordnung.md.
    /// </summary>
    /// <remarks>
    /// The wire names are lower case and without umlauts, matching the <c>vertrauen</c> field
    /// of the <c>ladesitzungen</c> table in Docs/Ladeprotokoll.md — the value travels from here
    /// through the DataHub into InfluxDB unchanged, so the Grafana filter
    /// <c>vertrauen IN ('bestaetigt','erkannt')</c> needs no translation layer anywhere.
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter<Vertrauensgrad>))]
    public enum Vertrauensgrad
    {
        /// <summary>No evidence at all. A session is running, nobody knows whose.</summary>
        [JsonStringEnumMemberName("unbekannt")]
        Unbekannt = 0,

        /// <summary>
        /// Taken from the history: the vehicle that last charged at this box. A guess, and the
        /// interface shows it as one.
        /// </summary>
        [JsonStringEnumMemberName("vermutet")]
        Vermutet = 1,

        /// <summary>
        /// Derived from a positive vehicle report together with the measured occupancy of the
        /// boxes. Never from the silence of a vehicle — see the concept document.
        /// </summary>
        [JsonStringEnumMemberName("erkannt")]
        Erkannt = 2,

        /// <summary>Set by a person in the web interface for this session.</summary>
        [JsonStringEnumMemberName("bestaetigt")]
        Bestaetigt = 3,
    }

    /// <summary>
    /// Which vehicle charges at one wallbox, published retained by the RulesEngine to
    /// daten/Laden/&lt;Ort&gt;/&lt;Box&gt;/Zuordnung — and, with the same shape, by the web
    /// interface to konfiguration/Laden/&lt;Ort&gt;/&lt;Box&gt;/Zuordnung as a manual override.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The topic has exactly one author on each of the two levels. The session record has three
    /// contributors (box, controller, RulesEngine) and they are joined over the
    /// <see cref="SitzungsId"/> by the DataHub, not by writing into each other's topics.
    /// </para>
    /// <para>
    /// The retained message outlives its session on purpose: after the plug is pulled it stays
    /// standing with the old <see cref="SitzungsId"/> and is then the only source of the history
    /// guess for the next session at that box. A consumer therefore has to compare the
    /// <see cref="SitzungsId"/> with the one the box currently reports before showing the
    /// vehicle as the one charging now.
    /// </para>
    /// <para>
    /// In an override the <see cref="Vertrauen"/> field carries no information — a manual
    /// statement is <see cref="Vertrauensgrad.Bestaetigt"/> by definition and the rule treats it
    /// as such whatever the payload says.
    /// </para>
    /// </remarks>
    public record FahrzeugZuordnung()
    {
        /// <summary>
        /// When this statement was made (UTC). Mandatory field of the topic convention: the
        /// topic is retained, so the broker replays it on every subscribe and without a
        /// timestamp in the payload its age cannot be told from its arrival.
        /// </summary>
        /// <remarks>
        /// It stands still as long as the statement does not change, so it reads as "assigned
        /// since" rather than "last evaluated". Whether the RulesEngine is alive is a question
        /// for its heartbeat, not for this field.
        /// </remarks>
        public DateTimeOffset Zeitpunkt { get; set; }

        /// <summary>
        /// The session this statement is about, as the box reports it in
        /// <see cref="WallboxStatus.SitzungsId"/>. Null only where a statement was made without
        /// a running session, which the rule never does.
        /// </summary>
        public int? SitzungsId { get; set; }

        /// <summary>
        /// Name of the vehicle as it appears in its topic ("BMW", "Mini", "VW"), or null when
        /// no vehicle could be named.
        /// </summary>
        public string? Fahrzeug { get; set; }

        /// <summary>How the vehicle was arrived at.</summary>
        public Vertrauensgrad Vertrauen { get; set; }

        /// <summary>
        /// Whether another statement says the same thing. <see cref="Zeitpunkt"/> is left out:
        /// it changes on every evaluation, and comparing it would republish the same assignment
        /// several times a minute onto a retained topic.
        /// </summary>
        public bool GleicheAussageWie(FahrzeugZuordnung? andere)
            => andere is not null
               && andere.SitzungsId == SitzungsId
               && andere.Fahrzeug == Fahrzeug
               && andere.Vertrauen == Vertrauen;
    }
}
