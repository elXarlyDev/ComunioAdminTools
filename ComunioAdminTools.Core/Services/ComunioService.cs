using ComunioAdminTools.Core.Authentication;
using Newtonsoft.Json;
using ComunioAdminTools.Core.Localization;
using System.Text;

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

        public async Task<T> GetAsync<T>(string uri) where T : class
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var httpResponse = await ComunioHttp.SendAsync(httpClient, request, text);
            ComunioHttp.EnsureSuccess(httpResponse, text);

            var json = await httpResponse.Content.ReadAsStringAsync();
            return ComunioHttp.Deserialize<T>(json, text);
        }

        public async Task<TResponse> PostAsync<TRequest, TResponse>(string uri, TRequest body)
            where TRequest : class
        {
            var json = JsonConvert.SerializeObject(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = content };
            using var httpResponse = await ComunioHttp.SendAsync(httpClient, request, text);
            ComunioHttp.EnsureSuccess(httpResponse, text);

            return ComunioHttp.Deserialize<TResponse>(await httpResponse.Content.ReadAsStringAsync(), text);
        }

        public async Task PostWithoutResponseAsync<T>(string uri, T body)
            where T : class
        {
            var json = JsonConvert.SerializeObject(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = content };
            using var httpResponse = await ComunioHttp.SendAsync(httpClient, request, text);
            ComunioHttp.EnsureSuccess(httpResponse, text);
        }

    }
}
