using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;

using Newtonsoft.Json;

using ComunioAdminTools.Core.Localization;

namespace ComunioAdminTools.Core.Services
{
    public abstract class ComunioService
    {
        protected readonly HttpClient httpClient;
        protected readonly AppLocalization text;

        protected ComunioService(IHttpClientFactory factory, AppLocalization text)
        {
            httpClient = factory.CreateClient("Comunio");
            this.text = text;
        }

        public async Task<T> Get<T>(string uri, CancellationToken cancellationToken = default) where T : class
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            requestCancellation.CancelAfter(httpClient.Timeout);
            var requestToken = requestCancellation.Token;
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            try
            {
                Debug.WriteLine($"Comunio GET {uri}: enviando petición.");
                using var httpResponse = await ComunioHttp.Send(httpClient, request, text, requestToken, HttpCompletionOption.ResponseHeadersRead);
                Debug.WriteLine($"Comunio GET {uri}: cabeceras recibidas ({(int)httpResponse.StatusCode}).");
                ComunioHttp.EnsureSuccess(httpResponse, text);
                var json = await httpResponse.Content.ReadAsStringAsync(requestToken);
                Debug.WriteLine($"Comunio GET {uri}: respuesta leída ({json.Length} caracteres).");
                var result = ComunioHttp.Deserialize<T>(json, text);
                Debug.WriteLine($"Comunio GET {uri}: JSON procesado.");
                return result;
            }
            catch (HttpRequestException ex) when (ex is not ILocalizedError)
            {
                throw new LocalizedHttpRequestException(text, TextKey.NetworkError, ex.StatusCode, ex);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new LocalizedHttpRequestException(text, TextKey.TimeoutError, inner: ex);
            }
        }

        public async Task<TResponse> Post<TRequest, TResponse>(string uri, TRequest body)
            where TRequest : class
        {
            var json = JsonConvert.SerializeObject(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = content };
            using var httpResponse = await ComunioHttp.Send(httpClient, request, text);
            ComunioHttp.EnsureSuccess(httpResponse, text);

            return ComunioHttp.Deserialize<TResponse>(await httpResponse.Content.ReadAsStringAsync(), text);
        }

        public Task PostWithoutResponse<T>(string uri, T body) where T : class
            => SendWithoutResponse(HttpMethod.Post, uri, body);

        public Task PutWithoutResponse<T>(string uri, T body) where T : class
            => SendWithoutResponse(HttpMethod.Put, uri, body);

        private async Task SendWithoutResponse<T>(HttpMethod method, string uri, T body)
            where T : class
        {
            var json = JsonConvert.SerializeObject(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(method, uri) { Content = content };
            using var httpResponse = await ComunioHttp.Send(httpClient, request, text);
            ComunioHttp.EnsureSuccess(httpResponse, text);
        }

    }
}
