using SharedContracts;

namespace SmartHome.DataHub
{
    /// <summary>
    /// Turns what a vehicle reports about itself into InfluxDB records. A pure function of its
    /// arguments — no broker, no clock, no database — so every rule below is testable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Tags.</b> <c>location</c> is "-": a vehicle moves, it has no place. <c>device</c> is
    /// the vehicle out of the topic path, never out of the payload — the same decision the
    /// wallbox branch made in backlog item 7/8, and for the same reason: it makes the converter
    /// mechanical and keeps a renamed nickname out of the tag set.
    /// </para>
    /// <para>
    /// <b>Missing values are not written.</b> Every field of
    /// <see cref="CarStatusData"/> is nullable, and a vehicle delivers what its account has
    /// registered — never a full set. A zero written for an absent value is a measurement that
    /// never happened, and in a chart it is indistinguishable from a real one.
    /// </para>
    /// </remarks>
    public static class Fahrzeugdaten
    {
        /// <summary>The source of these values. A second source for the same vehicle — the
        /// planned WiCAN dongle on the VW — would get its own sensor type and the same
        /// device.</summary>
        public const string SensorType = "Fahrzeug";

        /// <summary>A vehicle has no location. "-" rather than an empty tag, as everywhere
        /// else in this database.</summary>
        public const string OhneOrt = "-";

        /// <summary>
        /// Oldest measurement time still taken seriously. A <c>default(DateTime)</c> or a Unix
        /// epoch zero arrives as a perfectly well formed timestamp and would put a point
        /// decades into the past, where nobody looks and nothing deletes it. The system itself
        /// has no data before September 2025.
        /// </summary>
        public static readonly DateTimeOffset FruehesteMesszeit =
            new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// How far ahead of our own clock a measurement time may be. A vehicle clock running a
        /// few minutes fast is normal; an hour is not, and a point in the future stays the
        /// "latest value" of its series until the future catches up with it.
        /// </summary>
        public static readonly TimeSpan ErlaubterVorlauf = TimeSpan.FromHours(1);

        /// <summary>
        /// When the vehicle measured these values, or null when the payload carries no usable
        /// time and must not be written.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>lastUpdate</c> first, <c>Zeitpunkt</c> only as a fallback. They are not the same
        /// thing: lastUpdate is the measurement time inside the vehicle, Zeitpunkt the moment
        /// the connector published. A healthy connector publishes values that are weeks old,
        /// because the vehicle pushes only on events — on 2026-09-20 the BMW carried a
        /// lastUpdate from 2026-08-16 with nothing broken about it.
        /// </para>
        /// <para>
        /// Using the arrival time instead would enter that 35 day old value as a fresh
        /// measurement on every restart of this service, and the history would fill with
        /// convincing looking points that all carry one ancient value — worse than no data.
        /// </para>
        /// </remarks>
        public static DateTimeOffset? Messzeit(CarStatusData daten, DateTimeOffset jetzt)
        {
            var messzeit = daten.LastUpdate is DateTime gemessen
                ? AlsOffset(gemessen)
                : daten.Zeitpunkt;

            if (messzeit is not DateTimeOffset zeit)
                return null;
            if (zeit < FruehesteMesszeit || zeit > jetzt + ErlaubterVorlauf)
                return null;
            return zeit;
        }

        /// <summary>
        /// Both connectors write an explicit offset ("Z" for the BMW, the local offset for the
        /// VW), so the kind is normally known. A value without one is read as UTC rather than
        /// as local time: the connectors work in UTC, and guessing the host's zone would shift
        /// a measurement by an hour twice a year.
        /// </summary>
        private static DateTimeOffset AlsOffset(DateTime wert) => wert.Kind switch
        {
            DateTimeKind.Utc => new DateTimeOffset(wert, TimeSpan.Zero),
            DateTimeKind.Local => new DateTimeOffset(wert),
            _ => new DateTimeOffset(DateTime.SpecifyKind(wert, DateTimeKind.Utc), TimeSpan.Zero),
        };

        /// <summary>
        /// The charging state as a number, or null for a state nobody has translated. The
        /// vocabulary is the one the two manufacturers use and the web interface already
        /// renders (CarStatusDataVisualizer.LadestatusText) — one vocabulary, two readers.
        /// </summary>
        /// <remarks>
        /// An unknown state yields null and is not written. A made up code would be a number in
        /// the history that means nothing, and "0" would read as "not charging" — a statement
        /// about the vehicle that nobody made.
        /// </remarks>
        public static decimal? Ladestatuscode(string? ladestatus) => ladestatus switch
        {
            "NOCHARGING" or "off" => 0m,
            "CHARGINGACTIVE" or "charging" => 1m,
            "CHARGINGPAUSED" => 2m,
            "CHARGINGENDED" => 3m,
            "CHARGINGERROR" or "error" => 4m,
            "ready_for_charging" => 5m,
            "conservation" => 6m,
            "discharging" => 7m,
            _ => null,
        };

        /// <summary>
        /// Every value of this payload that deserves a history, as records. Absent values
        /// produce no record at all.
        /// </summary>
        /// <param name="fahrzeug">The vehicle out of the topic path: BMW, Mini or VW.</param>
        public static IReadOnlyList<InfluxRecord> NachInfluxRecords(string fahrzeug, CarStatusData daten)
        {
            var records = new List<InfluxRecord>();

            // State of charge and charge-to target. "Ist" and "Soll" as with the thermostats.
            if (daten.Battery is double ladestand)
                records.Add(Prozent(fahrzeug, "Ladestand", "Ist", (decimal)ladestand));
            if (daten.ChargingTarget is double ladeziel)
                records.Add(Prozent(fahrzeug, "Ladeziel", "Soll", (decimal)ladeziel));

            // Range and odometer. The odometer is the one value that makes every other one
            // interpretable later: kWh per 100 km, range at a given state of charge, ageing.
            if (daten.RemainingRange is double reichweite)
                records.Add(Distanz(fahrzeug, "Reichweite", "Ist", (decimal)reichweite));
            if (daten.PredictedRange is double prognose)
                records.Add(Distanz(fahrzeug, "ReichweiteNachLadung", "Prognose", (decimal)prognose));
            if (daten.Mileage is double kilometerstand)
                records.Add(Distanz(fahrzeug, "Kilometerstand", "Ist", (decimal)kilometerstand));

            // The vehicle's own view of the charging power. The wallbox measures the same
            // process from the other side, and the difference between the two is the charging
            // loss — which is only visible if both are recorded.
            if (daten.ChargingPower is double ladeleistung)
                records.Add(Leistung(fahrzeug, "Ladeleistung", (decimal)ladeleistung));

            // Usable battery capacity. Not a meter but a state, so the delta stays 0 and
            // MAX-MIN over a period is 0 — which is the truth: no energy flowed. What is read
            // here is the value itself, over years: the degradation curve of the battery.
            if (daten.MaxEnergy is double kapazitaet)
                records.Add(Energie(fahrzeug, "Batteriekapazitaet", (decimal)kapazitaet));

            // Plug and charging state. The vehicle's own view — which box it hangs on is what
            // the box says, see Docs/Fahrzeug-Wallbox-Zuordnung.md.
            if (daten.ChargerConnected is bool steckerVerbunden)
                records.Add(Status(fahrzeug, "SteckerVerbunden", "Verbindungsstatus", steckerVerbunden ? 1m : 0m));
            if (Ladestatuscode(daten.ChargingStatus) is decimal ladestatus)
                records.Add(Status(fahrzeug, "Ladestatus", "Ist", ladestatus));
            if (daten.Moving is bool faehrt)
                records.Add(Status(fahrzeug, "Faehrt", "Ist", faehrt ? 1m : 0m));

            // Position, the groundwork for backlog item 15. Not evaluated here, only recorded.
            // 0/0 is dropped: SharedContracts.GeoPosition has non-nullable doubles, so a
            // payload that carries an empty position object arrives as the Gulf of Guinea.
            if (daten.Position is SharedContracts.GeoPosition position
                && (position.Latitude != 0 || position.Longitude != 0))
                records.Add(Position(fahrzeug, position));

            return records;
        }

        /// <summary>
        /// The identity of a series. No location in it, because there is none — putting the "-"
        /// into the id would only make it harder to read.
        /// </summary>
        private static string Id(string fahrzeug, string messwert) => $"Fahrzeug_{fahrzeug}_{messwert}";

        private static InfluxPercentageRecord Prozent(string fahrzeug, string messwert, string unterkategorie, decimal wert) =>
            new()
            {
                MeasurementId = Id(fahrzeug, messwert),
                Category = MeasurementCategory.Fahrzeug,
                SubCategory = unterkategorie,
                SensorType = SensorType,
                Location = OhneOrt,
                Device = fahrzeug,
                Measurement = messwert,
                Value_Percent = wert,
            };

        private static InfluxDistanceRecord Distanz(string fahrzeug, string messwert, string unterkategorie, decimal wert) =>
            new()
            {
                MeasurementId = Id(fahrzeug, messwert),
                Category = MeasurementCategory.Fahrzeug,
                SubCategory = unterkategorie,
                SensorType = SensorType,
                Location = OhneOrt,
                Device = fahrzeug,
                Measurement = messwert,
                Value_Km = wert,
            };

        private static InfluxPowerRecord Leistung(string fahrzeug, string messwert, decimal wert) =>
            new()
            {
                MeasurementId = Id(fahrzeug, messwert),
                Category = MeasurementCategory.Fahrzeug,
                SubCategory = "Ladeleistung",
                SensorType = SensorType,
                Location = OhneOrt,
                Device = fahrzeug,
                Measurement = messwert,
                Value_W = wert,
            };

        private static InfluxEnergyRecord Energie(string fahrzeug, string messwert, decimal wert) =>
            new()
            {
                MeasurementId = Id(fahrzeug, messwert),
                Category = MeasurementCategory.Fahrzeug,
                SubCategory = MeasurementSubCategory.Other,
                SensorType = SensorType,
                Location = OhneOrt,
                Device = fahrzeug,
                Measurement = messwert,
                Value_Cumulated_KWh = wert,
                Value_Delta_KWh = 0m,
            };

        private static InfluxStatusRecord Status(string fahrzeug, string messwert, string unterkategorie, decimal wert) =>
            new()
            {
                MeasurementId = Id(fahrzeug, messwert),
                Category = MeasurementCategory.Fahrzeug,
                SubCategory = unterkategorie,
                SensorType = SensorType,
                Location = OhneOrt,
                Device = fahrzeug,
                Measurement = messwert,
                Value_Status = wert,
            };

        private static InfluxPositionRecord Position(string fahrzeug, SharedContracts.GeoPosition position) =>
            new()
            {
                MeasurementId = Id(fahrzeug, "Position"),
                Category = MeasurementCategory.Fahrzeug,
                SubCategory = "-",
                SensorType = SensorType,
                Location = OhneOrt,
                Device = fahrzeug,
                Measurement = "Position",
                Value_Latitude = position.Latitude,
                Value_Longitude = position.Longitude,
            };
    }
}
