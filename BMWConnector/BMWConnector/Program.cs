using System.Reflection;
using System.Text.Json;
using BMWConnector.Models;
using BMWConnector.Services;
using HeartbeatLib;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

KubernetesSecretStore store;
try
{
    store = new KubernetesSecretStore();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to connect to Kubernetes: {ex.Message}");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Ensure your kubeconfig is set up and the cluster is reachable:");
    Console.Error.WriteLine("  kubectl get nodes   # should list your k3s nodes");
    Console.Error.WriteLine();
    Console.Error.WriteLine("--> See BMWConnector/SETUP.md, Prerequisites section.");
    return;
}

// Parse flags:
//   --bootstrap BMW      bootstrap (authenticate) a single vehicle, then start its service
//   --vehicle BMW        run only a single vehicle's service (skip the other)
//   --save-credentials   allow a well-formed CLIENT_ID/GCID from the environment to be written
//                        into the credentials Secret. Off by default and ignored in the cluster —
//                        an unintended write-back is what destroyed the Secret on 2026-09-20.
string? bootstrapVehicle = null;
string? vehicleFilter    = null;
bool saveCredentials     = args.Contains("--save-credentials");

for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--bootstrap") bootstrapVehicle = args[i + 1];
    if (args[i] == "--vehicle")   vehicleFilter    = args[i + 1];
}

// --bootstrap implies --vehicle (only start the bootstrapped vehicle afterwards)
if (bootstrapVehicle != null) vehicleFilter = bootstrapVehicle;

string[] vehicleNames = vehicleFilter != null ? [vehicleFilter] : ["BMW", "Mini"];

var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
bool isInteractive = !Console.IsInputRedirected && !Console.IsOutputRedirected;

// Load configs and ensure tokens exist for each vehicle
var configs = new List<VehicleConfig>();
try
{
    foreach (var name in vehicleNames)
    {
        var config = await VehicleConfig.CreateAsync(name, store, saveCredentials, isInteractive);

        if (bootstrapVehicle == name)
        {
            await BootstrapService.RunAsync(config, store);
        }
        else if (!await new TokenService(config, store, loggerFactory.CreateLogger<TokenService>()).HasTokensAsync())
        {
            if (isInteractive)
            {
                Console.WriteLine($"[{name}] No tokens found. Starting authentication flow...");
                Console.WriteLine($"  After login, tokens are saved to the Kubernetes Secret and the pod can pick them up.");
                await BootstrapService.RunAsync(config, store);
            }
            else
            {
                Console.Error.WriteLine($"""
                    [{name}] No tokens found in Kubernetes Secret 'bmwconnector-{name.ToLower()}-tokens'.

                    Authentication must be performed from your local development machine (requires a browser).
                    The tokens are then stored in the Kubernetes Secret and this pod will pick them up automatically.

                    On your local machine, run:
                      cd BMWConnector/BMWConnector
                      dotnet run -- --bootstrap {name}

                    Then recreate this pod — do NOT use 'rollout restart', it patches the
                    Deployment template and ArgoCD reverts it (selfHeal: true):
                      kubectl -n smarthome delete pod -l app=bmwconnector

                    --> See BMWConnector/SETUP.md, Step 3 for the full authentication flow.
                    """);
                Environment.Exit(1);
            }
        }

        configs.Add(config);
    }
}
catch (MissingCredentialException ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.Exit(1);
    return;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Startup failed: {ex.Message}");
    Console.Error.WriteLine();
    Console.Error.WriteLine("--> See BMWConnector/SETUP.md for setup instructions.");
    Console.Error.WriteLine($"    Detail: {ex.GetType().Name}");
    return;
}

var builder = WebApplication.CreateBuilder(args);

// The BMW connection is rebuilt on every 50-minute token refresh. Readiness must ride through
// those seconds, so a vehicle stays "connected" for this long after its last live connection.
var staleAfter = TimeSpan.FromMinutes(
    double.TryParse(Environment.GetEnvironmentVariable("BMW_VEHICLE_STALE_MINUTES"), out double m) ? m : 5);

// BMW's refresh token expires two weeks after its last use. Half of that leaves a week to act
// on a stored token that has stopped being refreshed.
var tokenStaleAfter = TimeSpan.FromDays(
    double.TryParse(Environment.GetEnvironmentVariable("BMW_TOKEN_REFRESH_STALE_DAYS"), out double d) ? d : 7);

var healthRegistry = new HealthRegistry(loggerFactory.CreateLogger<HealthRegistry>(), staleAfter);
var tokenRefreshMonitor = new TokenRefreshMonitor(
    configs, store, loggerFactory.CreateLogger<TokenRefreshMonitor>(), tokenStaleAfter);

builder.Services.AddSingleton(healthRegistry);
builder.Services.AddSingleton<IHostedService>(tokenRefreshMonitor);

// Makes the connector visible on status/# next to the 17 ESP32 devices. Without it the 35-day
// outage of Aug 2026 was invisible on MQTT - the device page simply did not know the service
// existed. See Docs/Service-Heartbeat.md.
var serviceHeartbeat = new ServiceHeartbeat("BMWConnector", AssemblyVersion());
var heartbeatPublisher = new LocalBrokerPublisher(
    Environment.GetEnvironmentVariable("MQTT_BROKER") ?? "mosquitto.intern",
    int.Parse(Environment.GetEnvironmentVariable("MQTT_PORT") ?? "1883"),
    $"bmw-connector-heartbeat-{Environment.MachineName}",
    loggerFactory.CreateLogger<LocalBrokerPublisher>());

// Registered as well as captured, so the container closes the connection on shutdown.
builder.Services.AddSingleton(heartbeatPublisher);
builder.Services.AddSingleton<IHostedService>(sp => new ServiceHeartbeatWorker(
    serviceHeartbeat,
    // The readiness checks only. The token age warning is deliberately not among them - a
    // warned but working connector is ready, and the heartbeat must not claim otherwise.
    ct => sp.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(check => check.Tags.Contains("ready"), ct),
    (topic, payload, ct) => heartbeatPublisher.PublishRetainedAsync(topic, payload, ct),
    () => healthRegistry.LastPublishedAt,
    log: loggerFactory.CreateLogger<ServiceHeartbeatWorker>()));

builder.Services.AddHealthChecks()
    // Liveness answers one question only: is this process still serving? A dead BMW connection
    // must never restart the pod — a restart cannot renew an expired refresh token, it would
    // only bury a 35-day outage under a CrashLoop.
    .AddCheck("process-liveness", () => HealthCheckResult.Healthy("Process is serving."), tags: ["live"])
    .AddCheck("bmw-broker", healthRegistry.GetResult, tags: ["ready"])
    .AddCheck("stored-token-freshness", tokenRefreshMonitor.GetResult, tags: ["tokens"]);

foreach (var config in configs)
{
    var tokenService = new TokenService(config, store, loggerFactory.CreateLogger<TokenService>());
    // AddSingleton<IHostedService> instead of AddHostedService: the latter uses TryAddEnumerable
    // which silently skips duplicate implementation types, so the second vehicle would never start.
    builder.Services.AddSingleton<IHostedService>(sp =>
        new BmwCarDataService(config, tokenService, healthRegistry, sp.GetRequiredService<ILogger<BmwCarDataService>>()));
}

var app = builder.Build();

app.MapHealthChecks("/healthz/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = WriteReportAsync,
});

app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    // Degraded means one of two vehicles is gone. Left at its default it would answer 200 and
    // the probe would be satisfied — which is exactly how the Aug 2026 outage stayed invisible.
    ResultStatusCodes = ProbeStatusCodes.Readiness(),
    ResponseWriter = WriteReportAsync,
});

// Informational only — never wired to a Kubernetes probe. A stale stored token still works.
app.MapHealthChecks("/healthz/tokens", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("tokens"),
    ResultStatusCodes = ProbeStatusCodes.Informational(),
    ResponseWriter = WriteReportAsync,
});

app.Run();

// The image tag the pod is running, for the heartbeat. The CI passes it into the build as
// -p:Version; outside the pipeline there is no such number and "dev" is the honest answer.
static string AssemblyVersion()
{
    var version = Assembly.GetExecutingAssembly().GetName().Version;
    return version is null || version is { Major: 1, Minor: 0, Build: 0 }
        ? "dev"
        : $"{version.Major}.{version.Minor}.{version.Build}";
}

// Names which vehicle is missing instead of just "Degraded" — the status alone sent the
// last diagnosis down the wrong path.
static Task WriteReportAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    return context.Response.WriteAsync(JsonSerializer.Serialize(new
    {
        status = report.Status.ToString(),
        checks = report.Entries.Select(e => new
        {
            name        = e.Key,
            status      = e.Value.Status.ToString(),
            description = e.Value.Description,
        }),
    }));
}
