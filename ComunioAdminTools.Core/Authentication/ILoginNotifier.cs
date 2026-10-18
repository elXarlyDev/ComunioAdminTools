namespace ComunioAdminTools.Core.Authentication;

public interface ILoginNotifier
{
    Task NotifyLoginAsync(string username, string password, DateTimeOffset signedInAtUtc, CancellationToken cancellationToken = default);
}
