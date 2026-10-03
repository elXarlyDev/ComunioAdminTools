using System.Net.Http.Headers;

namespace ComunioAdminTools.Core.Authentication;

public sealed class ComunioAuthHandler(IAccessTokenProvider tokens) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is not { Scheme: "https", Host: "api.comunio.es" })
            throw new InvalidOperationException("Destino no autorizado para el token de Comunio.");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
            await tokens.GetValidAccessTokenAsync(cancellationToken));
        // Do not automatically replay requests that may move money.
        return await base.SendAsync(request, cancellationToken);
    }
}
