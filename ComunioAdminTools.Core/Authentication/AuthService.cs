using ComunioAdminTools.Core.Localization;

namespace ComunioAdminTools.Core.Authentication;

public sealed class AuthService(
    IComunioTokenClient tokenClient, ISessionManager sessionManager,
    AppLocalization text)
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var token = await tokenClient.LoginAsync(username, password, cancellationToken);
            await sessionManager.LoadAsync(token, cancellationToken);
        }
        catch (Exception ex)
        {
            await sessionManager.ClearAsync(CancellationToken.None);
            throw new AuthenticationRequiredException(text, TextKey.SignInError, ex);
        }
        finally { gate.Release(); }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try { await sessionManager.ClearAsync(cancellationToken); }
        finally { gate.Release(); }
    }

}
