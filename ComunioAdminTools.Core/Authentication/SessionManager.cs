using ComunioAdminTools.Core.Models;
using ComunioAdminTools.Core.Localization;
using ComunioAdminTools.Core.Services;

namespace ComunioAdminTools.Core.Authentication;

public interface ISessionManager
{
    SessionDataModel? Session { get; }
    event Action? SessionChanged;
    Task<bool> HasSessionAsync(CancellationToken cancellationToken = default);
    Task<string> GetValidAccessTokenAsync(CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
    Task StartAsync(TokenSession tokenSession, CancellationToken cancellationToken = default);
    Task LoadAsync(CancellationToken cancellationToken = default);
    void Clear();
}

// One instance per MAUI/WebAssembly client, never a shared server-user session.
public sealed class SessionManager(IHttpClientFactory factory, ITokenStore store, ComunioAuthOptions options, AppLocalization text) : ISessionManager
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private TokenSession? tokenSession;

    public async Task StartAsync(TokenSession value, CancellationToken cancellationToken = default)
    {
        Clear();
        await SaveAsync(value, cancellationToken);
        await LoadAsync(cancellationToken);
    }

    private async Task SaveAsync(TokenSession? value, CancellationToken cancellationToken)
    {
        tokenSession = value;
        await store.SetTokenAsync(value?.AccessToken);
    }
    public event Action? SessionChanged;

    public Task<bool> HasSessionAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(tokenSession is not null);

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try { await ClearAsync(cancellationToken); }
        finally { gate.Release(); }
        SessionChanged?.Invoke();
    }

    private async Task ClearAsync(CancellationToken cancellationToken)
    {
        Clear();
        await SaveAsync(null, cancellationToken);
    }

    public SessionDataModel? Session { get; private set; }

    public void Clear() => Session = null;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (Session is not null) return;

        using var client = factory.CreateClient("Comunio");
        using var request = new HttpRequestMessage(HttpMethod.Get, string.Empty);
        using var response = await ComunioHttp.SendAsync(client, request, text, cancellationToken);

        ComunioHttp.EnsureSuccess(response, text);
        var result = ComunioHttp.Deserialize<SessionDataModel>(await response.Content.ReadAsStringAsync(cancellationToken), text, TextKey.EmptySessionResponse);

        Session = result;
        SessionChanged?.Invoke();
    }

    public async Task<string> GetValidAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var invalidated = false;
        await gate.WaitAsync(cancellationToken);
        try
        {
            var current = tokenSession
                ?? throw new AuthenticationRequiredException(text, TextKey.SignInRequired);
            if (current.ExpiresAtUtc > DateTimeOffset.UtcNow + options.RefreshMargin)
                return current.AccessToken;

            if (string.IsNullOrWhiteSpace(current.RefreshToken))
            {
                await ClearAsync(CancellationToken.None);
                invalidated = true;
                throw new AuthenticationRequiredException(text, TextKey.SessionExpired);
            }
            if (options.CreateRefreshRequest is null)
                throw new AuthenticationRequiredException(text, TextKey.RefreshNotConfigured);

            using var request = options.CreateRefreshRequest(current.RefreshToken);
            try
            {
                var result = await AuthService.SendTokenRequestAsync(factory, text, request, cancellationToken);
                var renewed = AuthService.CreateSession(result, current.RefreshToken);
                await SaveAsync(renewed, cancellationToken);
                return renewed.AccessToken;
            }
            catch (AuthenticationRequiredException)
            {
                await ClearAsync(CancellationToken.None);
                invalidated = true;
                throw;
            }
        }
        finally
        {
            gate.Release();
            if (invalidated) SessionChanged?.Invoke();
        }
    }

}
