using Microsoft.Extensions.Diagnostics.HealthChecks;
using MQTTClient;
using MQTTnet.Protocol;
using RulesEngine;
using RulesEngine.Rules;

// Display version information on startup
var versionInfo = VersionInfo.GetVersionInfo();
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

// Kühlbetriebs-Regelung (Taupunktschutz FBHZ)
var coolingStatusValues = (configuration["CoolingStatusValues"] ?? "2")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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

Console.WriteLine($" ### Configuration: MQTT Broker={mqttBroker}:{mqttPort}, Health Check Port={healthCheckPort}");
Console.WriteLine($" ### FA_Status topic: {faStatusTopic}");
Console.WriteLine($" ### Mixer command topics: {string.Join(", ", mixerCommandTopics)}");
Console.WriteLine($" ### Max status age: {maxStatusAgeMinutes} min, evaluation interval: {evaluationIntervalSeconds} s");
Console.WriteLine($" ### Cooling regulation: FA_Status in [{string.Join(", ", coolingStatusValues)}], target {coolingFlowTargetTemperature}°C ±{coolingFlowDeadbandKelvin}K, {pulseSecondsPerKelvin} s/K ({minPulseSeconds}-{maxPulseSeconds} s) -> {fbhzMixerCommandTopic}");
Console.WriteLine($" ### Config topic: {configTopic} (retained JSON, overrides CoolingFlowTargetTemperature at runtime)");

// FA_Status 4 = Warmwasserladung der Hoval Belaria — fixed by the heat pump, not configuration
string[] warmWaterStatusValues = ["4"];
var mixerRule = new MixerPositionRule(warmWaterStatusValues, TimeSpan.FromMinutes(maxStatusAgeMinutes));
var coolingRule = new CoolingFlowTemperatureRule(
    coolingStatusValues, TimeSpan.FromMinutes(maxStatusAgeMinutes),
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
Console.WriteLine("    ...Done");

await mqttClient.PublishAsync("meta/RulesEngine/version", versionInfo.Version, MqttQualityOfServiceLevel.AtLeastOnce, true);

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
}, null, evaluationIntervalSeconds * 1000, evaluationIntervalSeconds * 1000);

Thread.Sleep(Timeout.Infinite);

void MqttMessageReceived(object? sender, MqttMessageReceivedEventArgs e)
{
    if (e.Topic == configTopic)
    {
        ApplyConfig(e.Payload);
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
                lastFaStatus, Age(lastFaStatusTime, now),
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

static TimeSpan Age(DateTimeOffset timestamp, DateTimeOffset now)
    => timestamp == DateTimeOffset.MinValue ? TimeSpan.MaxValue : now - timestamp;

static string PositionToPayload(MixerPosition position)
    => position == MixerPosition.Closed ? "close" : "open";

static void StartHealthCheckServer(int port)
{
    var builder = WebApplication.CreateBuilder();
    // Health check probes would otherwise rotate away the useful log lines within hours
    builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

    // Add health checks
    builder.Services.AddHealthChecks()
        .AddCheck<RulesEngineHealthCheck>("rules_engine");

    var app = builder.Build();

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
