using Microsoft.Extensions.Logging;

namespace ComunioAdminTools.Mobile
{
    using ComunioAdminTools.Core;

    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.Services.AddMauiBlazorWebView();
            builder.Services.AddComunioCore();

#if DEBUG
    		builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif

            var app = builder.Build();
            // Remove the unused access token persisted by earlier versions.
            SecureStorage.Default.Remove("comunio.access-token.v1");
            return app;
        }
    }
}
