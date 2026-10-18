namespace ComunioAdminTools.Core.Authentication;

public sealed record TokenSession(string AccessToken, string? RefreshToken, DateTimeOffset ExpiresAtUtc);

public interface ITokenStore
{
    Task SetTokenAsync(string? token);
    Task<string?> GetTokenAsync();
}

public sealed class MemoryTokenStore : ITokenStore
{
    private string? token;

    public Task SetTokenAsync(string? token)
    {
        this.token = token;
        return Task.CompletedTask;
    }

    public Task<string?> GetTokenAsync() => Task.FromResult(token);
}

public sealed class AuthenticationRequiredException(
    Localization.AppLocalization text, Localization.TextKey key, Exception? innerException = null)
    : Exception(text[key], innerException), Localization.ILocalizedError
{
    public Localization.TextKey ResourceKey => key;
    public object?[] Arguments => [];
    public override string Message => text[key];
}

public sealed class ComunioAuthOptions
{
    public Func<string, HttpRequestMessage> CreateRefreshRequest { get; set; } = refreshToken =>
        new HttpRequestMessage(HttpMethod.Post, "login")
        {
            Content = new StringContent(
                Newtonsoft.Json.JsonConvert.SerializeObject(new { refresh_token = refreshToken }),
                System.Text.Encoding.UTF8,
                "application/json")
        };
    public TimeSpan RefreshMargin { get; set; } = TimeSpan.FromSeconds(60);
}
