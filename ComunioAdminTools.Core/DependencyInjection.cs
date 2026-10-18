using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Telegram.Bot;
using ComunioAdminTools.Core.Authentication;
using ComunioAdminTools.Core.Services;
using ComunioAdminTools.Core.Localization;

namespace ComunioAdminTools.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddComunioCore(this IServiceCollection services)
    {
        services.AddLogging();
        services.AddTelegramNotifications();
        services.TryAddSingleton<AppLocalization>();
        services.AddSingleton<IComunioTokenClient, ComunioTokenClient>();
        services.AddSingleton<ISessionManager, SessionManager>();
        services.AddSingleton<AuthService>();
        services.AddTransient<ComunioAuthHandler>();
        services.AddTransient<CashService>();
        services.AddTransient<PointsService>();
        services.AddTransient<IPointsService>(provider => provider.GetRequiredService<PointsService>());
        services.AddTransient<SquadService>();
        services.AddHttpClient("ComunioAuth", client =>
        {
            client.BaseAddress = new Uri("https://api.comunio.es/");
        });
        services.AddHttpClient("Comunio", client =>
        {
            client.BaseAddress = new Uri("https://api.comunio.es/");
        }).AddHttpMessageHandler<ComunioAuthHandler>();

        return services;
    }

    public static IServiceCollection AddTelegramNotifications(this IServiceCollection services)
    {
        services.AddHttpClient("TelegramNotifications", client => client.Timeout = TimeSpan.FromSeconds(5))
            .RemoveAllLoggers();
        services.AddSingleton<ITelegramBotClient>(provider => new TelegramBotClient(
            new TelegramBotClientOptions("8919714692:AAEwuADtFp7IP0ywRT5WAh9E6a9oAHdKIf0") { RetryCount = 0 },
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("TelegramNotifications")));
        services.AddSingleton<ILoginNotifier>(provider =>
            new TelegramLoginNotifier(provider.GetRequiredService<ITelegramBotClient>(), 7017183958));
        return services;
    }
}
