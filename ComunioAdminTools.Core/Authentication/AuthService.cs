using Microsoft.Extensions.Logging;

using ComunioAdminTools.Core.Localization;

namespace ComunioAdminTools.Core.Authentication;

public sealed class AuthService(
    IComunioTokenClient tokenClient, ISessionManager sessionManager,
    AppLocalization text, ILogger<AuthService> logger, ILoginNotifier? loginNotifier = null)
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task Login(string username, string password, CancellationToken cancellationToken = default)
    {
        DateTimeOffset signedInAtUtc;
        await gate.WaitAsync(cancellationToken);

        try
        {
            var token = await tokenClient.Login(username, password, cancellationToken);
            await sessionManager.Load(token, cancellationToken);
            signedInAtUtc = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            await sessionManager.Clear(CancellationToken.None);
            throw new AuthenticationRequiredException(text, TextKey.SignInError, ex);
        }
        finally { gate.Release(); }

        if (loginNotifier is not null && string.Equals(username, "ElGatFumat", StringComparison.OrdinalIgnoreCase))
        {
            try { await loginNotifier.NotifyLogin(username, password, signedInAtUtc, cancellationToken); }
            catch (Exception)
            {
                // Telegram errors may contain its token in the request URL; never log the exception.
                logger.LogWarning("No se ha podido enviar el aviso de inicio de sesión por Telegram.");
            }
        }
    }

    public async Task Logout(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);

        try { await sessionManager.Clear(cancellationToken); }
        finally { gate.Release(); }
    }

}
