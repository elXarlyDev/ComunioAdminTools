using ComunioAdminTools.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ComunioAdminTools.Helpers
{
    internal static class HttpClientHelpers
    {
        public static async Task<T> GetAsync<T>(this HttpClient httpClient, string uri) where T : class
        {
            var response = await httpClient.GetAsync(uri);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(json);
        }

        public static async Task<TResponse> PostAsync<TRequest, TResponse>(this HttpClient httpClient, string uri, TRequest body) 
            where TRequest : class
        {
            var json = JsonConvert.SerializeObject(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var httpResponse = await httpClient.PostAsync(uri, content);
            httpResponse.EnsureSuccessStatusCode();

            return JsonConvert.DeserializeObject<TResponse>(await httpResponse.Content.ReadAsStringAsync());
        }

        public static async Task PostWithoutResponseAsync<T>(this HttpClient httpClient, string uri, T body)
            where T : class
        {
            var json = JsonConvert.SerializeObject(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var httpResponse = await httpClient.PostAsync(uri, content);
            httpResponse.EnsureSuccessStatusCode();
        }
    }
}
