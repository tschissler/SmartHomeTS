using System.Text.Json.Serialization;

namespace SharedContracts
{
    /// <summary>
    /// Whether a charging session is still running or has ended. The wire names are lower case
    /// and without umlauts, like every other enum that travels through these topics.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<Ladesitzungszustand>))]
    public enum Ladesitzungszustand
    {
        /// <summary>The vehicle is still plugged in and the box still reports this session.</summary>
        [JsonStringEnumMemberName("laufend")]
        Laufend = 0,

        /// <summary>The box no longer reports this session: the plug was pulled.</summary>
        [JsonStringEnumMemberName("beendet")]
        Beendet = 1,
    }

    /// <summary>
    /// How the start of a charging session was arrived at. Four ways, with four different error
    /// characteristics — which is the whole reason this is not a boolean.
    /// </summary>
    /// <remarks>
    /// The wire names are lower case and without umlauts, like every other enum on these topics,
    /// and they are what the <c>beginn_quelle</c> field of the <c>ladesitzungen</c> table carries.
    /// "Is the plugged-in duration measured?" reads as
    /// <c>beginn_quelle IN ('steckflanke','regelzyklus')</c> — and the other two values say why
    /// it is not, which a boolean could not.
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter<Beginnquelle>))]
    public enum Beginnquelle
    {
        /// <summary>
        /// The payload did not say. Only reachable for a record written before this field
        /// existed; the default is deliberately not a source that claims to be measured.
        /// </summary>
        [JsonStringEnumMemberName("unbekannt")]
        Unbekannt = 0,

        /// <summary>
        /// The plug-in edge the KebaConnector observed itself. Measured, and the best of the
        /// four.
        /// </summary>
        [JsonStringEnumMemberName("steckflanke")]
        Steckflanke = 1,

        /// <summary>
        /// The control cycle in which the ChargingController saw the session appear. Measured
        /// too, and at most one cycle (5 s) late.
        /// </summary>
        [JsonStringEnumMemberName("regelzyklus")]
        Regelzyklus = 2,

        /// <summary>
        /// The clock of the box, for a session that was already running when the controller
        /// started. That clock reports "timeQ": 0 — the value passed a plausibility check but
        /// may still be wrong by an unknown amount in either direction.
        /// </summary>
        [JsonStringEnumMemberName("boxuhr")]
        Boxuhr = 3,

        /// <summary>
        /// The moment the controller first saw an already running session, with no usable box
        /// clock to date it by. Unlike <see cref="Boxuhr"/> the error has a known sign: the
        /// session began earlier than this, so the plugged-in duration is too short.
        /// </summary>
        [JsonStringEnumMemberName("dienstanlauf")]
        Dienstanlauf = 4,
    }

    /// <summary>
    /// The wire spelling of a <see cref="Beginnquelle"/> — the same string the JSON payload
    /// carries, and the string the <c>beginn_quelle</c> column is written with. Same arrangement
    /// and same reason as <see cref="Vertrauensgrade"/>: one place produces the name, and a test
    /// holds the switch to the attributes above.
    /// </summary>
    public static class Beginnquellen
    {
        public static string Drahtname(Beginnquelle quelle) => quelle switch
        {
            Beginnquelle.Unbekannt => "unbekannt",
            Beginnquelle.Steckflanke => "steckflanke",
            Beginnquelle.Regelzyklus => "regelzyklus",
            Beginnquelle.Boxuhr => "boxuhr",
            Beginnquelle.Dienstanlauf => "dienstanlauf",
            _ => throw new ArgumentOutOfRangeException(nameof(quelle), quelle, null),
        };
    }

    /// <summary>
    /// One charging session of one wallbox as the ChargingController sees it, published retained
    /// to daten/Laden/&lt;Ort&gt;/&lt;Box&gt;/Ladesitzung. The controller is the only author of
    /// this topic. See Docs/Ladeprotokoll.md, sections "Tabelle ladesitzungen" and
    /// "Zuständigkeiten".
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It carries session values, not meter readings.</b> The three virtual meters live in
    /// <see cref="Energieaufteilung"/> and never reset; what a session consumed is a difference
    /// between two of their readings. The controller is the one service that watches the session
    /// boundaries in the 5 s cycle, so it forms that difference itself and publishes the result
    /// — a consumer of this topic needs no second topic and no window arithmetic.
    /// </para>
    /// <para>
    /// <b>Retained, and the controller's own backup.</b> The controller subscribes to this topic
    /// as it does to the split: after a restart the retained payload is the only thing that says
    /// which session was running, since when, and how much of it has already been booked.
    /// Without it every rollout would cut the running session in two.
    /// </para>
    /// <para>
    /// The DataHub joins this with the RulesEngine's <see cref="FahrzeugZuordnung"/> over the
    /// <see cref="SitzungsId"/> and writes the <c>ladesitzungen</c> table. It writes only when
    /// both carry the same id — that check is what the shared key is for.
    /// </para>
    /// </remarks>
    public record Ladesitzung()
    {
        /// <summary>
        /// When the controller last changed anything about this session (UTC). Mandatory field
        /// of every payload, see MQTT-Topic-Konvention.md.
        /// </summary>
        /// <remarks>
        /// It stands still while nothing changes, so it reads as "last known state as of" — and
        /// that is exactly what makes it usable as the end of a session whose end the controller
        /// slept through: it is the last moment the controller knew the session was running.
        /// </remarks>
        public DateTimeOffset Zeitpunkt { get; set; }

        /// <summary>
        /// The session as the box numbers it, from <see cref="WallboxStatus.SitzungsId"/>. This
        /// is the key the three contributing topics are joined over.
        /// </summary>
        public int SitzungsId { get; set; }

        /// <summary>When the vehicle was plugged in, or null when nobody could say.</summary>
        public DateTimeOffset? Beginn { get; set; }

        /// <summary>
        /// How <see cref="Beginn"/> was arrived at, and with it how much the plugged-in duration
        /// can be believed.
        /// </summary>
        /// <remarks>
        /// <b>Do not confuse this with <see cref="WallboxStatus.SitzungsBeginnAusBoxZeit"/>,
        /// whose name says where the value came from, not how good it is</b> — there, <c>true</c>
        /// means the value came from the clock of the box, which reports "timeQ": 0 and is the
        /// untrustworthy source, not the trustworthy one. Here the four values say outright what
        /// dated the session, so nobody has to know that trap to read the record.
        /// </remarks>
        public Beginnquelle Beginnquelle { get; set; }

        /// <summary>
        /// Whether <see cref="Beginn"/> is an estimate rather than a measured moment. Derived
        /// from <see cref="Beginnquelle"/> so the two can never disagree, and not serialised —
        /// it carries nothing the payload does not already have. Same arrangement as
        /// <see cref="WallboxStatus.FahrzeugVerbunden"/>.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public bool BeginnGeschaetzt
            => Beginnquelle is not (Beginnquelle.Steckflanke or Beginnquelle.Regelzyklus);

        /// <summary>
        /// When the box stopped reporting this session, null while it is still running.
        /// Accurate to one control cycle: the plug may have been pulled any time within the
        /// five seconds before the controller noticed.
        /// </summary>
        public DateTimeOffset? Ende { get; set; }

        /// <summary>Whether the session is still running.</summary>
        public Ladesitzungszustand Zustand { get; set; }

        /// <summary>Energy in kWh this session drew from the PV share of the house's mix.</summary>
        public decimal EnergiePvKwh { get; set; }

        /// <summary>Energy in kWh this session drew from the battery share.</summary>
        public decimal EnergieBatterieKwh { get; set; }

        /// <summary>Energy in kWh this session drew from the grid share.</summary>
        public decimal EnergieNetzKwh { get; set; }

        /// <summary>
        /// The energy of this session in kWh as the box itself counted it
        /// (<see cref="WallboxStatus.EnergieSitzungWh"/>), held at the last cycle in which the
        /// session was still running.
        /// </summary>
        /// <remarks>
        /// The measured total against which the three attributed shares are compared. Their
        /// difference is the unattributed energy — every interval the controller had to skip,
        /// plus every second it was not running at all. It is reported, never extrapolated away.
        /// </remarks>
        public decimal EnergieBoxKwh { get; set; }

        /// <summary>
        /// Seconds of this session in which the box actually charged, counted for intervals
        /// above <see cref="Energieaufteilung.LadeleistungsschwelleW"/>.
        /// </summary>
        /// <remarks>
        /// Not the plugged-in duration, which is <see cref="Ende"/> minus <see cref="Beginn"/>.
        /// A vehicle hangs on the box overnight and charges for two hours on surplus; the box
        /// reports one twelve hour session either way.
        /// </remarks>
        public long LadezeitSekunden { get; set; }

        /// <summary>
        /// Whether another payload says the same thing. <see cref="Zeitpunkt"/> is left out, so
        /// that a session in which nothing moves is not republished onto a retained topic every
        /// five seconds — same reason as <see cref="FahrzeugZuordnung.GleicheAussageWie"/>.
        /// </summary>
        public bool GleicheAussageWie(Ladesitzung? andere)
            => andere is not null
               && andere.SitzungsId == SitzungsId
               && andere.Beginn == Beginn
               && andere.Beginnquelle == Beginnquelle
               && andere.Ende == Ende
               && andere.Zustand == Zustand
               && andere.EnergiePvKwh == EnergiePvKwh
               && andere.EnergieBatterieKwh == EnergieBatterieKwh
               && andere.EnergieNetzKwh == EnergieNetzKwh
               && andere.EnergieBoxKwh == EnergieBoxKwh
               && andere.LadezeitSekunden == LadezeitSekunden;
    }
}
