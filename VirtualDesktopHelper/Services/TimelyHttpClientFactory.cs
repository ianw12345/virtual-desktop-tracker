using System.Net.Http;
using System.Text.Json;
using VirtualDesktopHelper.Configuration;

namespace VirtualDesktopHelper.Services
{
    /// <summary>
    /// Creates consistently configured HTTP clients for all Timely endpoints.
    /// </summary>
    internal static class TimelyHttpClientFactory
    {
        private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36 Edg/139.0.0.0";

        public static JsonSerializerOptions JsonOptions { get; } = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        public static HttpClient Create(TimelyConfiguration configuration, bool includeUploadHeaders = false)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("accept", "application/json");
            client.DefaultRequestHeaders.Add("accept-language", "en-US,en;q=0.9,nl;q=0.8");
            client.DefaultRequestHeaders.Add("cache-control", "no-cache");
            client.DefaultRequestHeaders.Add("user-agent", UserAgent);

            if (!string.IsNullOrEmpty(configuration.CookieString))
            {
                client.DefaultRequestHeaders.Add("Cookie", configuration.CookieString);
            }

            if (includeUploadHeaders)
            {
                client.DefaultRequestHeaders.Add("origin", configuration.ApiBaseUrl);
                client.DefaultRequestHeaders.Add("pragma", "no-cache");
                client.DefaultRequestHeaders.Add("priority", "u=1, i");
                client.DefaultRequestHeaders.Add("sec-ch-ua", "\"Not;A=Brand\";v=\"99\", \"Microsoft Edge\";v=\"139\", \"Chromium\";v=\"139\"");
                client.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
                client.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"Windows\"");
                client.DefaultRequestHeaders.Add("sec-fetch-dest", "empty");
                client.DefaultRequestHeaders.Add("sec-fetch-mode", "same-origin");
                client.DefaultRequestHeaders.Add("sec-fetch-site", "same-origin");
                AddHeaderWhenPresent(client, "x-csrf-token", configuration.CsrfToken);
                AddHeaderWhenPresent(client, "tl-socket-id", configuration.SocketId);
            }

            return client;
        }

        private static void AddHeaderWhenPresent(HttpClient client, string name, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                client.DefaultRequestHeaders.Add(name, value);
            }
        }
    }
}
