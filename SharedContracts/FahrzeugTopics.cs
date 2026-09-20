namespace SharedContracts
{
    /// <summary>
    /// The MQTT topics of the vehicle domain, following Docs/MQTT-Topic-Konvention.md
    /// (art / Kategorie / Geraet / Aspekt).
    /// </summary>
    /// <remarks>
    /// A vehicle is location-less: it moves, and it sometimes charges elsewhere. The category
    /// therefore has four levels where <see cref="LadeTopics"/> has five — the convention asks
    /// for that decision to be made once per category, never per message, so that the depth
    /// stays constant and a "+" wildcard keeps working.
    ///
    /// Spelled once here for the same reason as the charging topics: topics are case sensitive,
    /// and a typo does not fail. It silently creates a second topic nobody publishes to, and the
    /// subscriber simply receives nothing.
    /// </remarks>
    public static class FahrzeugTopics
    {
        /// <summary>The BMW, published by the BMWConnector.</summary>
        public const string Bmw = "BMW";

        /// <summary>The Mini, published by the same connector from a separate BMW account.</summary>
        public const string Mini = "Mini";

        /// <summary>The ID.4, published by the VWConnector via the EU Data Act portal.</summary>
        public const string Vw = "VW";

        /// <summary>
        /// The vehicles the system knows, in the order they are shown. A vehicle stays in this
        /// list even while it reports nothing — the interface shows an empty card rather than
        /// dropping the vehicle, because a missing card reads as "there is no such vehicle".
        /// </summary>
        public static readonly string[] Fahrzeuge = [Bmw, Mini, Vw];

        /// <summary>
        /// What one vehicle reports about itself, published retained by its connector.
        /// See <see cref="SharedContracts.CarStatusData"/> for the payload.
        /// </summary>
        public static string Status(string fahrzeug) => $"daten/Fahrzeug/{fahrzeug}/Status";

        /// <summary>
        /// All vehicles. One subscription instead of a case list — the levels of the convention
        /// put every vehicle at the same depth, so a further vehicle costs no reader a change.
        /// </summary>
        public const string StatusAlle = "daten/Fahrzeug/+/Status";

        /// <summary>
        /// Reads the vehicle out of a vehicle status topic, e.g. "daten/Fahrzeug/BMW/Status"
        /// into "BMW". Null for every other topic.
        /// </summary>
        /// <remarks>
        /// The category is checked, not just the shape: other four level topics under "daten"
        /// exist, and matching on depth alone would feed them into the vehicle dictionary.
        /// </remarks>
        public static string? ZerlegeStatusTopic(string topic)
        {
            var parts = topic.Split('/');
            if (parts.Length != 4) return null;
            if (parts[0] != "daten" || parts[1] != "Fahrzeug" || parts[3] != "Status") return null;
            return parts[2];
        }
    }
}
