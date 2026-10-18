using System.Net;
using System.Text;
using Newtonsoft.Json;
using ComunioAdminTools.Core.Localization;
using ComunioAdminTools.Core.Services;

namespace ComunioAdminTools.Core.Authentication;

public sealed class AuthService(
    IHttpClientFactory factory, ITokenStore store, ISessionManager sessionManager,
    AppLocalization text)
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        TokenSession tokenSession;
        try
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "login")
                {
                    Content = new StringContent(JsonConvert.SerializeObject(new { username, password }), Encoding.UTF8, "application/json")
                };
                var result = await SendTokenRequestAsync(factory, text, request, cancellationToken);
                tokenSession = CreateSession(result, null);
                await store.SetTokenAsync(tokenSession.AccessToken);
                await sessionManager.Clear();
            }
            finally 
            { 
                gate.Release(); 
            }
            await sessionManager.StartAsync(tokenSession, cancellationToken);
        }
        catch (Exception ex)
        {
            await sessionManager.Clear();
            throw new AuthenticationRequiredException(text, TextKey.SignInError, ex);
        }
    }

    public Task LogoutAsync(CancellationToken cancellationToken = default)
        => sessionManager.LogoutAsync(cancellationToken);

    internal static async Task<TokenResponse> SendTokenRequestAsync(IHttpClientFactory factory, AppLocalization text, HttpRequestMessage request, CancellationToken ct)
    {
        using var client = factory.CreateClient("ComunioAuth");
        using var response = await client.SendAsync(request, ct);
        if (response.StatusCode != HttpStatusCode.OK)
            throw new AuthenticationRequiredException(text, TextKey.SignInError);

        var body = await response.Content.ReadAsStringAsync(ct);
        return ComunioHttp.Deserialize<TokenResponse>(body, text, TextKey.SignInError);
    }

    internal static TokenSession CreateSession(TokenResponse result, string? previousRefreshToken)
    {
        return new(result.AccessToken ?? "",
            string.IsNullOrWhiteSpace(result.RefreshToken) ? previousRefreshToken : result.RefreshToken,
            DateTimeOffset.UtcNow.AddSeconds(result.ExpiresIn));
    }

    internal sealed class TokenResponse
    {
        [JsonProperty("access_token")] public string? AccessToken { get; set; }
        [JsonProperty("refresh_token")] public string? RefreshToken { get; set; }
        [JsonProperty("expires_in")] public int ExpiresIn { get; set; }
    }
}
