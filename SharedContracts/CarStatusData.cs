using System;
using System.Text.Json.Serialization;

namespace SharedContracts
{
    /// <summary>
    /// Everything a vehicle reports about itself, published retained by its connector to
    /// <see cref="FahrzeugTopics.Status"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing here says which wallbox the vehicle is at. The box is the truth about a charging
    /// process; the vehicle only supplies identity and vehicle data. See
    /// Docs/Fahrzeug-Wallbox-Zuordnung.md.
    /// </para>
    /// <para>
    /// <b>Every value is nullable, and that is the point.</b> A vehicle delivers what its
    /// account has registered and what it happened to send since the connector started — never a
    /// full set. A missing value used to deserialise into 0 and was then displayed as "0 km" or
    /// "0 %", which is a statement about the vehicle that nobody made. Null means "not
    /// reported", and the interface renders it as such.
    /// </para>
    /// <para>
    /// The field names stay English camelCase because both connectors write them that way and
    /// the BMW field reference (BMWConnector/DATAPOINTS.md) is keyed by them. Only
    /// <see cref="Zeitpunkt"/> follows the topic convention's German spelling — it is the one
    /// field the convention names itself.
    /// </para>
    /// </remarks>
    public class CarStatusData
    {
        /// <summary>
        /// When the connector published this payload (UTC). Mandatory field of the topic
        /// convention: the message is retained, so the broker replays it on every subscribe
        /// regardless of age, and without a timestamp inside the payload a consumer cannot tell
        /// a fresh value from a five week old one.
        /// </summary>
        /// <remarks>
        /// This is the publication time, not the measurement time — those differ here by more
        /// than usual, because a connector republishes on every event while the values inside
        /// may be days old. <see cref="LastUpdate"/> is the measurement time and the one the age
        /// of a value has to be judged by; Zeitpunkt says whether the connector is still alive.
        /// </remarks>
        [JsonPropertyName("Zeitpunkt")]
        public DateTimeOffset? Zeitpunkt { get; set; }

        /// <summary>Name the owner gave the vehicle in the manufacturer's portal, if any.</summary>
        [JsonPropertyName("nickname")]
        public string? Nickname { get; set; }

        /// <summary>Make, used to pick the vehicle picture: "BMW", "Mini", "VW".</summary>
        [JsonPropertyName("brand")]
        public string Brand { get; set; } = "";

        /// <summary>Model name where the portal delivers one, otherwise the make again.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        /// <summary>State of charge of the HV battery in %.</summary>
        [JsonPropertyName("battery")]
        public double? Battery { get; set; }

        /// <summary>
        /// Charging state as the vehicle words it: NOCHARGING / CHARGINGACTIVE /
        /// CHARGINGPAUSED / CHARGINGENDED / CHARGINGERROR for BMW and Mini, lower case state
        /// names for the VW.
        /// </summary>
        [JsonPropertyName("chargingStatus")]
        public string? ChargingStatus { get; set; }

        /// <summary>Charging state of the HV system, where the account has it registered.</summary>
        [JsonPropertyName("hvChargingStatus")]
        public string? HvChargingStatus { get; set; }

        /// <summary>Charge-to target in %.</summary>
        [JsonPropertyName("chargingTarget")]
        public double? ChargingTarget { get; set; }

        /// <summary>Estimated end of the running charge, null when not charging.</summary>
        [JsonPropertyName("chargingEndTime")]
        public DateTime? ChargingEndTime { get; set; }

        /// <summary>
        /// Whether the vehicle sees a cable in its charging port. This is the vehicle's own
        /// view — whether it is charging at one of our wallboxes is what the box says, not this.
        /// </summary>
        [JsonPropertyName("chargerConnected")]
        public bool? ChargerConnected { get; set; }

        /// <summary>
        /// Counter that the BMW increments on every plug-in event. Only useful as a diagnostic:
        /// it distinguishes two charges in a row from one long one.
        /// </summary>
        [JsonPropertyName("plugEventId")]
        public int? PlugEventId { get; set; }

        /// <summary>Operating state of the vehicle, e.g. "parked".</summary>
        [JsonPropertyName("state")]
        public string? State { get; set; }

        /// <summary>Remaining electric range in km, as the instrument cluster shows it.</summary>
        [JsonPropertyName("remainingRange")]
        public double? RemainingRange { get; set; }

        /// <summary>Range in km the vehicle predicts for the end of the running charge.</summary>
        [JsonPropertyName("predictedRange")]
        public double? PredictedRange { get; set; }

        /// <summary>Odometer reading in km.</summary>
        [JsonPropertyName("mileage")]
        public double? Mileage { get; set; }

        /// <summary>Average consumption in kWh/100 km.</summary>
        [JsonPropertyName("avgConsumption")]
        public double? AvgConsumption { get; set; }

        /// <summary>Charging power in W the vehicle measures on its side.</summary>
        [JsonPropertyName("chargingPower")]
        public double? ChargingPower { get; set; }

        /// <summary>Usable capacity of the HV battery in kWh.</summary>
        [JsonPropertyName("maxEnergy")]
        public double? MaxEnergy { get; set; }

        /// <summary>Charging mode the vehicle chose, e.g. NORMAL_PROGNOSE_BASED.</summary>
        [JsonPropertyName("chargingMode")]
        public string? ChargingMode { get; set; }

        /// <summary>Charging voltage in V, AC charging only.</summary>
        [JsonPropertyName("acVoltage")]
        public double? AcVoltage { get; set; }

        /// <summary>Maximum charging current in A the vehicle accepts, AC charging only.</summary>
        [JsonPropertyName("acAmpere")]
        public double? AcAmpere { get; set; }

        /// <summary>
        /// When the vehicle measured these values. This is the age that matters: a vehicle
        /// pushes only on events, so a connector that is perfectly healthy still carries values
        /// that are days old.
        /// </summary>
        [JsonPropertyName("lastUpdate")]
        public DateTime? LastUpdate { get; set; }

        /// <summary>
        /// Last known position. Absent from the VW entirely — the EU Data Act export contains
        /// no GPS data at all.
        /// </summary>
        [JsonPropertyName("position")]
        public GeoPosition? Position { get; set; }

        /// <summary>Whether the vehicle is moving.</summary>
        [JsonPropertyName("moving")]
        public bool? Moving { get; set; }
    }

    public class GeoPosition
    {
        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }
    }
}
