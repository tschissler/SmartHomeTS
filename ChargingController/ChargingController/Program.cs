using ChargingController;
using MQTTnet;
using SharedContracts;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

IMqttClient mqttClient;
ChargingSituation currentChargingSituation = new ChargingSituation();
ChargingSettings? currentChargingSettings = null;
DateTime lastHeartbeatTime = DateTime.MinValue;
DateTime lastControlCycleTime = DateTime.MinValue;
// Waiting is a normal state at startup and a fault if it lasts. Saying once per minute what
// is missing is what tells the two apart in the log.
DateTime lastWaitingLogTime = DateTime.MinValue;
bool powerDataReceived = false;
bool insideDataReceived = false;
bool outsideDataReceived = false;
const int heartbeatIntervalSeconds = 60;
// The decision runs on a fixed cycle instead of on every incoming message: the Enphase
// connector publishes once per second, which is far faster than the dead time of the
// wallbox and the car, and reacting that fast is what makes the loop oscillate.
const int controlCycleSeconds = 5;

// All tuning parameters of the control loop live in this options object
var stabilizerOptions = new ChargingStabilizerOptions();
var chargingSmoother = new ChargingSmoother(stabilizerOptions.SmoothingTimeConstant);
var chargingStabilizer = new ChargingStabilizer(stabilizerOptions);

// Display version information on startup
var versionInfo = VersionInfo.GetVersionInfo();
Console.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  ChargingController Starting                                       ║");
Console.WriteLine("╠════════════════════════════════════════════════════════════════════╣");
Console.WriteLine($"║  {versionInfo.GetDisplayString().PadRight(66)}║");
Console.WriteLine("╚════════════════════════════════════════════════════════════════════╝");

// Build configuration from environment variables
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddEnvironmentVariables(prefix: "ChargingControllerSettings__")
    .Build();

// Read configuration values
var mqttBroker = configuration["MqttBroker"] ?? "smarthomepi2";
var mqttPort = int.Parse(configuration["MqttPort"] ?? "32004");
var healthCheckPort = int.Parse(configuration["HealthCheckPort"] ?? "8080");
// The client id has to be unique per instance. During a rolling update the old and the new
// pod are connected at the same time, and with a shared id the broker kicks them alternately
// (DISCONNECT with Reason code=SessionTakenOver), so both keep losing their subscriptions.
// The machine name is the pod name inside Kubernetes and stays stable across reconnects.
// Same scheme as the shared MQTTClient library uses for the connectors.
var mqttClientId = $"Smarthome.ChargingController_{Environment.MachineName}";

Console.WriteLine($" ### Configuration: MQTT Broker={mqttBroker}:{mqttPort}, Health Check Port={healthCheckPort}");
Console.WriteLine($" ### MQTT Client Id: {mqttClientId}");

// Start health check HTTP server in background
var healthCheckTask = Task.Run(() => StartHealthCheckServer(healthCheckPort));

Console.WriteLine("  - Connecting to MQTT Broker");

var factory = new MqttClientFactory();
mqttClient = factory.CreateMqttClient();
await MQTTConnectAsync();

Console.WriteLine("    ...Done");
Console.WriteLine("Waiting for first data to start calculation");

while (true)
{
    if (!mqttClient.IsConnected)
    {
        Console.WriteLine("MQTT connection lost. Reconnecting...");
        ChargingControllerHealthCheck.UpdateMqttConnectionStatus(false);
        await MQTTConnectAsync();
    }
    if (DateTime.Now.Subtract(lastControlCycleTime).TotalSeconds >= controlCycleSeconds)
    {
        lastControlCycleTime = DateTime.Now;
        await RunControlCycle();
    }
    if (DateTime.Now.Subtract(lastHeartbeatTime).TotalSeconds >= heartbeatIntervalSeconds)
    {
        lastHeartbeatTime = DateTime.Now;
        await PublishHeartbeat();
    }
    await Task.Delay(1000);
}

// One pass of the control loop: filter the readings, calculate the ideal charging power,
// apply the delays that protect the wallbox contactor, and publish the result.
async Task RunControlCycle()
{
    if (!powerDataReceived || !mqttClient.IsConnected)
        return;
    // No command for a box this controller has never heard from, and none at all without
    // settings. Deciding blind means commanding 0 mA — which opens the contactor of a box
    // that may well be charging — and a setpoint of 0 is published retained, so it switches
    // the box off again on every reconnect until something overwrites it. Both wallbox status
    // and settings are retained topics: whatever exists is delivered on subscribe, within
    // milliseconds. What does not arrive is genuinely absent, and a box that is not there
    // cannot be controlled anyway. Staying silent hands the box to the emergency release of
    // the KebaConnector, which is exactly what that release is for.
    if (!insideDataReceived || !outsideDataReceived || currentChargingSettings is null)
    {
        LogWhyTheLoopIsWaiting();
        return;
    }

    try
    {
        var now = DateTimeOffset.Now;

        // The decision works on filtered readings, while currentChargingSituation keeps the
        // raw values: they are published for the UI and are the input of the next filter step.
        var smoothedSituation = chargingSmoother.Smooth(currentChargingSituation, now);
        var targetResult = await ChargingDecisionsMaker.CalculateChargingData(smoothedSituation, currentChargingSettings);
        // The battery level hysteresis keeps its state in the situation, so it has to survive the copy
        currentChargingSituation.BatterySupportedChargingActive = smoothedSituation.BatterySupportedChargingActive;
        currentChargingSituation.AvailableChargingPowerWatts = ChargingDecisionsMaker.CalculateRawAvailablePower(smoothedSituation);

        var command = chargingStabilizer.Stabilize(targetResult, currentChargingSituation, currentChargingSettings, now);

        if (command.InsideChargingCurrentmA != currentChargingSituation.InsideChargingLatestmA)
        {
            await PublishChargingCommand(LadeTopics.Garage, command.InsideChargingCurrentmA);
            currentChargingSituation.InsideChargingLatestmA = command.InsideChargingCurrentmA;
            Console.WriteLine($"Sent charging command for {LadeTopics.Garage}: {command.InsideChargingCurrentmA} mA");
        }
        if (command.OutsideChargingCurrentmA != currentChargingSituation.OutsideChargingLatestmA)
        {
            await PublishChargingCommand(LadeTopics.Stellplatz, command.OutsideChargingCurrentmA);
            currentChargingSituation.OutsideChargingLatestmA = command.OutsideChargingCurrentmA;
            Console.WriteLine($"Sent charging command for {LadeTopics.Stellplatz}: {command.OutsideChargingCurrentmA} mA");
        }

        await PublishChargingSituation();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error running control cycle: {ex.Message}");
    }
}

void LogWhyTheLoopIsWaiting()
{
    if (DateTime.Now.Subtract(lastWaitingLogTime).TotalSeconds < 60)
        return;
    lastWaitingLogTime = DateTime.Now;

    var missing = new List<string>();
    if (!insideDataReceived) missing.Add($"status of {LadeTopics.Garage}");
    if (!outsideDataReceived) missing.Add($"status of {LadeTopics.Stellplatz}");
    if (currentChargingSettings is null) missing.Add(LadeTopics.Einstellungen);
    Console.WriteLine($"Not controlling yet, still missing: {string.Join(", ", missing)}. " +
        "The wallboxes keep whatever they are set to; the KebaConnector releases them to full " +
        "current once the setpoint is stale.");
}

async Task PublishChargingSituation()
{
    currentChargingSituation.Zeitpunkt = DateTimeOffset.UtcNow;
    var payloadChargingSituation = JsonSerializer.Serialize(currentChargingSituation);
    await mqttClient.PublishAsync(new MqttApplicationMessageBuilder()
        .WithTopic(LadeTopics.Situation)
        .WithPayload(payloadChargingSituation)
        .WithRetainFlag()
        .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
        .Build());
}

// The KebaConnector only enforces a setpoint while it is fresh (wallboxes fall back to
// autonomous full-power charging when the controller is silent), so the latest sent
// values are re-published periodically as a heartbeat.
async Task PublishHeartbeat()
{
    try
    {
        if (!mqttClient.IsConnected)
            return;
        if (currentChargingSituation.InsideChargingLatestmA >= 0)
        {
            await PublishChargingCommand(LadeTopics.Garage, currentChargingSituation.InsideChargingLatestmA);
        }
        if (currentChargingSituation.OutsideChargingLatestmA >= 0)
        {
            await PublishChargingCommand(LadeTopics.Stellplatz, currentChargingSituation.OutsideChargingLatestmA);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error publishing heartbeat: {ex.Message}");
    }
}

// The command carries the time it was decided. It is published retained, so the broker
// replays it to the KebaConnector on every subscribe — only this timestamp lets the connector
// tell a live setpoint from an arbitrarily old one and release the box when control is gone.
async Task PublishChargingCommand(string wallbox, int chargingCurrentmA)
{
    var payloadOut = JsonSerializer.Serialize(new LadestromKommando()
    {
        Zeitpunkt = DateTimeOffset.UtcNow,
        LadestromMa = chargingCurrentmA,
    });
    await mqttClient.PublishAsync(new MqttApplicationMessageBuilder()
        .WithTopic(LadeTopics.Ladestrom(wallbox))
        .WithPayload(payloadOut)
        .WithRetainFlag()
        .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
        .Build());
}


// Message handling only updates the situation, the control cycle acts on it
Task MqttMessageReceived(MqttApplicationMessageReceivedEventArgs args)
{
    string payload = Encoding.UTF8.GetString(args.ApplicationMessage.Payload);
    var topic = args.ApplicationMessage.Topic;
    var time = DateTime.Now;

    try
    {
        if (topic == LadeTopics.Einstellungen)
        {
            Console.WriteLine($"Received message from {topic} at {time}: {payload}");
            var settings = JsonSerializer.Deserialize<ChargingSettings>(payload);
            if (settings is not null)
            {
                currentChargingSettings = settings;
                // A setting changed by the user should take effect right away, not at the next cycle
                lastControlCycleTime = DateTime.MinValue;
            }
        }

        else if (topic == "data/electricity/envoym3")
        {
            var pvData = JsonSerializer.Deserialize<EnphaseData>(payload);
            currentChargingSituation.PowerFromGrid = (int)(pvData.PowerFromGrid / 1000);
            if (pvData.BatteryLevel.HasValue)
                currentChargingSituation.BatteryLevel = pvData.BatteryLevel.Value;
            currentChargingSituation.PowerFromBattery = (int)(pvData.PowerFromBattery / 1000);
            currentChargingSituation.PowerFromPV = (int)(pvData.PowerFromPV / 1000);
            currentChargingSituation.HouseConsumptionPower = (int)(pvData.PowerToHouse / 1000);
            powerDataReceived = true;
        }

        else if (LadeTopics.ZerlegeStatusTopic(topic) is (_, string wallbox))
        {
            var status = JsonSerializer.Deserialize<WallboxStatus>(payload);
            if (status is null)
            {
                Console.WriteLine($"Failed to deserialize wallbox status from {topic}: {payload}");
                return Task.CompletedTask;
            }
            var connected = status.FahrzeugVerbunden;

            if (wallbox == LadeTopics.Garage)
            {
                currentChargingSituation.InsideCurrentChargingPower = status.Ladeleistung;
                currentChargingSituation.InsideConnected = connected;
                currentChargingSituation.InsideChargingCurrentSessionWh = status.EnergieSitzungWh;
                insideDataReceived = true;
            }
            else if (wallbox == LadeTopics.Stellplatz)
            {
                currentChargingSituation.OutsideCurrentChargingPower = status.Ladeleistung;
                currentChargingSituation.OutsideConnected = connected;
                currentChargingSituation.OutsideChargingCurrentSessionWh = status.EnergieSitzungWh;
                outsideDataReceived = true;
            }
            else
            {
                Console.WriteLine($"Status of unknown wallbox {wallbox}, ignoring it");
            }
        }
        else
        {
            Console.WriteLine($"Unknown topic: {topic}");
        }

        // Update health check on successful message processing
        ChargingControllerHealthCheck.UpdateLastSuccessfulRead();

        // The charging decision is not taken here: it runs on the fixed control cycle, so the
        // 1 Hz updates of the PV connector cannot drive the loop faster than the wallbox and
        // the car can follow.
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error processing MQTT message: {ex.Message}");
    }

    return Task.CompletedTask;
}

async Task MQTTConnectAsync()
{
    var mqttOptions = new MqttClientOptionsBuilder()
        .WithTcpServer(mqttBroker, mqttPort)
        .WithClientId(mqttClientId)
        .WithKeepAlivePeriod(new TimeSpan(0, 1, 0,0))
        .Build();

    while (true)
    {
        if (mqttClient.IsConnected)
            break;
        try
        {
            await mqttClient.ConnectAsync(mqttOptions);
            if (mqttClient.IsConnected)
            {
                Console.WriteLine("Connected to MQTT Broker.");
                ChargingControllerHealthCheck.UpdateMqttConnectionStatus(true);

                mqttClient.ApplicationMessageReceivedAsync -= MqttMessageReceived;
                mqttClient.ApplicationMessageReceivedAsync += MqttMessageReceived;

                // One wildcard instead of a list of boxes: the levels of the convention put
                // every wallbox at the same depth, so a new box needs no change here.
                await mqttClient.SubscribeAsync(LadeTopics.StatusAlle);
                await mqttClient.SubscribeAsync("data/electricity/envoym3");
                await mqttClient.SubscribeAsync(LadeTopics.Einstellungen);
                break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error connecting to MQTT Broker: {ex.Message}");
            await Task.Delay(5000);
        }
    }
}

static void StartHealthCheckServer(int port)
{
    var builder = WebApplication.CreateBuilder();
    // Health check probes would otherwise rotate away the useful log lines within hours
    builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

    // Add health checks
    builder.Services.AddHealthChecks()
        .AddCheck<ChargingControllerHealthCheck>("charging_controller");

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
