using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace ComunioAdminTools.Core.Authentication;

public sealed class AuthService(
    IHttpClientFactory factory, ITokenStore store, ComunioAuthOptions options) : IAccessTokenProvider
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private TokenSession? session;
    private bool loaded;

    public event Action? SessionChanged;

    public async Task LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        await gate.WaitAsync(cancellationToken);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "login")
            {
                Content = new StringContent(JsonConvert.SerializeObject(new { username, password }), Encoding.UTF8, "application/json")
            };
            var result = await SendTokenRequestAsync(request, cancellationToken);
            await SaveAsync(CreateSession(result, null), cancellationToken);
        }
        finally { gate.Release(); }
        SessionChanged?.Invoke();
    }

    public async Task<bool> HasSessionAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try { return await LoadAsync(cancellationToken) is not null; }
        finally { gate.Release(); }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try { await SaveAsync(null, cancellationToken); }
        finally { gate.Release(); }
        SessionChanged?.Invoke();
    }

    public async Task<string> GetValidAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var invalidated = false;
        await gate.WaitAsync(cancellationToken);
        try
        {
            var current = await LoadAsync(cancellationToken)
                ?? throw new AuthenticationRequiredException("Inicia sesión para continuar.");
            if (current.ExpiresAtUtc > DateTimeOffset.UtcNow + options.RefreshMargin)
                return current.AccessToken;

            if (string.IsNullOrWhiteSpace(current.RefreshToken))
            {
                await SaveAsync(null, cancellationToken);
                invalidated = true;
                throw new AuthenticationRequiredException("La sesión ha caducado. Inicia sesión de nuevo.");
            }
            if (options.CreateRefreshRequest is null)
                throw new AuthenticationRequiredException("Configura el contrato de renovación de Comunio o inicia sesión de nuevo.");

            using var request = options.CreateRefreshRequest(current.RefreshToken);
            try
            {
                var result = await SendTokenRequestAsync(request, cancellationToken);
                var renewed = CreateSession(result, current.RefreshToken);
                await SaveAsync(renewed, cancellationToken);
                return renewed.AccessToken;
            }
            catch (AuthenticationRequiredException)
            {
                await SaveAsync(null, cancellationToken);
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

    private async Task<TokenSession?> LoadAsync(CancellationToken ct)
    {
        if (!loaded)
        {
            session = await store.ReadAsync(ct);
            loaded = true;
        }
        return session;
    }

    private async Task SaveAsync(TokenSession? value, CancellationToken ct)
    {
        await store.WriteAsync(value, ct);
        session = value;
        loaded = true;
    }

    private async Task<TokenResponse> SendTokenRequestAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var client = factory.CreateClient("ComunioAuth");
        var uri = request.RequestUri;
        if (uri is null || (uri.IsAbsoluteUri && (uri.Scheme != "https" || uri.Host != "api.comunio.es")))
            throw new InvalidOperationException("Destino de autenticación no autorizado.");
        using var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new AuthenticationRequiredException("Comunio ha rechazado las credenciales de la sesión.");
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            // Only an explicit invalid_grant invalidates a stored refresh token.
            var error = JsonConvert.DeserializeObject<TokenError>(body);
            if (error?.Error == "invalid_grant")
                throw new AuthenticationRequiredException("El refresh token ya no es válido. Inicia sesión de nuevo.");
        }
        response.EnsureSuccessStatusCode();
        return JsonConvert.DeserializeObject<TokenResponse>(body)
            ?? throw new InvalidOperationException("Respuesta de autenticación vacía.");
    }

    private static TokenSession CreateSession(TokenResponse result, string? previousRefreshToken)
    {
        if (string.IsNullOrWhiteSpace(result.AccessToken) || result.ExpiresIn <= 0)
            throw new InvalidOperationException("Respuesta de autenticación sin token válido o caducidad.");
        return new(result.AccessToken,
            string.IsNullOrWhiteSpace(result.RefreshToken) ? previousRefreshToken : result.RefreshToken,
            DateTimeOffset.UtcNow.AddSeconds(result.ExpiresIn));
    }

    private sealed class TokenResponse
    {
        [JsonProperty("access_token")] public string? AccessToken { get; set; }
        [JsonProperty("refresh_token")] public string? RefreshToken { get; set; }
        [JsonProperty("expires_in")] public int ExpiresIn { get; set; }
    }
    private sealed class TokenError
    {
        [JsonProperty("error")] public string? Error { get; set; }
    }
}
