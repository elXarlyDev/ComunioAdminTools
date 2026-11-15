namespace ComunioAdminTools.Core.Authentication;

public interface IComunioTokenClient
{
    Task<TokenSession> Login(string username, string password, CancellationToken cancellationToken = default);
    Task<TokenSession> Refresh(string refreshToken, CancellationToken cancellationToken = default);
}
