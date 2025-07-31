using InfluxConnector;
using Microsoft.AspNetCore.DataProtection;
using SmartHome.Web.Components;
using SmartHome.Web.Services;
using Syncfusion.Blazor;

var builder = WebApplication.CreateBuilder(args);

// Add console logging
builder.Logging.AddConsole();

try
{
    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(Environment.GetEnvironmentVariable("SyncfusionLicenseKey"));
    Console.WriteLine("Setting license key for syncfusion components");
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
}

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddLocalization();
builder.Services.AddSyncfusionBlazor();
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<MqttService>();
builder.Services.AddScoped<IChargingSessionService, ChargingSessionService>();
builder.Services.AddScoped<IInfluxDBConnector>(provider =>
{
    var influxUrl = Environment.GetEnvironmentVariable("INFLUXDB_URL");
    var org = Environment.GetEnvironmentVariable("INFLUXDB_ORG");
    var token = Environment.GetEnvironmentVariable("INFLUXDB_TOKEN");
    if (string.IsNullOrEmpty(influxUrl) || string.IsNullOrEmpty(org) || string.IsNullOrEmpty(token))
    {
        throw new InvalidOperationException("InfluxDB connection details are not set in environment variables.");
    }
    return new InfluxDbConnector(influxUrl, org, token);
});

builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();

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

app.Run();
