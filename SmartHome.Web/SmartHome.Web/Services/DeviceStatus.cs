using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartHome.Web.Services
{
    /// <summary>
    /// A device heartbeat as published by MQTTClientLib::publishStatus() on
    /// status/&lt;location&gt;/&lt;deviceType&gt;/&lt;deviceName&gt;.
    /// </summary>
    public class DeviceStatus
    {
        public string Location { get; set; } = "";
        public string DeviceType { get; set; } = "";
        public string DeviceName { get; set; } = "";
        public string Version { get; set; } = "";
        public string Mac { get; set; } = "";
        public string Ip { get; set; } = "";
        public int Rssi { get; set; }
        public long UptimeSeconds { get; set; }
        public long FreeHeap { get; set; }
        public string ResetReason { get; set; } = "";
        public int MqttConnects { get; set; }

        /// <summary>Device clock at the time of sending; empty when the device has no NTP time yet.</summary>
        public string Timestamp { get; set; } = "";

        /// <summary>Firmware specific fields such as relay states, kept as raw JSON.</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement> Extra { get; set; } = new();

        /// <summary>
        /// When a heartbeat with *changed* content last arrived. Deliberately not updated for an
        /// identical repeat: on every reconnect the broker replays all retained messages, which
        /// would otherwise make long-dead devices look freshly alive.
        /// </summary>
        [JsonIgnore]
        public DateTimeOffset ReceivedAt { get; set; }

        /// <summary>The payload behind ReceivedAt, used to recognise an unchanged repeat.</summary>
        [JsonIgnore]
        public string RawPayload { get; set; } = "";

        /// <summary>
        /// Age of the heartbeat, measured locally. The device timestamp is shown but not used
        /// here: the firmwares configure different NTP offsets, so a device on UTC would appear
        /// an hour older than it is and be flagged dead.
        /// </summary>
        [JsonIgnore]
        public TimeSpan Age => DateTimeOffset.Now - ReceivedAt;

        [JsonIgnore]
        public string ExtraSummary =>
            string.Join(", ", Extra.Select(e => $"{e.Key}={e.Value}"));
    }

    /// <summary>A device that only reports a version topic and has no heartbeat yet.</summary>
    public class LegacyDevice
    {
        public string Topic { get; set; } = "";
        public string Version { get; set; } = "";
        public DateTimeOffset ReceivedAt { get; set; }
    }
}
