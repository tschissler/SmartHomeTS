using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HeartbeatLib;

/// <summary>
/// Builds the heartbeat a .NET service publishes on <c>status/Cluster/Dienst/&lt;Name&gt;</c>.
///
/// The topic follows the existing ESP32 device heartbeat rather than the topic convention -
/// see the "Ausnahme status/" section of Docs/MQTT-Topic-Konvention.md and, for the field by
/// field reasoning, Docs/Service-Heartbeat.md. The consumer is SmartHome.Web
/// (Services/DeviceStatus.cs), which parses the ESP32 format; the two field lists are spelled
/// out separately because the ESP32 side is hand-written C++ and cannot share this type. Keep
/// them in step.
/// </summary>
public sealed class ServiceHeartbeat
{
    /// <summary>The services run in the cluster and have no room. The level cannot be dropped:
    /// the existing format has a fixed depth and the consumer groups cards by it.</summary>
    public const string Ort = "Cluster";

    /// <summary>Device type level. All services share one, so the device page shows them as a
    /// single group instead of one group per service.</summary>
    public const string Geraetetyp = "Dienst";

    public static readonly TimeSpan DefaultIntervall = TimeSpan.FromMinutes(1);

    /// <summary>A description longer than this is truncated. The payload is retained, so it
    /// sits on the broker until it is overwritten - an exception with a stack trace in it
    /// would stay there.</summary>
    private const int MaxBeschreibungslaenge = 500;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // Fields a service cannot fill honestly are left out rather than sent as 0 - see
        // Beschreibung of the nullable fields in SmartHome.Web's DeviceStatus.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _dienstName;
    private readonly string _version;
    private readonly Func<DateTimeOffset> _now;
    private readonly DateTimeOffset _startZeitpunkt;

    public ServiceHeartbeat(string dienstName, string version, Func<DateTimeOffset>? now = null)
    {
        if (string.IsNullOrWhiteSpace(dienstName))
            throw new ArgumentException("A service needs a name for its heartbeat topic.", nameof(dienstName));
        // A wildcard or separator in the name would silently produce a different topic depth
        // and the card would land in the wrong group - or, with '#', not be publishable at all.
        if (dienstName.IndexOfAny(['/', '+', '#']) >= 0)
            throw new ArgumentException($"'{dienstName}' cannot be an MQTT topic level.", nameof(dienstName));

        _dienstName = dienstName;
        _version = string.IsNullOrWhiteSpace(version) ? "0.0.0" : version;
        _now = now ?? (() => DateTimeOffset.UtcNow);
        _startZeitpunkt = _now();
    }

    public string DienstName => _dienstName;

    public string Topic => $"status/{Ort}/{Geraetetyp}/{_dienstName}";

    /// <summary>
    /// The heartbeat as it goes on the wire. <paramref name="letzteAktion"/> is when the service
    /// last did its job successfully - it has to come from the same state the health check reads,
    /// not from a second counter, or readiness and the device page start telling different stories.
    /// </summary>
    public string BuildPayload(HealthReport report, DateTimeOffset? letzteAktion = null)
        => BuildPayload(report.Status, Beschreibung(report), letzteAktion);

    /// <summary>Overload for services whose health is a single check result rather than a report.</summary>
    public string BuildPayload(HealthCheckResult result, DateTimeOffset? letzteAktion = null)
        => BuildPayload(result.Status, Kuerzen(result.Description ?? result.Exception?.Message), letzteAktion);

    private string BuildPayload(HealthStatus status, string? zustandText, DateTimeOffset? letzteAktion)
    {
        var jetzt = _now();
        var payload = new HeartbeatPayload
        {
            Location = Ort,
            DeviceType = Geraetetyp,
            DeviceName = _dienstName,
            Version = _version,
            UptimeSeconds = (long)Math.Max(0, (jetzt - _startZeitpunkt).TotalSeconds),
            LastDataSecondsAgo = letzteAktion is { } aktion
                ? (long)Math.Max(0, (jetzt - aktion).TotalSeconds)
                : null,
            Zeitpunkt = jetzt.ToUniversalTime(),
            Zustand = status.ToString(),
            ZustandText = zustandText,
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    /// <summary>
    /// The descriptions of the registered checks, joined. This is where "last successful action"
    /// becomes readable: the existing checks already phrase it ("Last successful read: 4 seconds
    /// ago"), so the heartbeat quotes them instead of wording it a second time.
    /// </summary>
    private static string? Beschreibung(HealthReport report)
    {
        var mehrere = report.Entries.Count > 1;
        var teile = report.Entries
            .Select(e => (e.Key, Text: e.Value.Description ?? e.Value.Exception?.Message))
            .Where(e => !string.IsNullOrWhiteSpace(e.Text))
            .Select(e => mehrere ? $"{e.Key}: {e.Text}" : e.Text!);

        return Kuerzen(string.Join(" | ", teile));
    }

    private static string? Kuerzen(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        text = text.Trim();
        return text.Length <= MaxBeschreibungslaenge ? text : text[..MaxBeschreibungslaenge] + "…";
    }

    /// <summary>
    /// The wire format. The lower case English fields are the existing ESP32 heartbeat and are
    /// spelled exactly as MQTTClientLib::publishStatus writes them; the three German ones are
    /// new and follow the payload rules of the topic convention. Everything an ESP32 reports
    /// about its hardware - mac, chipModel, ip, rssi, freeHeap, resetReason, mqttConnects - is
    /// absent here, because a pod has no honest value for it.
    /// </summary>
    private sealed record HeartbeatPayload
    {
        [JsonPropertyName("location")] public required string Location { get; init; }
        [JsonPropertyName("deviceType")] public required string DeviceType { get; init; }
        [JsonPropertyName("deviceName")] public required string DeviceName { get; init; }
        [JsonPropertyName("version")] public required string Version { get; init; }
        [JsonPropertyName("uptimeSeconds")] public required long UptimeSeconds { get; init; }
        [JsonPropertyName("lastDataSecondsAgo")] public long? LastDataSecondsAgo { get; init; }

        /// <summary>When this message was put on the broker. Mandatory field of the topic
        /// convention and the reason a service heartbeat is honest where an ESP32 one is not:
        /// a consumer can tell a live heartbeat from a retained one replayed on subscribe.</summary>
        [JsonPropertyName("Zeitpunkt")] public required DateTimeOffset Zeitpunkt { get; init; }

        /// <summary>Healthy / Degraded / Unhealthy, taken from the service's own health checks.</summary>
        [JsonPropertyName("Zustand")] public required string Zustand { get; init; }

        [JsonPropertyName("ZustandText")] public string? ZustandText { get; init; }
    }
}
