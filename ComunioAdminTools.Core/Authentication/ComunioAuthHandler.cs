using System.Net.Http.Headers;
using ComunioAdminTools.Core.Localization;

namespace ComunioAdminTools.Core.Authentication;

public sealed class ComunioAuthHandler(ITokenStore store, AppLocalization text) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is not { Scheme: "https", Host: "api.comunio.es" })
            throw new LocalizedInvalidOperationException(text, TextKey.TokenDestinationNotAllowed);
        var token = await store.GetTokenAsync()
            ?? throw new AuthenticationRequiredException(text, TextKey.SignInRequired);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        // Do not automatically replay requests that may move money.
        return await base.SendAsync(request, cancellationToken);
    }
}
