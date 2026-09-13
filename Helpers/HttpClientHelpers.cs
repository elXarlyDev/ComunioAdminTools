using Newtonsoft.Json;
using System.Text;

namespace ComunioAdminTools.Helpers
{
    internal static class HttpClientHelpers
    {
        public static async Task<T> GetAsync<T>(this HttpClient httpClient, string uri) where T : class
        {
            var httpResponse = await httpClient.GetAsync(uri);
            await httpResponse.EnsureGetSuccess(uri);

            var json = await httpResponse.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(json);
        }

        public static async Task<TResponse> PostAsync<TRequest, TResponse>(this HttpClient httpClient, string uri, TRequest body)
            where TRequest : class
        {
            var json = JsonConvert.SerializeObject(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var httpResponse = await httpClient.PostAsync(uri, content);
            await httpResponse.EnsurePostSuccess(uri, content);

            return JsonConvert.DeserializeObject<TResponse>(await httpResponse.Content.ReadAsStringAsync());
        }

        public static async Task PostWithoutResponseAsync<T>(this HttpClient httpClient, string uri, T body)
            where T : class
        {
            var json = JsonConvert.SerializeObject(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var httpResponse = await httpClient.PostAsync(uri, content);
            await httpResponse.EnsurePostSuccess(uri, content);
        }

        private static async Task EnsureGetSuccess(this HttpResponseMessage httpResponse, string uri)
        {
            if (!httpResponse.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"HTTP {(int)httpResponse.StatusCode} {httpResponse.StatusCode}{Environment.NewLine}" +
                    $"URL: {uri}{Environment.NewLine}" +
                    $"RESPONSE: {await httpResponse.Content.ReadAsStringAsync()}");
            }
        }

        private static async Task EnsurePostSuccess(this HttpResponseMessage httpResponse, string uri, StringContent content)
        {
            if (!httpResponse.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"HTTP {(int)httpResponse.StatusCode} {httpResponse.StatusCode}{Environment.NewLine}" +
                    $"URL: {uri}{Environment.NewLine}" +
                    $"REQUEST: {await content.ReadAsStringAsync()}{Environment.NewLine}" +
                    $"RESPONSE: {await httpResponse.Content.ReadAsStringAsync()}");
            }
        }
    }
}
