namespace ComunioAdminTools.Core.Authentication;

// Refresh before the authentication handler reads the token; never replay requests.
public sealed class SessionRefreshHandler(ISessionManager sessionManager) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await sessionManager.GetValidAccessTokenAsync(cancellationToken);
        return await base.SendAsync(request, cancellationToken);
    }
}
