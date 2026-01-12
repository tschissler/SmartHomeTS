using EnphaseConnector;
using MQTTClient;
using MQTTnet.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

// Display version information on startup
var versionInfo = VersionInfo.GetVersionInfo();
Console.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  EnphaseConnector Starting                                         ║");
Console.WriteLine("╠════════════════════════════════════════════════════════════════════╣");
Console.WriteLine($"║  {versionInfo.GetDisplayString().PadRight(66)}║");
Console.WriteLine("╚════════════════════════════════════════════════════════════════════╝");

// Build configuration from appsettings.json and environment variables
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables(prefix: "EnphaseSettings__")
    .Build();

// Bind configuration to settings object
var settings = new EnphaseSettings();
configuration.Bind(settings);



// Validate required settings
if (string.IsNullOrEmpty(settings.EnphaseUserName) || 
    string.IsNullOrEmpty(settings.EnphasePassword) ||
    string.IsNullOrEmpty(settings.EnvoyM1Serial) ||
    string.IsNullOrEmpty(settings.EnvoyM3Serial))
{
    Console.WriteLine("ERROR: Missing required configuration. Please check appsettings.json or environment variables.");
    Console.WriteLine("Required: EnphaseSettings__EnphaseUserName, EnphaseSettings__EnphasePassword, EnphaseSettings__EnvoyM1Serial, EnphaseSettings__EnvoyM3Serial");
    return;
}

// Start health check HTTP server in background
var healthCheckTask = Task.Run(() => StartHealthCheckServer(settings.HealthCheckPort));

var enphaseAuth = new EnphaseLocalAuth();
var tokenM1 = enphaseAuth.GetTokenAsync(settings.EnphaseUserName, settings.EnphasePassword, settings.EnvoyM1Serial).Result;
Thread.Sleep(1000);
var tokenM3 = enphaseAuth.GetTokenAsync(settings.EnphaseUserName, settings.EnphasePassword, settings.EnvoyM3Serial).Result;

using (var mqttClient = new MQTTClient.MQTTClient("EnphaseConnector", settings.MqttBroker, settings.MqttPort))
{
    Console.WriteLine("Connected to MQTT broker");
    EnphaseConnectorHealthCheck.UpdateMqttConnectionStatus(true);

    while (true)
    {
        var startTime = DateTime.Now;
        if (!mqttClient.IsConnected)
        {
            EnphaseConnectorHealthCheck.UpdateMqttConnectionStatus(false);
            await mqttClient.ConnectAsync();
            EnphaseConnectorHealthCheck.UpdateMqttConnectionStatus(true);
        }
        
        await ReadDataAndSendToMQTT(tokenM1, mqttClient, "envoym1");
        await ReadDataAndSendToMQTT(tokenM3, mqttClient, "envoym3");
        
        Thread.Sleep(settings.ReadIntervalMs - (int)-DateTime.Now.Subtract(startTime).TotalMilliseconds);
    }
}

static async Task ReadDataAndSendToMQTT(EnphaseLocalToken token, MQTTClient.MQTTClient mqttClient, string deviceName)
{
    var data = await new EnphaseLib().FetchDataAsync(token, deviceName);
    if (data != null)
    {
        await mqttClient.PublishAsync($"data/electricity/{deviceName}", JsonSerializer.Serialize(data), MqttQualityOfServiceLevel.AtLeastOnce, false);

        Console.WriteLine($"{DateTime.Now} --- Data for Device {deviceName} sent via MQTT -> Battery level: {data.BatteryLevel}\t| Production: {data.PowerFromPV}");
        
        // Update health check
        EnphaseConnectorHealthCheck.UpdateLastSuccessfulRead();
    }
    else
    {
        Console.WriteLine("Unable to read data from Envoy");
    }
}

static void StartHealthCheckServer(int port)
{
    var builder = WebApplication.CreateBuilder();
    
    // Add health checks
    builder.Services.AddHealthChecks()
        .AddCheck<EnphaseConnectorHealthCheck>("enphase_connector");
    
    var app = builder.Build();
    
    // Configure health check endpoints
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var result = JsonSerializer.Serialize(new
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
    
    Console.WriteLine($"Health check server starting on port {port}");
    app.Run($"http://0.0.0.0:{port}");
}