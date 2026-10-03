using Newtonsoft.Json;
using System.Text;

namespace ComunioAdminTools.Core.Services
{
    public abstract class ComunioService
    {
        protected static readonly string apiBaseUrl = "https://api.comunio.es";
        protected readonly HttpClient httpClient;

        protected ComunioService(IHttpClientFactory factory)
        {
            httpClient = factory.CreateClient("Comunio");
        }

        public async Task<T> GetAsync<T>(string uri) where T : class
        {
            using var httpResponse = await httpClient.GetAsync(uri);
            await EnsureGetSuccess(httpResponse, uri);

            var json = await httpResponse.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(json)
                ?? throw new InvalidOperationException("Respuesta JSON vacía.");
        }

        public async Task<TResponse> PostAsync<TRequest, TResponse>(string uri, TRequest body)
            where TRequest : class
        {
            var json = JsonConvert.SerializeObject(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var httpResponse = await httpClient.PostAsync(uri, content);
            await EnsurePostSuccess(httpResponse, uri, content);

            return JsonConvert.DeserializeObject<TResponse>(await httpResponse.Content.ReadAsStringAsync())
                ?? throw new InvalidOperationException("Respuesta JSON vacía.");
        }

        public async Task PostWithoutResponseAsync<T>(string uri, T body)
            where T : class
        {
            var json = JsonConvert.SerializeObject(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var httpResponse = await httpClient.PostAsync(uri, content);
            await EnsurePostSuccess(httpResponse, uri, content);
        }

        private static async Task EnsureGetSuccess(HttpResponseMessage httpResponse, string uri)
        {
            if (!httpResponse.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"HTTP {(int)httpResponse.StatusCode} {httpResponse.StatusCode}{Environment.NewLine}" +
                    $"URL: {uri}{Environment.NewLine}" +
                    $"RESPONSE: {await httpResponse.Content.ReadAsStringAsync()}");
            }
        }

        private static async Task EnsurePostSuccess(HttpResponseMessage httpResponse, string uri, StringContent content)
        {
            if (!httpResponse.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"HTTP {(int)httpResponse.StatusCode} {httpResponse.StatusCode}{Environment.NewLine}" +
                    $"URL: {uri}{Environment.NewLine}" +
                    $"RESPONSE: {await httpResponse.Content.ReadAsStringAsync()}");
            }
        }
    }
}
