namespace ComunioAdminTools.Core.Authentication;

public sealed record TokenSession(string AccessToken, string? RefreshToken, DateTimeOffset ExpiresAtUtc);

public interface ITokenStore
{
    Task<TokenSession?> ReadAsync(CancellationToken cancellationToken = default);
    Task WriteAsync(TokenSession? session, CancellationToken cancellationToken = default);
}

public sealed class MemoryTokenStore : ITokenStore
{
    private TokenSession? session;
    public Task<TokenSession?> ReadAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(session);
    public Task WriteAsync(TokenSession? value, CancellationToken cancellationToken = default)
    {
        session = value;
        return Task.CompletedTask;
    }
}

public interface IAccessTokenProvider
{
    Task<string> GetValidAccessTokenAsync(CancellationToken cancellationToken = default);
}

public sealed class AuthenticationRequiredException(string message) : Exception(message);

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
