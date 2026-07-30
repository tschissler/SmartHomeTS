using HelpersLib;
using KebaConnector;
using MQTTnet;
using MQTTnet.Protocol;
using SharedContracts;
using System.Net;
using System.Text.Json;
using MQTTClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

MQTTClient.MQTTClient mqttClient;
KebaDeviceConnector kebaOutside;
KebaDeviceConnector kebaGarage;

// Display version information on startup
var versionInfo = VersionInfo.GetVersionInfo();
Console.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  KebaConnector Starting                                            ║");
Console.WriteLine("╠════════════════════════════════════════════════════════════════════╣");
Console.WriteLine($"║  {versionInfo.GetDisplayString().PadRight(66)}║");
Console.WriteLine("╚════════════════════════════════════════════════════════════════════╝");

// Build configuration from environment variables
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddEnvironmentVariables(prefix: "KebaConnectorSettings__")
    .Build();

// Read configuration values
var mqttBroker = configuration["MqttBroker"] ?? "smarthomepi2";
var mqttPort = int.Parse(configuration["MqttPort"] ?? "32004");
var healthCheckPort = int.Parse(configuration["HealthCheckPort"] ?? "8080");
var kebaOutsideHost = configuration["KebaOutsideHost"] ?? "keba-stellplatz";
var kebaGarageHost = configuration["KebaGarageHost"] ?? "keba-garage";
var kebaPort = int.Parse(configuration["KebaPort"] ?? "7090");

Console.WriteLine($" ### Configuration: MQTT Broker={mqttBroker}:{mqttPort}, Health Check Port={healthCheckPort}");
Console.WriteLine($" ### Keba devices: Outside={kebaOutsideHost}:{kebaPort}, Garage={kebaGarageHost}:{kebaPort}");

// Start health check HTTP server in background
var healthCheckTask = Task.Run(() => StartHealthCheckServer(healthCheckPort));

Console.WriteLine("  - Connecting to Keba Outside...");
var ipsOutside = await Dns.GetHostAddressesAsync(kebaOutsideHost);
if (ipsOutside is null || ipsOutside.Length == 0)
{
    Console.WriteLine($"    Could not resolve {kebaOutsideHost}");
    return;
}
kebaOutside = new KebaDeviceConnector(ipsOutside[0], kebaPort);
Console.WriteLine("    ...Done");

Console.WriteLine("  - Connecting to Keba Garage...");
var ipsGarage = await Dns.GetHostAddressesAsync(kebaGarageHost);
if (ipsGarage is null || ipsGarage.Length == 0)
{
    Console.WriteLine($"    Could not resolve {kebaGarageHost}");
    return;
}
kebaGarage = new KebaDeviceConnector(ipsGarage[0], kebaPort);
Console.WriteLine("    ...Done");

Console.WriteLine("  - Connecting to MQTT Broker");

mqttClient = new MQTTClient.MQTTClient("KebaConnector", mqttBroker, mqttPort);
Console.WriteLine($"    ClientId: {mqttClient.ClientId}");
KebaConnectorHealthCheck.UpdateMqttConnectionStatus(true);
mqttClient.OnConnectionStateChanged += (_, connected) => KebaConnectorHealthCheck.UpdateMqttConnectionStatus(connected);

mqttClient.OnMessageReceived += MqttMessageReceived;

await mqttClient.SubscribeToTopic("commands/charging/#");

Console.WriteLine("    ...Done");

var timer = new Timer(Update, null, 2000, 5000);

Thread.Sleep(Timeout.Infinite);


async void Update(object? state)
{
    try
    {
        var dataOutside = await kebaOutside.ReadDeviceData();
        if (dataOutside is not null)
        {
            Console.WriteLine($"Keba Outside: {dataOutside.PlugStatus,-50} {dataOutside.CurrentChargingPower,10} W {dataOutside.EnergyCurrentChargingSession,15:#,##0} Wh {dataOutside.EnergyTotal,15:#,##0} Wh");
            await SendDataAsMQTTMessage(mqttClient, dataOutside, "KebaOutside");
            KebaConnectorHealthCheck.UpdateLastSuccessfulRead();
            await kebaOutside.EnforceDesiredState(dataOutside, "Outside");
        }

        var dataGarage = await kebaGarage.ReadDeviceData();
        if (dataGarage is not null)
        {
            Console.WriteLine($"Keba Garage : {dataGarage.PlugStatus,-50} {dataGarage.CurrentChargingPower,10} W {dataGarage.EnergyCurrentChargingSession,15:#,##0} Wh {dataGarage.EnergyTotal,15:#,##0} Wh");
            await SendDataAsMQTTMessage(mqttClient, dataGarage, "KebaGarage");
            KebaConnectorHealthCheck.UpdateLastSuccessfulRead();
            await kebaGarage.EnforceDesiredState(dataGarage, "Garage");
        }

        var sessionGarage = await kebaGarage.CheckIfChargingSessionEnded("Garage");
        if (sessionGarage is not null)
        {
            Console.WriteLine($"--- Keba Garage Charging Session ended ---\n  SessionID {sessionGarage.SessionId,-5} {sessionGarage.StartTime,-30} {sessionGarage.EndTime,-30} {sessionGarage.EnergyOfChargingSession,15:#,##0} Wh {sessionGarage.TatalEnergyAtStart,15:#,##0} Wh");
            await SendChargingSessionAsMQTTMessage(mqttClient, sessionGarage, "KebaGarage");
        }

        var sessionOutside = await kebaOutside.CheckIfChargingSessionEnded("Stellplatz");
        if (sessionOutside is not null)
        {
            Console.WriteLine($"--- Keba Outside Charging Session ended ---\n  SessionID {sessionOutside.SessionId,-5} {sessionOutside.StartTime,-30} {sessionOutside.EndTime,-30} {sessionOutside.EnergyOfChargingSession,15:#,##0} Wh {sessionOutside.TatalEnergyAtStart,15:#,##0} Wh");
            await SendChargingSessionAsMQTTMessage(mqttClient, sessionOutside, "KebaOutside");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("Error reading device data -" + ex.ToDetailedString());
    }
}

async void MqttMessageReceived(object? sender, MqttMessageReceivedEventArgs e)
{
    string payload = e.Payload;
    var topic = e.Topic;
    var time = DateTime.Now;

    Console.WriteLine($"Received message from {topic} at {time}: {payload}");

    ChargingSetData? chargingSetData = null;
    try
    {
        chargingSetData = JsonSerializer.Deserialize<ChargingSetData>(payload);
        if (chargingSetData is null)
        {
            Console.WriteLine($"Failed to deserialize payload: {payload}");
            return;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to deserialize payload: {ex.Message}");
        return;
    }

    var topicParts = topic.Split("/");
    if (topicParts.Length < 3)
    {
        Console.WriteLine($"Invalid topic {topic}, topic needs to follow pattern [commands/Charging/<Device>]");
        return;
    }
    var kebaDevice = topic.Split("/")[2];
    try
    {
        if (kebaDevice.ToLower() == "kebagarage")
        {
            await kebaGarage.UpdateDeviceDesiredCurrent(chargingSetData.ChargingCurrent);
        }
        else if (kebaDevice.ToLower() == "kebaoutside")
        {
            await kebaOutside.UpdateDeviceDesiredCurrent(chargingSetData.ChargingCurrent);
        }
        else
        {
            Console.WriteLine($"Unknown device {kebaDevice}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error updating charging current for {kebaDevice}: {ex.Message}");
    }

    return;
}

static async Task SendDataAsMQTTMessage(MQTTClient.MQTTClient mqttClient, KebaData? data, string device)
{
    if (data is null)
        return;
    var messageData = new ChargingGetData()
    {
        CarIsPlugedIn = data.PlugStatus == PlugStatus.CablePluggedInChargingStationAndVehicleAndLocked,
        CurrentChargingPower = data.CurrentChargingPower,
        EnergyCurrentChargingSession = data.EnergyCurrentChargingSession,
        EnergyTotal = data.EnergyTotal
    };
    await mqttClient.PublishAsync($"data/charging/{device}", JsonSerializer.Serialize(messageData), MqttQualityOfServiceLevel.AtMostOnce, false);
}

static async Task SendChargingSessionAsMQTTMessage(MQTTClient.MQTTClient mqttClient, ChargingSession? data, string device)
{
    if (data is null)
        return;
    await mqttClient.PublishAsync($"data/charging/{device}_ChargingSessionEnded", JsonSerializer.Serialize(data), MqttQualityOfServiceLevel.AtMostOnce, false);
}

static void StartHealthCheckServer(int port)
{
    var builder = WebApplication.CreateBuilder();
    // Health check probes would otherwise rotate away the useful log lines within hours
    builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

    // Add health checks
    builder.Services.AddHealthChecks()
        .AddCheck<KebaConnectorHealthCheck>("keba_connector");

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
