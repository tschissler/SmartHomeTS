using HeartbeatLib;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MQTTClient;
using MQTTnet.Protocol;
using RulesEngine;
using RulesEngine.Rules;
using SharedContracts;
using System.Text.Json;

// Display version information on startup
var versionInfo = VersionInfo.GetVersionInfo();
// Makes the service visible on status/# next to the 17 ESP32 devices, and replaces
// meta/RulesEngine/version, which carried nothing but a number. See Docs/Service-Heartbeat.md.
var serviceHeartbeat = new ServiceHeartbeat("RulesEngine", versionInfo.Version);
// Set once the health check web app is up. Until then there is nothing to report and the
// service heartbeat waits - it must say what the registered checks say, not guess.
HealthCheckService? healthChecks = null;
Console.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  RulesEngine Starting                                              ║");
Console.WriteLine("╠════════════════════════════════════════════════════════════════════╣");
Console.WriteLine($"║  {versionInfo.GetDisplayString().PadRight(66)}║");
Console.WriteLine("╚════════════════════════════════════════════════════════════════════╝");

// Build configuration from environment variables
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddEnvironmentVariables(prefix: "RulesEngineSettings__")
    .Build();

// Read configuration values
var mqttBroker = configuration["MqttBroker"] ?? "smarthomepi2";
var mqttPort = int.Parse(configuration["MqttPort"] ?? "32004");
var healthCheckPort = int.Parse(configuration["HealthCheckPort"] ?? "8080");
var faStatusTopic = configuration["FaStatusTopic"] ?? "cangateway/M1/WEZ/Status/FA_Status";
var mixerCommandTopics = (configuration["MixerCommandTopics"]
    ?? "commands/MixerController/M1/Mischer_FBHZ,commands/MixerController/M1/Mischer_HK")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var maxStatusAgeMinutes = int.Parse(configuration["MaxStatusAgeMinutes"] ?? "15");
var evaluationIntervalSeconds = int.Parse(configuration["EvaluationIntervalSeconds"] ?? "60");

// Vorlauftemperatur-Regelung FBHZ (Taupunktschutz)
var fbhzFlowTempTopic = configuration["FbhzFlowTempTopic"] ?? "cangateway/M1/FBHZ/Temperatur/Vorlauf_Ist";
var fbhzPumpTopic = configuration["FbhzPumpTopic"] ?? "cangateway/M1/FBHZ/Status/Pumpe";
var fbhzMixerCommandTopic = configuration["FbhzMixerCommandTopic"] ?? "commands/MixerController/M1/Mischer_FBHZ";
var configTopic = configuration["ConfigTopic"] ?? "config/RulesEngine";
// Startup default only — the retained config/RulesEngine message overrides it at runtime
var coolingFlowTargetTemperature = double.Parse(configuration["CoolingFlowTargetTemperature"] ?? "15.0", System.Globalization.CultureInfo.InvariantCulture);
var coolingFlowDeadbandKelvin = double.Parse(configuration["CoolingFlowDeadbandKelvin"] ?? "0.5", System.Globalization.CultureInfo.InvariantCulture);
var pulseSecondsPerKelvin = double.Parse(configuration["PulseSecondsPerKelvin"] ?? "5.0", System.Globalization.CultureInfo.InvariantCulture);
var minPulseSeconds = int.Parse(configuration["MinPulseSeconds"] ?? "2");
var maxPulseSeconds = int.Parse(configuration["MaxPulseSeconds"] ?? "20");

// Fahrzeug-Wallbox-Zuordnung (Docs/Fahrzeug-Wallbox-Zuordnung.md)
var maxWallboxStatusAgeMinutes = int.Parse(configuration["MaxWallboxStatusAgeMinutes"] ?? "5");
var maxVehicleReportAgeHours = int.Parse(configuration["MaxVehicleReportAgeHours"] ?? "6");
var assignmentRestoreSeconds = int.Parse(configuration["AssignmentRestoreSeconds"] ?? "15");

Console.WriteLine($" ### Configuration: MQTT Broker={mqttBroker}:{mqttPort}, Health Check Port={healthCheckPort}");
Console.WriteLine($" ### Service heartbeat topic: {serviceHeartbeat.Topic}");
Console.WriteLine($" ### FA_Status topic: {faStatusTopic}");
Console.WriteLine($" ### Mixer command topics: {string.Join(", ", mixerCommandTopics)}");
Console.WriteLine($" ### Max status age: {maxStatusAgeMinutes} min, evaluation interval: {evaluationIntervalSeconds} s");
Console.WriteLine($" ### Flow regulation: target {coolingFlowTargetTemperature}°C ±{coolingFlowDeadbandKelvin}K, {pulseSecondsPerKelvin} s/K ({minPulseSeconds}-{maxPulseSeconds} s) -> {fbhzMixerCommandTopic}");
Console.WriteLine($" ### Config topic: {configTopic} (retained JSON, overrides CoolingFlowTargetTemperature at runtime)");
Console.WriteLine($" ### Vehicle assignment: {LadeTopics.ZuordnungAlle} / {LadeTopics.ZuordnungKorrekturAlle} at {LadeTopics.Ort}, " +
    $"box state valid for {maxWallboxStatusAgeMinutes} min, vehicle report for {maxVehicleReportAgeHours} h, " +
    $"restore window {assignmentRestoreSeconds} s");

// FA_Status 4 = Warmwasserladung der Hoval Belaria — fixed by the heat pump, not configuration
string[] warmWaterStatusValues = ["4"];
var mixerRule = new MixerPositionRule(warmWaterStatusValues, TimeSpan.FromMinutes(maxStatusAgeMinutes));
var coolingRule = new CoolingFlowTemperatureRule(
    TimeSpan.FromMinutes(maxStatusAgeMinutes),
    coolingFlowDeadbandKelvin,
    pulseSecondsPerKelvin, minPulseSeconds, maxPulseSeconds);

string? lastFaStatus = null;
DateTimeOffset lastFaStatusTime = DateTimeOffset.MinValue;
bool? lastFbhzPumpRunning = null;
DateTimeOffset lastFbhzPumpTime = DateTimeOffset.MinValue;
double? lastFbhzFlowTemp = null;
DateTimeOffset lastFbhzFlowTempTime = DateTimeOffset.MinValue;
MixerPosition? lastPublishedPosition = null;
var evaluationLock = new object();

var assignmentRule = new VehicleAssignmentRule(
    TimeSpan.FromMinutes(maxWallboxStatusAgeMinutes),
    TimeSpan.FromHours(maxVehicleReportAgeHours));
var assignmentRestoreWindow = TimeSpan.FromSeconds(assignmentRestoreSeconds);
var startedAt = DateTimeOffset.UtcNow;
var restoreWindowReported = false;

// Everything the assignment rule reads, and its own last word per box. All four dictionaries
// are filled exclusively from retained topics, so after a restart they are complete again
// within milliseconds of subscribing - which is what the restore window below waits for.
var wallboxStatus = new Dictionary<string, WallboxStatus>();
var fahrzeugStatus = new Dictionary<string, CarStatusData>();
var zuordnungsKorrekturen = new Dictionary<string, FahrzeugZuordnung>();
var letzteZuordnung = new Dictionary<string, FahrzeugZuordnung>();
var assignmentLock = new object();

// Start health check HTTP server in background
var healthCheckTask = Task.Run(() => StartHealthCheckServer(healthCheckPort));

Console.WriteLine("  - Connecting to MQTT Broker");
var mqttClient = new MQTTClient.MQTTClient("RulesEngine", mqttBroker, mqttPort);
Console.WriteLine($"    ClientId: {mqttClient.ClientId}");
RulesEngineHealthCheck.UpdateMqttConnectionStatus(true);
mqttClient.OnConnectionStateChanged += (_, connected) => RulesEngineHealthCheck.UpdateMqttConnectionStatus(connected);

mqttClient.OnMessageReceived += MqttMessageReceived;
await mqttClient.SubscribeToTopic(faStatusTopic);
await mqttClient.SubscribeToTopic(fbhzFlowTempTopic);
await mqttClient.SubscribeToTopic(fbhzPumpTopic);
await mqttClient.SubscribeToTopic(configTopic);
// Its own assignment topic included: after a restart those retained messages are the only
// thing that restores the running assignments, and the history behind them.
await mqttClient.SubscribeToTopic(LadeTopics.StatusAlle);
await mqttClient.SubscribeToTopic(FahrzeugTopics.StatusAlle);
await mqttClient.SubscribeToTopic(LadeTopics.ZuordnungAlle);
await mqttClient.SubscribeToTopic(LadeTopics.ZuordnungKorrekturAlle);
Console.WriteLine("    ...Done");

// The version now rides in the service heartbeat, together with uptime and health. The old
// retained meta topic is cleared with an empty payload rather than simply abandoned - left
// alone it would sit on the broker forever, naming a version nobody updates any more.
await mqttClient.PublishAsync("meta/RulesEngine/version", "", MqttQualityOfServiceLevel.AtLeastOnce, true);

// Publish once at startup to establish the retained command, then only when
// the decision changes. The periodic evaluation exists so the staleness rule
// (MaxStatusAge) also fires when the CAN gateway stops sending — decisions
// must not depend on messages that are no longer arriving.
var initialPublishTimer = new Timer(_ => EvaluateAndPublish("startup", force: true), null, 2000, Timeout.Infinite);
// Pulses only from the periodic tick: they are relative moves, so their rate
// must be fixed by the timer, not by how often the CAN gateway publishes.
var evaluationTimer = new Timer(_ =>
{
    EvaluateAndPublish("periodic evaluation");
    EvaluateCoolingPulse();
    // Also on the tick, not only on incoming messages: the ageing of a box state and of a
    // vehicle report has to take effect even while nothing is arriving any more.
    EvaluateAssignments();
}, null, evaluationIntervalSeconds * 1000, evaluationIntervalSeconds * 1000);

// Separate from the evaluation tick on purpose: the heartbeat has to keep going when a rule
// evaluation fails, because that failure is exactly what it is supposed to report.
var heartbeatTimer = new Timer(PublishServiceHeartbeat, null,
    TimeSpan.Zero, ServiceHeartbeat.DefaultIntervall);

Thread.Sleep(Timeout.Infinite);

// Retained, because it is state: the last heartbeat stays on the broker after the pod dies,
// and its Zeitpunkt is what turns the card on the device page silent.
async void PublishServiceHeartbeat(object? state)
{
    try
    {
        if (healthChecks is null || !mqttClient.IsConnected)
            return;
        var report = await healthChecks.CheckHealthAsync();
        var payload = serviceHeartbeat.BuildPayload(report, RulesEngineHealthCheck.LastEvaluation);
        await mqttClient.PublishAsync(serviceHeartbeat.Topic, payload,
            MqttQualityOfServiceLevel.AtLeastOnce, true);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error publishing service heartbeat: {ex.Message}");
    }
}

void MqttMessageReceived(object? sender, MqttMessageReceivedEventArgs e)
{
    if (e.Topic == configTopic)
    {
        ApplyConfig(e.Payload);
        return;
    }

    if (TrackAssignmentInput(e.Topic, e.Payload))
    {
        EvaluateAssignments();
        return;
    }

    if (e.Topic == fbhzFlowTempTopic)
    {
        lock (evaluationLock)
        {
            lastFbhzFlowTemp = double.TryParse(e.Payload.Trim(), System.Globalization.CultureInfo.InvariantCulture, out var temp)
                ? temp
                : null;
            lastFbhzFlowTempTime = DateTimeOffset.UtcNow;
        }
        return;
    }

    if (e.Topic == fbhzPumpTopic)
    {
        lock (evaluationLock)
        {
            lastFbhzPumpRunning = e.Payload.Trim() == "1";
            lastFbhzPumpTime = DateTimeOffset.UtcNow;
        }
        return;
    }

    if (e.Topic != faStatusTopic)
    {
        return;
    }

    var newStatus = e.Payload.Trim();
    lock (evaluationLock)
    {
        if (newStatus != lastFaStatus)
        {
            Console.WriteLine($"FA_Status changed: '{lastFaStatus}' -> '{newStatus}'");
        }
        lastFaStatus = newStatus;
        lastFaStatusTime = DateTimeOffset.UtcNow;
    }

    EvaluateAndPublish("status update");
}

void EvaluateAndPublish(string trigger, bool force = false)
{
    try
    {
        MixerPosition position;
        bool shouldPublish;
        lock (evaluationLock)
        {
            position = mixerRule.Evaluate(lastFaStatus, Age(lastFaStatusTime, DateTimeOffset.UtcNow));

            shouldPublish = force || position != lastPublishedPosition;
            if (position != lastPublishedPosition)
            {
                Console.WriteLine($"Mixer target changed to '{PositionToPayload(position)}' (FA_Status='{lastFaStatus}', trigger={trigger})");
            }
            lastPublishedPosition = position;
        }

        if (shouldPublish)
        {
            var payload = PositionToPayload(position);
            foreach (var topic in mixerCommandTopics)
            {
                mqttClient.PublishAsync(topic, payload, MqttQualityOfServiceLevel.AtLeastOnce, true)
                    .GetAwaiter().GetResult();
            }
        }
        RulesEngineHealthCheck.UpdateLastEvaluation();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error publishing mixer position: {ex.Message}");
    }
}

void EvaluateCoolingPulse()
{
    try
    {
        MixerPulse? pulse;
        double target;
        lock (evaluationLock)
        {
            var now = DateTimeOffset.UtcNow;
            target = coolingFlowTargetTemperature;
            pulse = coolingRule.Evaluate(
                lastPublishedPosition,
                lastFbhzPumpRunning, Age(lastFbhzPumpTime, now),
                lastFbhzFlowTemp, Age(lastFbhzFlowTempTime, now),
                target);
        }

        if (pulse == null)
        {
            return;
        }

        // Not retained: a pulse is a relative move, replaying it after a
        // reconnect would drift the mixer without any temperature reason
        var payload = $"{(pulse.Direction == PulseDirection.Open ? "open" : "close")}:{pulse.Seconds}";
        Console.WriteLine($"Cooling pulse '{payload}' (Vorlauf={lastFbhzFlowTemp}°C, Soll={target}°C)");
        mqttClient.PublishAsync(fbhzMixerCommandTopic, payload, MqttQualityOfServiceLevel.AtLeastOnce, false)
            .GetAwaiter().GetResult();
        RulesEngineHealthCheck.UpdateLastEvaluation();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error publishing cooling pulse: {ex.Message}");
    }
}

void ApplyConfig(string payload)
{
    try
    {
        using var doc = System.Text.Json.JsonDocument.Parse(payload);
        if (doc.RootElement.TryGetProperty("CoolingFlowTargetTemperature", out var targetElement)
            && targetElement.TryGetDouble(out var newTarget))
        {
            lock (evaluationLock)
            {
                if (Math.Abs(newTarget - coolingFlowTargetTemperature) > 0.001)
                {
                    Console.WriteLine($"Config: CoolingFlowTargetTemperature {coolingFlowTargetTemperature}°C -> {newTarget}°C");
                }
                coolingFlowTargetTemperature = newTarget;
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error parsing config message: {ex.Message}");
    }
}

/// <summary>
/// Files a message the assignment rule reads. Returns true when the topic belonged to it.
/// </summary>
/// <remarks>
/// Nothing here records an arrival time, and that is deliberate: every one of these topics is
/// retained, so on startup and after every reconnect they all arrive at once as a burst. They
/// are state, not news, and their age lives inside the payload.
/// </remarks>
bool TrackAssignmentInput(string topic, string payload)
{
    if (LadeTopics.ZerlegeStatusTopic(topic) is (string statusOrt, string statusBox))
    {
        // Other locations are somebody else's installation; merging them under the same box
        // name would quietly mix two systems.
        if (statusOrt != LadeTopics.Ort) return true;
        return Deserialisiere<WallboxStatus>(topic, payload, wert =>
        {
            lock (assignmentLock) wallboxStatus[statusBox] = wert;
        });
    }

    if (FahrzeugTopics.ZerlegeStatusTopic(topic) is string fahrzeug)
    {
        return Deserialisiere<CarStatusData>(topic, payload, wert =>
        {
            lock (assignmentLock) fahrzeugStatus[fahrzeug] = wert;
        });
    }

    if (LadeTopics.ZerlegeZuordnungKorrekturTopic(topic) is (string korrekturOrt, string korrekturBox))
    {
        if (korrekturOrt != LadeTopics.Ort) return true;
        return Deserialisiere<FahrzeugZuordnung>(topic, payload, wert =>
        {
            lock (assignmentLock) zuordnungsKorrekturen[korrekturBox] = wert;
        });
    }

    if (LadeTopics.ZerlegeZuordnungTopic(topic) is (string zuordnungOrt, string zuordnungBox))
    {
        if (zuordnungOrt != LadeTopics.Ort) return true;
        // This service's own topic. Reading it back is the entire restart path, and the echo of
        // a publication of its own simply confirms what is already in the dictionary.
        return Deserialisiere<FahrzeugZuordnung>(topic, payload, wert =>
        {
            lock (assignmentLock) letzteZuordnung[zuordnungBox] = wert;
        });
    }

    return false;
}

bool Deserialisiere<T>(string topic, string payload, Action<T> uebernehmen)
{
    try
    {
        var wert = JsonSerializer.Deserialize<T>(payload);
        if (wert is not null)
        {
            uebernehmen(wert);
        }
    }
    catch (JsonException ex)
    {
        // Keep what we have: a malformed message must not drop an assignment.
        Console.WriteLine($"Ignoring malformed payload on {topic}: {ex.Message}");
    }
    return true;
}

/// <summary>
/// Works out which vehicle is at which box and publishes the boxes whose statement changed.
/// </summary>
void EvaluateAssignments()
{
    try
    {
        var jetzt = DateTimeOffset.UtcNow;
        lock (assignmentLock)
        {
            // Nothing is published before the retained messages have had time to arrive.
            // Publishing earlier would write a fresh "unbekannt" over the very topic the
            // running assignment has to be restored from - and the RulesEngine is rolled out
            // as often as any other service here.
            if (jetzt - startedAt < assignmentRestoreWindow)
            {
                return;
            }
            if (!restoreWindowReported)
            {
                restoreWindowReported = true;
                Console.WriteLine($"Vehicle assignment: restore window over, {letzteZuordnung.Count} retained " +
                    $"assignment(s), {wallboxStatus.Count} box state(s), {fahrzeugStatus.Count} vehicle(s), " +
                    $"{zuordnungsKorrekturen.Count} manual override(s) restored");
            }

            var ziel = assignmentRule.Evaluate(
                wallboxStatus, fahrzeugStatus, zuordnungsKorrekturen, letzteZuordnung, jetzt);

            foreach (var (box, neu) in ziel)
            {
                if (neu.GleicheAussageWie(letzteZuordnung.GetValueOrDefault(box)))
                {
                    continue;
                }

                var vorher = letzteZuordnung.GetValueOrDefault(box);
                Console.WriteLine($"Zuordnung {box}: session {neu.SitzungsId} -> " +
                    $"{neu.Fahrzeug ?? "kein Fahrzeug"} ({neu.Vertrauen}), was " +
                    $"{vorher?.Fahrzeug ?? "kein Fahrzeug"} ({vorher?.Vertrauen.ToString() ?? "nichts"}, " +
                    $"session {vorher?.SitzungsId?.ToString() ?? "-"})");

                // Published while holding the lock so the baseline can only move on once the
                // broker has taken the message: a failed publish must leave the assignment
                // pending, not silently mark it as done.
                mqttClient.PublishAsync(
                        LadeTopics.Zuordnung(box),
                        JsonSerializer.Serialize(neu),
                        MqttQualityOfServiceLevel.AtLeastOnce,
                        true)
                    .GetAwaiter().GetResult();
                letzteZuordnung[box] = neu;
            }
        }
        // Deliberately no UpdateLastEvaluation here. The health check watches whether the
        // periodic timer is still running, and this path also fires on every incoming box
        // state - a few times a second. Refreshing it from here would keep the service
        // reporting healthy with a dead timer.
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error publishing vehicle assignment: {ex.Message}");
    }
}

static TimeSpan Age(DateTimeOffset timestamp, DateTimeOffset now)
    => timestamp == DateTimeOffset.MinValue ? TimeSpan.MaxValue : now - timestamp;

static string PositionToPayload(MixerPosition position)
    => position == MixerPosition.Closed ? "close" : "open";

void StartHealthCheckServer(int port)
{
    var builder = WebApplication.CreateBuilder();
    // Health check probes would otherwise rotate away the useful log lines within hours
    builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

    // Add health checks
    builder.Services.AddHealthChecks()
        .AddCheck<RulesEngineHealthCheck>("rules_engine");

    var app = builder.Build();

    // The same HealthCheckService the /ready probe answers from. The service heartbeat reports
    // what it says rather than re-deciding what "healthy" means.
    healthChecks = app.Services.GetRequiredService<HealthCheckService>();

    // Configure health check endpoints
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var result = System.Text.Json.JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    duration = e.Value.Duration.TotalMilliseconds
                }),
                totalDuration = report.TotalDuration.TotalMilliseconds
            });
            await context.Response.WriteAsync(result);
        }
    });

    // Simple liveness probe
    app.MapGet("/healthz", () => Results.Ok(new { status = "alive" }));

    // Readiness probe
    app.MapGet("/ready", async (HealthCheckService healthCheckService) =>
    {
        var report = await healthCheckService.CheckHealthAsync();
        return report.Status == HealthStatus.Healthy
            ? Results.Ok(new { status = "ready" })
            : Results.StatusCode(503);
    });

    Console.WriteLine($" ### Health check server starting on port {port}");
    app.Run($"http://0.0.0.0:{port}");
}
