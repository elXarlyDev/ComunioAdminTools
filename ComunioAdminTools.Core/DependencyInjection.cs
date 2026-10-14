using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ComunioAdminTools.Core.Authentication;
using ComunioAdminTools.Core.Services;
using ComunioAdminTools.Core.Localization;

namespace ComunioAdminTools.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddComunioCore(this IServiceCollection services, Action<ComunioAuthOptions>? configure = null)
    {
        var options = new ComunioAuthOptions();
        services.TryAddSingleton<AppLocalization>();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.TryAddSingleton<ITokenStore, MemoryTokenStore>();
        services.AddSingleton<ISessionManager, SessionManager>();
        services.AddSingleton<AuthService>();
        services.AddSingleton<IAccessTokenProvider>(sp => sp.GetRequiredService<AuthService>());
        services.AddTransient<ComunioAuthHandler>();
        services.AddTransient<CashService>();
        services.AddTransient<PointsService>();
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
