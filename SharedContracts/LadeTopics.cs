namespace SharedContracts
{
    /// <summary>
    /// The MQTT topics of the charging domain, following Docs/MQTT-Topic-Konvention.md
    /// (art / Kategorie / Ort / Geraet / Aspekt).
    /// </summary>
    /// <remarks>
    /// Five services publish and subscribe to these topics. Topics are case sensitive, and a
    /// typo does not fail: it silently creates a second topic that nobody publishes to, and
    /// the subscriber simply receives nothing. Spelling them once here is what keeps that from
    /// happening — see the "Schreibregeln" section of the convention.
    /// </remarks>
    public static class LadeTopics
    {
        /// <summary>Both wallboxes are in building M3.</summary>
        public const string Ort = "M3";

        /// <summary>
        /// The wallboxes are named after where they are, never after their manufacturer:
        /// the make is a replaceable implementation detail.
        /// </summary>
        public const string Garage = "Garage";

        /// <summary>The wallbox on the outside parking space.</summary>
        public const string Stellplatz = "Stellplatz";

        /// <summary>Both wallboxes, in the order they are addressed everywhere.</summary>
        public static readonly string[] Wallboxen = [Garage, Stellplatz];

        /// <summary>State of one box, published retained by the KebaConnector.</summary>
        public static string Status(string wallbox) => $"daten/Laden/{Ort}/{wallbox}/Status";

        /// <summary>
        /// All wallbox states, at any location. The location level is part of the pattern
        /// rather than fixed to M3 so that consumers read it from the path — that is what
        /// makes the DataHub converter mechanical instead of carrying a hard coded "M3".
        /// </summary>
        public const string StatusAlle = "daten/Laden/+/+/Status";

        /// <summary>Setpoint for one box, published retained by the ChargingController.</summary>
        public static string Ladestrom(string wallbox) => $"befehle/Laden/{Ort}/{wallbox}/Ladestrom";

        /// <summary>All setpoints of this location, subscribed by the KebaConnector.</summary>
        public const string LadestromAlle = $"befehle/Laden/{Ort}/+/Ladestrom";

        /// <summary>The picture the control loop acted on, published retained for diagnostics and the UI.</summary>
        public const string Situation = $"daten/Laden/{Ort}/Regelung/Situation";

        /// <summary>
        /// The three virtual meters of one box and the momentary split of its charging power,
        /// published retained by the ChargingController. See <see cref="SharedContracts.Energieaufteilung"/>.
        /// </summary>
        /// <remarks>
        /// The controller both publishes and subscribes here: the retained payload is how the
        /// meter readings survive a restart. Aspect level "Energieaufteilung" rather than one
        /// topic per source — the three numbers belong to the same moment, and the convention
        /// asks for state that belongs together to travel in one payload.
        /// </remarks>
        public static string Energieaufteilung(string wallbox) => $"daten/Laden/{Ort}/{wallbox}/Energieaufteilung";

        /// <summary>
        /// The splits of all boxes, at any location. Subscribed by the DataHub, and by the
        /// controller itself to restore its meters — same wildcard shape as
        /// <see cref="StatusAlle"/>, for the same reason.
        /// </summary>
        public const string EnergieaufteilungAlle = "daten/Laden/+/+/Energieaufteilung";

        /// <summary>Charging level and per-box enables, published retained by the web UI.</summary>
        public const string Einstellungen = $"konfiguration/Laden/{Ort}/Regelung/Einstellungen";

        /// <summary>
        /// Splits a wallbox status topic into its location and device level, e.g.
        /// "daten/Laden/M3/Garage/Status" into ("M3", "Garage"). Null for every other topic.
        /// </summary>
        /// <remarks>
        /// The category is checked, not just the shape: other five level topics exist
        /// (daten/Heizung/M1/Mischersteuerung/PositionIst), and matching on depth alone would
        /// feed them into the charging converter and write them under the wrong tags.
        /// </remarks>
        public static (string Ort, string Wallbox)? ZerlegeStatusTopic(string topic)
            => Zerlege(topic, "daten", "Status");

        /// <summary>
        /// Splits a charging current command topic into its location and device level, e.g.
        /// "befehle/Laden/M3/Garage/Ladestrom" into ("M3", "Garage"). Null for every other topic.
        /// </summary>
        public static (string Ort, string Wallbox)? ZerlegeLadestromTopic(string topic)
            => Zerlege(topic, "befehle", "Ladestrom");

        /// <summary>
        /// Splits an energy split topic into its location and device level, e.g.
        /// "daten/Laden/M3/Garage/Energieaufteilung" into ("M3", "Garage"). Null for every
        /// other topic.
        /// </summary>
        public static (string Ort, string Wallbox)? ZerlegeEnergieaufteilungTopic(string topic)
            => Zerlege(topic, "daten", "Energieaufteilung");

        private static (string Ort, string Wallbox)? Zerlege(string topic, string art, string aspekt)
        {
            var parts = topic.Split('/');
            if (parts.Length != 5) return null;
            if (parts[0] != art || parts[1] != "Laden" || parts[4] != aspekt) return null;
            return (parts[2], parts[3]);
        }
    }
}
