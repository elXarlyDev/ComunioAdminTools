using System.Net;
using System.Text;

using Newtonsoft.Json;

using ComunioAdminTools.Core.Localization;
using ComunioAdminTools.Core.Services;

namespace ComunioAdminTools.Core.Authentication;

public sealed class ComunioTokenClient(IHttpClientFactory factory, AppLocalization text) : IComunioTokenClient
{
    public Task<TokenSession> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
        => RequestTokenAsync(new { username, password }, null, cancellationToken);

    public Task<TokenSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
        => RequestTokenAsync(new { refresh_token = refreshToken }, refreshToken, cancellationToken);

    private async Task<TokenSession> RequestTokenAsync(object payload, string? previousRefreshToken, CancellationToken cancellationToken)
    {
        using var client = factory.CreateClient("ComunioAuth");
        using var request = new HttpRequestMessage(HttpMethod.Post, "login")
        {
            Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
        };
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK)
            throw new AuthenticationRequiredException(text, TextKey.SignInError);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = ComunioHttp.Deserialize<TokenResponse>(body, text, TextKey.SignInError);
        return new TokenSession(
            result.AccessToken ?? "",
            string.IsNullOrWhiteSpace(result.RefreshToken) ? previousRefreshToken : result.RefreshToken,
            DateTimeOffset.UtcNow.AddSeconds(result.ExpiresIn));
    }

    private sealed class TokenResponse
    {
        [JsonProperty("access_token")] public string? AccessToken { get; set; }
        [JsonProperty("refresh_token")] public string? RefreshToken { get; set; }
        [JsonProperty("expires_in")] public int ExpiresIn { get; set; }
    }
}
