using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartHome.Web.Services
{
    /// <summary>
    /// A device heartbeat as published by MQTTClientLib::publishStatus() on
    /// status/&lt;location&gt;/&lt;deviceType&gt;/&lt;deviceName&gt;.
    ///
    /// The .NET services publish the same shape on status/Cluster/Dienst/&lt;Name&gt; via
    /// Libs/HeartbeatLib - minus everything that describes ESP32 hardware, plus Zeitpunkt and
    /// the health state. Which is why the hardware fields are nullable: absent means "this
    /// sender has no such value", and a 0 would be read as a measurement. See
    /// Docs/Service-Heartbeat.md.
    /// </summary>
    public class DeviceStatus
    {
        public string Location { get; set; } = "";
        public string DeviceType { get; set; } = "";
        public string DeviceName { get; set; } = "";
        public string Version { get; set; } = "";
        public string Mac { get; set; } = "";
        public string ChipModel { get; set; } = "";
        public string Ip { get; set; } = "";
        public int? Rssi { get; set; }
        public long UptimeSeconds { get; set; }
        public long? FreeHeap { get; set; }
        public string ResetReason { get; set; } = "";
        public int? MqttConnects { get; set; }

        /// <summary>Seconds between the last data publish and this heartbeat; null when the
        /// firmware does not report it or nothing was sent yet.</summary>
        public long? LastDataSecondsAgo { get; set; }

        /// <summary>Device clock at the time of sending; empty when the device has no NTP time yet.
        /// Not used for the age - see <see cref="Age"/>.</summary>
        public string Timestamp { get; set; } = "";

        /// <summary>
        /// When the sender put this message on the broker, UTC. Mandatory field of the topic
        /// convention, sent by the .NET and Python services and by no ESP32 firmware. Its
        /// presence is what makes <see cref="Age"/> survive a restart of this application.
        /// </summary>
        public DateTimeOffset? Zeitpunkt { get; set; }

        /// <summary>Healthy / Degraded / Unhealthy as the sender's own health checks report it;
        /// null for senders that have none (every ESP32).</summary>
        public string? Zustand { get; set; }

        /// <summary>What the health checks say in words, e.g. "Last successful read: 4 seconds ago".</summary>
        public string? ZustandText { get; set; }

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
        /// Age of the heartbeat: from <see cref="Zeitpunkt"/> when the sender supplies one,
        /// otherwise from the local receive time.
        ///
        /// The local fallback exists for the ESP32 firmwares: they configure different NTP
        /// offsets, so a device on UTC would appear an hour older than it is and be flagged
        /// dead. Its price is that a retained heartbeat replayed after a restart of this
        /// application makes a long-dead device look alive for three minutes.
        ///
        /// The services do not need to pay it: they run in the cluster on UTC, and the topic
        /// convention makes Zeitpunkt mandatory in every retained payload for exactly this
        /// reason. Preferring it where it exists leaves the 17 devices untouched and makes the
        /// services honest across a restart. Changing the devices over is a decision about the
        /// whole fleet and an OTA rollout, not about this page.
        /// </summary>
        [JsonIgnore]
        public TimeSpan Age => DateTimeOffset.Now - (Zeitpunkt ?? ReceivedAt);

        /// <summary>Approximate boot time of the current run. Stable within one boot (up to a
        /// few seconds of jitter), different after every reset - which is what identifies a
        /// reset warning the user has already acknowledged.</summary>
        [JsonIgnore]
        public long BootEpochSeconds => ReceivedAt.ToUnixTimeSeconds() - UptimeSeconds;

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
