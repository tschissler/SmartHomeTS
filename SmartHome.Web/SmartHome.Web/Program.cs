using SmartHome.Web.Components;
using SmartHome.Web.Components.Libs;
using Syncfusion.Blazor;

var builder = WebApplication.CreateBuilder(args);

try
{
    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(System.Environment.GetEnvironmentVariable("SyncfusionLicenseKey"));
    Console.WriteLine("Setting license key for syncfusion components");
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
}

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSyncfusionBlazor();
builder.Services.AddSingleton<MqttController>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();