using ChargingController;
using MQTTnet;
using SharedContracts;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

IMqttClient mqttClient;
ChargingSituation currentChargingSituation = new ChargingSituation();
ChargingSettings currentChargingSettings = new ChargingSettings();
DateTime lastOutsideSetTime = DateTime.MinValue;
DateTime lastInsideSetTime = DateTime.MinValue;
const int minimumSetIntervalSeconds = 10;

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

Console.WriteLine($" ### Configuration: MQTT Broker={mqttBroker}:{mqttPort}, Health Check Port={healthCheckPort}");

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
    await Task.Delay(1000);
}


async Task MqttMessageReceived(MqttApplicationMessageReceivedEventArgs args)
{
    string payload = Encoding.UTF8.GetString(args.ApplicationMessage.Payload);
    var topic = args.ApplicationMessage.Topic;
    var time = DateTime.Now;

    try
    {
        int value;
        if (topic == "config/charging/settings")
        {
            Console.WriteLine($"Received message from {topic} at {time}: {payload}");
            currentChargingSettings = JsonSerializer.Deserialize<ChargingSettings>(payload);
        }

        else if (topic == "data/electricity/envoym3")
        {
            var pvData = JsonSerializer.Deserialize<EnphaseData>(payload);
            currentChargingSituation.PowerFromGrid = (int)(pvData.PowerFromGrid / 1000);
            currentChargingSituation.BatteryLevel = pvData.BatteryLevel;
            currentChargingSituation.PowerFromBattery = (int)(pvData.PowerFromBattery / 1000);
            currentChargingSituation.PowerFromPV = (int)(pvData.PowerFromPV / 1000);
            currentChargingSituation.HouseConsumptionPower = (int)(pvData.PowerToHouse / 1000);
        }

        else if (topic == "data/charging/KebaGarage")
        {
            var kebaGarageData = JsonSerializer.Deserialize<ChargingGetData>(payload);
            currentChargingSituation.InsideCurrentChargingPower = kebaGarageData.CurrentChargingPower;
            currentChargingSituation.InsideConnected = kebaGarageData.CarIsPlugedIn;
            currentChargingSituation.InsideChargingCurrentSessionWh = kebaGarageData.EnergyCurrentChargingSession;
        }

        else if (topic == "data/charging/KebaOutside")
        {
            var kebaOutsideData = JsonSerializer.Deserialize<ChargingGetData>(payload);
            currentChargingSituation.OutsideCurrentChargingPower = kebaOutsideData.CurrentChargingPower;
            currentChargingSituation.OutsideConnected = kebaOutsideData.CarIsPlugedIn;
            currentChargingSituation.OutsideChargingCurrentSessionWh = kebaOutsideData.EnergyCurrentChargingSession;
        }
        else
        {
            Console.WriteLine($"Unknown topic: {topic}");
        }

        // Update health check on successful message processing
        ChargingControllerHealthCheck.UpdateLastSuccessfulRead();

        var chargingResult = await ChargingDecisionsMaker.CalculateChargingData(currentChargingSituation, currentChargingSettings);
        if (chargingResult.InsideChargingCurrentmA != currentChargingSituation.InsideChargingLatestmA)
        {
            if (DateTime.Now.Subtract(lastInsideSetTime).Seconds > minimumSetIntervalSeconds)
            {
                var payloadOut = JsonSerializer.Serialize(new ChargingSetData() { ChargingCurrent = chargingResult.InsideChargingCurrentmA });
                await mqttClient.PublishAsync(new MqttApplicationMessageBuilder()
                    .WithTopic("commands/charging/KebaGarage")
                    .WithPayload(payloadOut)
                    .WithRetainFlag()
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build());
                Console.WriteLine($"Sent MQTT message with payload; {payloadOut}");
                currentChargingSituation.InsideChargingLatestmA = chargingResult.InsideChargingCurrentmA;
            }
            else
            {
                Console.WriteLine($"Inside charging current was set too recently. Skipping.");
            }
        }
        if (chargingResult.OutsideChargingCurrentmA != currentChargingSituation.OutsideCurrentChargingPower)
        {
            if (DateTime.Now.Subtract(lastOutsideSetTime).Seconds > minimumSetIntervalSeconds)
            {
                var payloadOut = JsonSerializer.Serialize(new ChargingSetData() { ChargingCurrent = chargingResult.OutsideChargingCurrentmA });
                await mqttClient.PublishAsync(new MqttApplicationMessageBuilder()
                    .WithTopic("commands/charging/KebaOutside")
                    .WithPayload(payloadOut)
                    .WithRetainFlag()
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build());
                Console.WriteLine($"Sent MQTT message with payload; {payloadOut}");
                currentChargingSituation.OutsideChargingLatestmA = chargingResult.OutsideChargingCurrentmA;
            }
            else
            {
                Console.WriteLine($"Outside charging current was set too recently. Skipping.");
            }
        }

        var payloadChargingSituation = JsonSerializer.Serialize(currentChargingSituation);
        await mqttClient.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic("data/charging/situation")
            .WithPayload(payloadChargingSituation)
            .WithRetainFlag()
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .Build());
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error processing MQTT message: {ex.Message}");
    }
}

async Task MQTTConnectAsync()
{
    var mqttOptions = new MqttClientOptionsBuilder()
        .WithTcpServer(mqttBroker, mqttPort)
        .WithClientId("Smarthome.ChargingController")
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
