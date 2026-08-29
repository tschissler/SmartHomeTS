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

        /// <summary>When this process received the message. Set by the service, not by the device.</summary>
        [JsonIgnore]
        public DateTimeOffset ReceivedAt { get; set; }

        /// <summary>
        /// Age of the heartbeat. Prefers the device timestamp, because a retained message can be
        /// picked up long after it was sent - the receive time would then look deceptively fresh.
        /// </summary>
        [JsonIgnore]
        public TimeSpan Age
        {
            get
            {
                if (DateTime.TryParse(Timestamp, out var sent))
                {
                    // The devices send local time (UTC+1 offset configured in their NTP client)
                    var sentOffset = new DateTimeOffset(sent, TimeSpan.FromHours(1));
                    var age = DateTimeOffset.Now - sentOffset;
                    if (age > TimeSpan.Zero) return age;
                }
                return DateTimeOffset.Now - ReceivedAt;
            }
        }

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
