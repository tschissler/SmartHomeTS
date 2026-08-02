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
    ?? "commands/MixerController/M1/Mischer1,commands/MixerController/M1/Mischer2")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var maxStatusAgeMinutes = int.Parse(configuration["MaxStatusAgeMinutes"] ?? "15");
var evaluationIntervalSeconds = int.Parse(configuration["EvaluationIntervalSeconds"] ?? "60");

Console.WriteLine($" ### Configuration: MQTT Broker={mqttBroker}:{mqttPort}, Health Check Port={healthCheckPort}");
Console.WriteLine($" ### FA_Status topic: {faStatusTopic}");
Console.WriteLine($" ### Mixer command topics: {string.Join(", ", mixerCommandTopics)}");
Console.WriteLine($" ### Max status age: {maxStatusAgeMinutes} min, evaluation interval: {evaluationIntervalSeconds} s");

// FA_Status 4 = Warmwasserladung der Hoval Belaria — fixed by the heat pump, not configuration
string[] warmWaterStatusValues = ["4"];
var mixerRule = new MixerPositionRule(warmWaterStatusValues, TimeSpan.FromMinutes(maxStatusAgeMinutes));

string? lastFaStatus = null;
DateTimeOffset lastFaStatusTime = DateTimeOffset.MinValue;
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
Console.WriteLine("    ...Done");

await mqttClient.PublishAsync("meta/RulesEngine/version", versionInfo.Version, MqttQualityOfServiceLevel.AtLeastOnce, true);

// Publish once at startup to establish the retained command, then only when
// the decision changes. The periodic evaluation exists so the staleness rule
// (MaxStatusAge) also fires when the CAN gateway stops sending — decisions
// must not depend on messages that are no longer arriving.
var initialPublishTimer = new Timer(_ => EvaluateAndPublish("startup", force: true), null, 2000, Timeout.Infinite);
var evaluationTimer = new Timer(_ => EvaluateAndPublish("periodic evaluation"), null, evaluationIntervalSeconds * 1000, evaluationIntervalSeconds * 1000);

Thread.Sleep(Timeout.Infinite);

void MqttMessageReceived(object? sender, MqttMessageReceivedEventArgs e)
{
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
            var statusAge = lastFaStatusTime == DateTimeOffset.MinValue
                ? TimeSpan.MaxValue
                : DateTimeOffset.UtcNow - lastFaStatusTime;
            position = mixerRule.Evaluate(lastFaStatus, statusAge);

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
