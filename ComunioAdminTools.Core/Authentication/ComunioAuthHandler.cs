using System.Net.Http.Headers;

namespace ComunioAdminTools.Core.Authentication;

public sealed class ComunioAuthHandler(ISessionManager sessionManager) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await sessionManager.GetValidAccessTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        // Do not automatically replay requests that may move money.
        return await base.SendAsync(request, cancellationToken);
    }
}
