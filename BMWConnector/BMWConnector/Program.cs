using BMWConnector.Models;
using BMWConnector.Services;

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
//   --bootstrap BMW   bootstrap (authenticate) a single vehicle, then start its service
//   --vehicle BMW     run only a single vehicle's service (skip the other)
string? bootstrapVehicle = null;
string? vehicleFilter    = null;

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
        var config = await VehicleConfig.CreateAsync(name, store);

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
                      cd BMWConnector
                      dotnet run -- --bootstrap {name}

                    Then restart this pod:
                      kubectl -n smarthome rollout restart deployment/bmwconnector

                    --> See BMWConnector/SETUP.md, Step 3 for the full authentication flow.
                    """);
                Environment.Exit(1);
            }
        }

        configs.Add(config);
    }
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

var healthRegistry = new HealthRegistry();
builder.Services.AddSingleton(healthRegistry);
builder.Services.AddHealthChecks()
    .AddCheck("bmw-broker", () => healthRegistry.GetResult());

foreach (var config in configs)
{
    var tokenService = new TokenService(config, store, loggerFactory.CreateLogger<TokenService>());
    // AddSingleton<IHostedService> instead of AddHostedService: the latter uses TryAddEnumerable
    // which silently skips duplicate implementation types, so the second vehicle would never start.
    builder.Services.AddSingleton<IHostedService>(sp =>
        new BmwCarDataService(config, tokenService, healthRegistry, sp.GetRequiredService<ILogger<BmwCarDataService>>()));
}

var app = builder.Build();
app.MapHealthChecks("/healthz/live");
app.MapHealthChecks("/healthz/ready");
app.Run();
