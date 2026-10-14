using System.Net.Http.Headers;
using ComunioAdminTools.Core.Localization;

namespace ComunioAdminTools.Core.Authentication;

public sealed class ComunioAuthHandler(IAccessTokenProvider tokens, AppLocalization text) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is not { Scheme: "https", Host: "api.comunio.es" })
            throw new LocalizedInvalidOperationException(text, TextKey.TokenDestinationNotAllowed);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
            await tokens.GetValidAccessTokenAsync(cancellationToken));
        // Do not automatically replay requests that may move money.
        return await base.SendAsync(request, cancellationToken);
    }
}
