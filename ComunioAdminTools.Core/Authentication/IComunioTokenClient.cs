namespace ComunioAdminTools.Core.Authentication;

public interface IComunioTokenClient
{
    Task<TokenSession> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<TokenSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
}
