using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using SmartHome.Web;
using SmartHome.Web.Components;
using SmartHome.Web.Services;
using Syncfusion.Blazor;

var builder = WebApplication.CreateBuilder(args);

// Configure logging with categories
var loggerFactory = LoggerFactory.Create(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});
var logger = loggerFactory.CreateLogger("Program");

// Load configuration from appsettings.json and environment variables
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();

// Get configuration values with environment variable override support
string? syncfusionKey = configuration["SMARTHOME__SYNCFUSION_LICENSE_KEY"] ?? configuration["SMARTHOME:SYNCFUSION_LICENSE_KEY"];
string? mqttBroker = configuration["SMARTHOME__MQTT_BROKER"] ?? configuration["SMARTHOME:MQTT_BROKER"];
string? influxUrl = configuration["SMARTHOME__INFLUXDB_URL"] ?? configuration["SMARTHOME:INFLUXDB_URL"];
string? influxOrg = configuration["SMARTHOME__INFLUXDB_ORG"] ?? configuration["SMARTHOME:INFLUXDB_ORG"];
string? influxToken = configuration["SMARTHOME__INFLUXDB_TOKEN"] ?? configuration["SMARTHOME:INFLUXDB_TOKEN"];
string? environment = configuration["SMARTHOME__ENVIRONMENT"] ?? configuration["SMARTHOME:ENVIRONMENT"];

// Log version information
var versionInfo = VersionInfo.GetVersionInfo();
logger.LogInformation($"SmartHome.Web {versionInfo.GetDisplayString()}");
logger.LogInformation($"Starting SmartHome.Web in {environment} environment");
logger.LogInformation($"Using MQTT broker at {mqttBroker}");
logger.LogInformation($"Using InfluxDB at {influxUrl}");

try
{
    if (!string.IsNullOrEmpty(syncfusionKey))
    {
        Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(syncfusionKey);
        logger.LogInformation("Syncfusion license key registered successfully");
    }
    else
    {
        logger.LogWarning("Syncfusion license key not configured");
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "Error setting Syncfusion license key");
}

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddLocalization();
builder.Services.AddSyncfusionBlazor();
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<MqttService>();
//builder.Services.AddScoped<IChargingSessionService, ChargingSessionService>();

builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => 
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy()
    );

var app = builder.Build();
app.UseRequestLocalization(new RequestLocalizationOptions()
    .AddSupportedCultures(new[] { "de-DE" })
    .AddSupportedUICultures(new[] { "de-DE" }));

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

//app.UseStaticFiles();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapControllers();

// Add version endpoint
app.MapGet("/version", () => 
{
    var version = VersionInfo.GetVersionInfo();
    return Results.Ok(new
    {
        service = "SmartHome.Web",
        version = version.Version,
        buildNumber = version.BuildNumber,
        buildDate = version.BuildDate,
        gitCommit = version.GitCommit
    });
});

// Add health check endpoints
app.MapGet("/health", (MqttService mqttService) => 
{
    // Check if MQTT service is connected and healthy
    // You can add more checks here based on your requirements
    return Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
});

app.MapHealthChecks("/healthz/live", new HealthCheckOptions
{
    Predicate = (check) => check.Tags.Contains("live"),
});

app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
{
    Predicate = (check) => true,
});

logger.LogInformation("SmartHome.Web started successfully");

app.Run();
