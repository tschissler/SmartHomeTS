// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MQTTClient;
using Newtonsoft.Json;
using SharedContracts;
using ShellyConnector;
using ShellyConnector.DataContracts;
using SmartHomeHelpers.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

ConsoleHelpers.PrintInformation("ShellyConnector started");

// Build configuration from environment variables
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddEnvironmentVariables(prefix: "ShellySettings__")
    .Build();

// Read configuration values
var mqttBroker = configuration["MqttBroker"] ?? "smarthomepi2";
var mqttPort = int.Parse(configuration["MqttPort"] ?? "32004");
var healthCheckPort = int.Parse(configuration["HealthCheckPort"] ?? "8080");

ConsoleHelpers.PrintInformation($" ### Configuration: MQTT Broker={mqttBroker}:{mqttPort}, Health Check Port={healthCheckPort}");

// Start health check HTTP server in background
var healthCheckTask = Task.Run(() => StartHealthCheckServer(healthCheckPort));

ConsoleHelpers.PrintInformation(" ### Registering services");
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton<MQTTClient.MQTTClient>(provider => new MQTTClient.MQTTClient("ShellyConnector", mqttBroker, mqttPort));
    })
    .Build();

using var serviceScope = host.Services.CreateScope();
var services = serviceScope.ServiceProvider;

var mqttClient = services.GetRequiredService<MQTTClient.MQTTClient>();
ShellyConnectorHealthCheck.UpdateMqttConnectionStatus(true);

List<ShellyConnector.DataContracts.ShellyDevice> powerDevices, thermostatDevices;

powerDevices = ShellyDevices.GetPowerDevices();
thermostatDevices = ShellyDevices.GetThermostatDevices();

await TestDeviceConnection(powerDevices, thermostatDevices);

ConsoleHelpers.PrintInformation(" ### Subscribing to topics");
await mqttClient.SubscribeToTopic("commands/shelly/#");
mqttClient.OnMessageReceived += async (sender, e) =>
{
    ConsoleHelpers.PrintInformation($"  - Message received: {e.Topic}");
    if (string.IsNullOrEmpty(e.Payload))
    {
        ConsoleHelpers.PrintErrorMessage($"Empty payload for message on topic {e.Topic}");
        return;
    }

    var topicParts = e.Topic.Split("/");
    if (topicParts.Length < 2) return;
    var deviceName = topicParts[2];
    var device = powerDevices.FirstOrDefault(x =>
        string.Equals(x.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
    if (device != null)
    {
        var state = e.Payload;
        await ShellyConnector.ShellyConnector.SetRelay(device, state);
        return;
    }

    if (topicParts.Length < 3) return;
    var location = topicParts[2];
    deviceName = topicParts[3];
    device = thermostatDevices.FirstOrDefault(x =>
        string.Equals(x.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(x.Location.ToString(), location, StringComparison.OrdinalIgnoreCase));
    if (device != null)
    {
        ShellyThermostatData? currentData = null;
        var targetTemp = await ShellyConnector.ShellyConnector.SetTargetTemp(device, e.Payload);
        if (targetTemp is null) return;
        
        var timeout = TimeSpan.FromSeconds(30);
        var startTime = DateTime.Now;
        
        while (currentData is null || currentData.TargetTemperature != targetTemp)
        {
            if (DateTime.Now - startTime > timeout)
            {
                ConsoleHelpers.PrintErrorMessage($"Timeout waiting for target temperature confirmation on device {device.DeviceName}");
                break;
            }
            
            await Task.Delay(1000);
            currentData = await ShellyConnector.ShellyConnector.GetThermostatData(device);
            await SendUpdatedThermostatData(mqttClient, [device]);
        }
        return;
    }
};

ConsoleHelpers.PrintInformation(" ### Creating timer to read data from devices every second");
ConsoleHelpers.PrintInformation("     and send the data as MQTT messages");
var timerPowerDevices = new System.Timers.Timer(1000);
timerPowerDevices.Elapsed += async (sender, e) =>
{
    var tasks = powerDevices.Where(i => i.IsConnected).Select(async device =>
    {
        var powerData = await ShellyConnector.ShellyConnector.GetPowerData(device);
        if (powerData is null)
        {
            ConsoleHelpers.PrintInformation($"  --- Could not read meter data from device {device.DeviceName}");
            return;
        }
        if (!powerData.IsValid)
        {
            ConsoleHelpers.PrintInformation($"  --- Data from device {device.DeviceName} is invalid");
            return;
        }
        var jsonPayload = JsonConvert.SerializeObject(powerData);
        await mqttClient.PublishAsync($"data/electricity/{device.Location}/shelly/{device.DeviceName}", jsonPayload, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce, false);
        
        // Update health check on successful publish
        ShellyConnectorHealthCheck.UpdateLastSuccessfulRead();
    });

    await Task.WhenAll(tasks);
};
timerPowerDevices.Start();

var timerThermostatDevices = new System.Timers.Timer(60000);
timerThermostatDevices.Elapsed += async (sender, e) =>
{
    await SendUpdatedThermostatData(mqttClient, thermostatDevices);
};
timerThermostatDevices.Start();

var timerTestDeviceConnection = new System.Timers.Timer(300000);
timerTestDeviceConnection.Elapsed += async (sender, e) =>
{
    await TestDeviceConnection(powerDevices, thermostatDevices);
};
timerTestDeviceConnection.Start();

ConsoleHelpers.PrintInformation("");
ConsoleHelpers.PrintInformation(" ### Done");

// Run the host to keep the application running and processing events
await host.RunAsync();

static async Task TestDeviceConnection(List<ShellyConnector.DataContracts.ShellyDevice> powerDevices, List<ShellyConnector.DataContracts.ShellyDevice> thermostatDevices)
{
    ConsoleHelpers.PrintInformation(" ### Testing connection to all Power-Devices");

    foreach (var powerDevice in powerDevices)
    {
        var status = await ShellyConnector.ShellyConnector.GetPowerStaus(powerDevice);
        if (status is null)
            ConsoleHelpers.PrintInformation($"  --- Could not connect to powerDevice {powerDevice.DeviceName}");
        else
        {
            powerDevice.IsConnected = true;
            ConsoleHelpers.PrintInformation($"  - {powerDevice.DeviceName,-30} {"(" + powerDevice.IPAddress + ")",-20} {status.app + status.type,20} => success");
        }
    }

    ConsoleHelpers.PrintInformation(" ### Testing connection to all Thermostat-Devices");

    foreach (var thermostatDevice in thermostatDevices)
    {
        var status = await ShellyConnector.ShellyConnector.GetThermostatStaus(thermostatDevice);
        if (status is null)
            ConsoleHelpers.PrintInformation($"  --- Could not connect to thermostatDevice {thermostatDevice.DeviceName}");
        else
        {
            thermostatDevice.IsConnected = true;
            ConsoleHelpers.PrintInformation($"  - {thermostatDevice.DeviceName,-30} {"(" + thermostatDevice.IPAddress + ")",-20} => success");
        }
    }
}

static async Task SendUpdatedThermostatData(MQTTClient.MQTTClient mqttClient, List<ShellyConnector.DataContracts.ShellyDevice> thermostatDevices)
{
    var tasks = thermostatDevices.Where(i => i.IsConnected).Select(async device =>
    {
        var thermostatData = await ShellyConnector.ShellyConnector.GetThermostatData(device);
        if (thermostatData is null)
        {
            ConsoleHelpers.PrintInformation($"  --- Could not read meter data from device {device.DeviceName}");
            return;
        }
        var jsonPayload = JsonConvert.SerializeObject(thermostatData);
        await mqttClient.PublishAsync($"data/thermostat/{device.Location}/shelly/{device.DeviceName}", jsonPayload, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce, false);
    });

    await Task.WhenAll(tasks);
}

static void StartHealthCheckServer(int port)
{
    var builder = WebApplication.CreateBuilder();
    
    // Add health checks
    builder.Services.AddHealthChecks()
        .AddCheck<ShellyConnectorHealthCheck>("shelly_connector");
    
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
    
    ConsoleHelpers.PrintInformation($" ### Health check server starting on port {port}");
    app.Run($"http://0.0.0.0:{port}");
}