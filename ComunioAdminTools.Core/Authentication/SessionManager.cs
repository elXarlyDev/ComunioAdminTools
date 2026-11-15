using System.Net.Http.Headers;

using ComunioAdminTools.Core.Localization;
using ComunioAdminTools.Core.Models;
using ComunioAdminTools.Core.Services;

namespace ComunioAdminTools.Core.Authentication;

public interface ISessionManager
{
    SessionDataModel? Session { get; }

    event Action? SessionChanged;

    Task Load(TokenSession tokenSession, CancellationToken cancellationToken = default);
    Task Clear(CancellationToken cancellationToken = default);
    Task<string> GetValidAccessToken(CancellationToken cancellationToken = default);
}

// One instance per MAUI/WebAssembly client, never a shared server-user session.
public sealed class SessionManager(IHttpClientFactory factory, IComunioTokenClient tokenClient, AppLocalization text) : ISessionManager
{
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(60);
    private readonly SemaphoreSlim gate = new(1, 1);
    private SessionState? state;

    public SessionDataModel? Session => state?.Data;

    public event Action? SessionChanged;

    // AuthService serializes login/logout; gate coordinates state changes with refresh.
    public async Task Load(TokenSession value, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);

        try
        {
            state = null;
            var profile = await LoadProfile(value.AccessToken, cancellationToken);
            state = new SessionState(value) { Data = profile };
        }
        catch
        {
            state = null;
            throw;
        }
        finally
        {
            gate.Release();
            SessionChanged?.Invoke();
        }
    }

    public async Task Clear(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);

        try { state = null; }
        finally { gate.Release(); }

        SessionChanged?.Invoke();
    }

    public async Task<string> GetValidAccessToken(CancellationToken cancellationToken = default)
    {
        var invalidated = false;
        await gate.WaitAsync(cancellationToken);

        try
        {
            var session = state
                ?? throw new AuthenticationRequiredException(text, TextKey.SignInRequired);

            if (session.Token.ExpiresAtUtc <= DateTimeOffset.UtcNow + RefreshMargin)
                session.Token = await RefreshToken(session.Token, cancellationToken);

            return session.Token.AccessToken;
        }
        catch (AuthenticationRequiredException) when (state is not null)
        {
            state = null;
            invalidated = true;
            throw;
        }
        finally
        {
            gate.Release();
            if (invalidated) SessionChanged?.Invoke();
        }
    }

    private Task<TokenSession> RefreshToken(TokenSession token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            throw new AuthenticationRequiredException(text, TextKey.SessionExpired);
        }

        return tokenClient.Refresh(token.RefreshToken, cancellationToken);
    }

    private async Task<SessionDataModel> LoadProfile(string accessToken, CancellationToken cancellationToken)
    {
        // This client has no session handler; Load already holds gate.
        using var client = factory.CreateClient("ComunioAuth");
        using var request = new HttpRequestMessage(HttpMethod.Get, string.Empty);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await ComunioHttp.Send(client, request, text, cancellationToken);

        ComunioHttp.EnsureSuccess(response, text);
        return ComunioHttp.Deserialize<SessionDataModel>(await response.Content.ReadAsStringAsync(cancellationToken), text, TextKey.EmptySessionResponse);
    }

    private sealed class SessionState(TokenSession token)
    {
        public TokenSession Token { get; set; } = token;
        public SessionDataModel? Data { get; set; }
    }
}
