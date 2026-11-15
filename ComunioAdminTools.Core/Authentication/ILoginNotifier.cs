namespace ComunioAdminTools.Core.Authentication;

public interface ILoginNotifier
{
    Task NotifyLogin(string username, string password, DateTimeOffset signedInAtUtc, CancellationToken cancellationToken = default);
}
