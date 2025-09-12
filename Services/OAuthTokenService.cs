using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace GitGUI.Services
{
    public class OAuthTokenService
    {
        private const string _clientId = @"Ov23li8BSqYRxBYZ4dBh"; // Replace with your actual client ID
        private const string _clientSecret = @"71e7612eedb5988cbd3ee6cfcf6671aa0693c8d8"; // Replace with your actual client secret
        private const string _authURL = @"https://github.com/login/oauth/authorize"; // Replace with your actual redirect URI
        private const string _tokenURL = @"https://github.com/login/oauth/access_token"; // Replace with your actual token URL

        private static readonly HttpClient httpClient = new HttpClient();

        public static async Task<string> GetAccessTokenAsync(string code, string redirectUri = "http://localhost:8080/callback")
        {
            var parameters = new Dictionary<string, string>
            {
                { "client_id", _clientId },
                { "client_secret", _clientSecret },
                { "code", code },
                { "redirect_uri", redirectUri }
            };

            var content = new FormUrlEncodedContent(parameters);

            var request = new HttpRequestMessage(HttpMethod.Post, _tokenURL);
            request.Content = content;

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var responseString = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseString);
            if (!doc.RootElement.TryGetProperty("access_token", out var tokenElement))
                return null;

            return tokenElement.GetString();
        }
    }
}
