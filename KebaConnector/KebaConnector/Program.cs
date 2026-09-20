using HeartbeatLib;
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
KebaDeviceConnector kebaStellplatz;
KebaDeviceConnector kebaGarage;
// Set once the health check web app is up. Until then there is nothing to report and the
// service heartbeat waits - it must say what the registered checks say, not guess.
HealthCheckService? healthChecks = null;

// Display version information on startup
var versionInfo = VersionInfo.GetVersionInfo();
// Makes the service visible on status/# next to the 17 ESP32 devices - a stopped connector is
// invisible on MQTT otherwise. See Docs/Service-Heartbeat.md.
var serviceHeartbeat = new ServiceHeartbeat("KebaConnector", versionInfo.Version);
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
// The environment variable names stay as they are: renaming them means touching the
// Kubernetes secret of a running service, which is not part of this change.
var kebaStellplatzHost = configuration["KebaOutsideHost"] ?? "keba-stellplatz";
var kebaGarageHost = configuration["KebaGarageHost"] ?? "keba-garage";
var kebaPort = int.Parse(configuration["KebaPort"] ?? "7090");

Console.WriteLine($" ### Configuration: MQTT Broker={mqttBroker}:{mqttPort}, Health Check Port={healthCheckPort}");
Console.WriteLine($" ### Wallboxes: {LadeTopics.Stellplatz}={kebaStellplatzHost}:{kebaPort}, {LadeTopics.Garage}={kebaGarageHost}:{kebaPort}");
Console.WriteLine($" ### Service heartbeat topic: {serviceHeartbeat.Topic}");

// Start health check HTTP server in background
var healthCheckTask = Task.Run(() => StartHealthCheckServer(healthCheckPort));

Console.WriteLine($"  - Connecting to wallbox {LadeTopics.Stellplatz}...");
var ipsStellplatz = await Dns.GetHostAddressesAsync(kebaStellplatzHost);
if (ipsStellplatz is null || ipsStellplatz.Length == 0)
{
    Console.WriteLine($"    Could not resolve {kebaStellplatzHost}");
    return;
}
kebaStellplatz = new KebaDeviceConnector(ipsStellplatz[0], kebaPort);
Console.WriteLine("    ...Done");

Console.WriteLine($"  - Connecting to wallbox {LadeTopics.Garage}...");
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

await mqttClient.SubscribeToTopic(LadeTopics.LadestromAlle);

Console.WriteLine("    ...Done");

var timer = new Timer(Update, null, 2000, 5000);
// Separate from the wallbox cycle on purpose: the heartbeat has to keep going when reading a
// box fails, because that failure is exactly what it is supposed to report.
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
        var payload = serviceHeartbeat.BuildPayload(report, KebaConnectorHealthCheck.LastSuccessfulRead);
        await mqttClient.PublishAsync(serviceHeartbeat.Topic, payload,
            MqttQualityOfServiceLevel.AtLeastOnce, retain: true);
    }
    catch (Exception ex)
    {
        Console.WriteLine("Error publishing service heartbeat - " + ex.ToDetailedString());
    }
}

async void Update(object? state)
{
    try
    {
        await UpdateWallbox(kebaStellplatz, LadeTopics.Stellplatz);
        await UpdateWallbox(kebaGarage, LadeTopics.Garage);
    }
    catch (Exception ex)
    {
        Console.WriteLine("Error reading device data -" + ex.ToDetailedString());
    }
}

// One read cycle of a single wallbox: read it, follow its charging session, publish what it
// knows, and reconcile it with the setpoint of the controller.
async Task UpdateWallbox(KebaDeviceConnector wallbox, string name)
{
    var data = await wallbox.ReadDeviceData();
    if (data is null)
        return;

    Console.WriteLine($"Keba {name,-10}: {data.PlugStatus,-50} {data.CurrentChargingPower,10} W " +
        $"{data.EnergyCurrentChargingSession,15:#,##0} Wh {data.EnergyTotal,15:#,##0} Wh");
    wallbox.TrackSession(data, name);
    await PublishWallboxStatus(mqttClient, data, wallbox, name);
    KebaConnectorHealthCheck.UpdateLastSuccessfulRead();
    await wallbox.EnforceDesiredState(data, name);
}

async void MqttMessageReceived(object? sender, MqttMessageReceivedEventArgs e)
{
    string payload = e.Payload;
    var topic = e.Topic;
    var time = DateTime.Now;

    Console.WriteLine($"Received {(e.Retained ? "retained " : "")}message from {topic} at {time}: {payload}");

    LadestromKommando? kommando = null;
    try
    {
        kommando = JsonSerializer.Deserialize<LadestromKommando>(payload);
        if (kommando is null)
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

    if (LadeTopics.ZerlegeLadestromTopic(topic) is not (_, string wallboxName))
    {
        Console.WriteLine($"Invalid topic {topic}, expected [befehle/Laden/<Ort>/<Wallbox>/Ladestrom]");
        return;
    }

    if (kommando.Zeitpunkt == default)
    {
        // Without a Zeitpunkt the age of the setpoint is unknowable, and a retained replay is
        // indistinguishable from a live command. Refusing it is the safe direction: the
        // setpoint is ignored, the box keeps charging, and StaleReleaseAfter releases it.
        Console.WriteLine($"Charging command for {wallboxName} has no Zeitpunkt, ignoring it: {payload}");
        return;
    }

    try
    {
        if (wallboxName == LadeTopics.Garage)
        {
            await kebaGarage.UpdateDeviceDesiredCurrent(kommando.LadestromMa, kommando.Zeitpunkt);
        }
        else if (wallboxName == LadeTopics.Stellplatz)
        {
            await kebaStellplatz.UpdateDeviceDesiredCurrent(kommando.LadestromMa, kommando.Zeitpunkt);
        }
        else
        {
            Console.WriteLine($"Unknown wallbox {wallboxName}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error updating charging current for {wallboxName}: {ex.Message}");
    }

    return;
}

// Everything the box knows, published retained: a subscriber that connects between two read
// cycles gets the current state at once instead of waiting up to five seconds for it. The
// ChargingController relies on that — it must not command a box it has never heard from.
static async Task PublishWallboxStatus(MQTTClient.MQTTClient mqttClient, KebaData data,
    KebaDeviceConnector wallbox, string name)
{
    var status = new WallboxStatus()
    {
        Zeitpunkt = DateTimeOffset.UtcNow,
        PlugStatus = (int)data.PlugStatus,
        DeviceState = data.DeviceState,
        Freigegeben = data.ChargingEnabled,
        SitzungsId = wallbox.RunningSessionId,
        SitzungsBeginn = wallbox.SessionStart,
        SitzungsBeginnAusBoxZeit = wallbox.SessionStartFromBoxClock,
        EnergieSitzungWh = data.EnergyCurrentChargingSession,
        EnergieGesamtWh = data.EnergyTotal,
        Ladeleistung = data.CurrentChargingPower,
        StromPhase1Ma = data.CurrencyPhase1,
        StromPhase2Ma = data.CurrencyPhase2,
        StromPhase3Ma = data.CurrencyPhase3,
        AngebotenerStromMa = data.MaxCurrencyOfferedByChargingStation,
        SollstromMa = data.TargetCurrency,
    };
    await mqttClient.PublishAsync(LadeTopics.Status(name), JsonSerializer.Serialize(status),
        MqttQualityOfServiceLevel.AtLeastOnce, true);
}

void StartHealthCheckServer(int port)
{
    var builder = WebApplication.CreateBuilder();
    // Health check probes would otherwise rotate away the useful log lines within hours
    builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

    // Add health checks
    builder.Services.AddHealthChecks()
        .AddCheck<KebaConnectorHealthCheck>("keba_connector");

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
