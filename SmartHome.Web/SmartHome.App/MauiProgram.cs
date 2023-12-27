using Microsoft.Extensions.Logging;
using Syncfusion.Blazor;

namespace SmartHome.App
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            try
            {
                Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(System.Environment.GetEnvironmentVariable("SyncfusionLicenseKey"));
                Console.WriteLine("Setting license key for syncfusion components");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
    		builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif

            builder.Services.AddSyncfusionBlazor(null);
            return builder.Build();
        }
    }
}
