using System.Net;
using ComunioAdminTools.Core.Localization;
using Newtonsoft.Json;

namespace ComunioAdminTools.Core.Services;

internal static class ComunioHttp
{
    public static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, HttpRequestMessage request, AppLocalization text, CancellationToken cancellationToken = default)
    {
        try { return await client.SendAsync(request, cancellationToken); }
        catch (HttpRequestException ex) when (ex is not ILocalizedError)
        {
            throw new LocalizedHttpRequestException(text, TextKey.NetworkError, ex.StatusCode, ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new LocalizedHttpRequestException(text, TextKey.TimeoutError, inner: ex);
        }
    }

    public static void EnsureSuccess(HttpResponseMessage response, AppLocalization text)
    {
        if (response.IsSuccessStatusCode) return;
        var key = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => TextKey.SessionExpired,
            HttpStatusCode.Forbidden => TextKey.PermissionDenied,
            HttpStatusCode.NotFound => TextKey.ApiNotFound,
            HttpStatusCode.TooManyRequests => TextKey.TooManyRequests,
            >= HttpStatusCode.InternalServerError => TextKey.ServiceUnavailable,
            _ => TextKey.HttpError
        };
        throw new LocalizedHttpRequestException(text, key, response.StatusCode);
    }

    public static T Deserialize<T>(string json, AppLocalization text, TextKey emptyKey = TextKey.EmptyJsonResponse)
    {
        try
        {
            return JsonConvert.DeserializeObject<T>(json)
                ?? throw new LocalizedInvalidOperationException(text, emptyKey);
        }
        catch (JsonException ex)
        {
            throw new LocalizedInvalidOperationException(text, TextKey.InvalidJsonResponse, ex);
        }
    }
}
