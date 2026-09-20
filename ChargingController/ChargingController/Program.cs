using ChargingController;
using MQTTnet;
using SharedContracts;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

IMqttClient mqttClient;
ChargingSituation currentChargingSituation = new ChargingSituation();
ChargingSettings currentChargingSettings = new ChargingSettings();
DateTime lastHeartbeatTime = DateTime.MinValue;
DateTime lastControlCycleTime = DateTime.MinValue;
bool powerDataReceived = false;
bool insideDataReceived = false;
bool outsideDataReceived = false;
DateTime firstPowerDataTime = DateTime.MinValue;
const int heartbeatIntervalSeconds = 60;
// Deciding before the wallbox readings have arrived would command 0 mA while a session is
// running and open the contactor. If a wallbox stays silent, control has to start anyway.
const int startupGraceSeconds = 30;
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
    if (!(insideDataReceived && outsideDataReceived)
        && DateTime.Now.Subtract(firstPowerDataTime).TotalSeconds < startupGraceSeconds)
    {
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
            await PublishChargingCommand("commands/charging/KebaGarage", command.InsideChargingCurrentmA);
            currentChargingSituation.InsideChargingLatestmA = command.InsideChargingCurrentmA;
            Console.WriteLine($"Sent charging command for KebaGarage: {command.InsideChargingCurrentmA} mA");
        }
        if (command.OutsideChargingCurrentmA != currentChargingSituation.OutsideChargingLatestmA)
        {
            await PublishChargingCommand("commands/charging/KebaOutside", command.OutsideChargingCurrentmA);
            currentChargingSituation.OutsideChargingLatestmA = command.OutsideChargingCurrentmA;
            Console.WriteLine($"Sent charging command for KebaOutside: {command.OutsideChargingCurrentmA} mA");
        }

        await PublishChargingSituation();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error running control cycle: {ex.Message}");
    }
}

async Task PublishChargingSituation()
{
    var payloadChargingSituation = JsonSerializer.Serialize(currentChargingSituation);
    await mqttClient.PublishAsync(new MqttApplicationMessageBuilder()
        .WithTopic("data/charging/situation")
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
            await PublishChargingCommand("commands/charging/KebaGarage", currentChargingSituation.InsideChargingLatestmA);
        }
        if (currentChargingSituation.OutsideChargingLatestmA >= 0)
        {
            await PublishChargingCommand("commands/charging/KebaOutside", currentChargingSituation.OutsideChargingLatestmA);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error publishing heartbeat: {ex.Message}");
    }
}

async Task PublishChargingCommand(string topic, int chargingCurrentmA)
{
    var payloadOut = JsonSerializer.Serialize(new ChargingSetData() { ChargingCurrent = chargingCurrentmA });
    await mqttClient.PublishAsync(new MqttApplicationMessageBuilder()
        .WithTopic(topic)
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
        if (topic == "config/charging/settings")
        {
            Console.WriteLine($"Received message from {topic} at {time}: {payload}");
            currentChargingSettings = JsonSerializer.Deserialize<ChargingSettings>(payload);
            // A setting changed by the user should take effect right away, not at the next cycle
            lastControlCycleTime = DateTime.MinValue;
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
            if (!powerDataReceived)
            {
                powerDataReceived = true;
                firstPowerDataTime = DateTime.Now;
            }
        }

        else if (topic == "data/charging/KebaGarage")
        {
            var kebaGarageData = JsonSerializer.Deserialize<ChargingGetData>(payload);
            currentChargingSituation.InsideCurrentChargingPower = kebaGarageData.CurrentChargingPower;
            currentChargingSituation.InsideConnected = kebaGarageData.CarIsPlugedIn;
            currentChargingSituation.InsideChargingCurrentSessionWh = kebaGarageData.EnergyCurrentChargingSession;
            insideDataReceived = true;
        }

        else if (topic == "data/charging/KebaOutside")
        {
            var kebaOutsideData = JsonSerializer.Deserialize<ChargingGetData>(payload);
            currentChargingSituation.OutsideCurrentChargingPower = kebaOutsideData.CurrentChargingPower;
            currentChargingSituation.OutsideConnected = kebaOutsideData.CarIsPlugedIn;
            currentChargingSituation.OutsideChargingCurrentSessionWh = kebaOutsideData.EnergyCurrentChargingSession;
            outsideDataReceived = true;
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

                await mqttClient.SubscribeAsync("data/charging/KebaGarage");
                await mqttClient.SubscribeAsync("data/charging/KebaOutside");
                await mqttClient.SubscribeAsync("data/charging/BMW");
                await mqttClient.SubscribeAsync("data/charging/VW");
                await mqttClient.SubscribeAsync("data/electricity/envoym3");
                await mqttClient.SubscribeAsync("config/charging/#");
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
