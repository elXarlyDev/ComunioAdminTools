using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ComunioAdminTools.Core.Authentication;
using ComunioAdminTools.Core.Services;
using ComunioAdminTools.Core.Localization;

namespace ComunioAdminTools.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddComunioCore(this IServiceCollection services)
    {
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
}
