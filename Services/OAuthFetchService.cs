using GitWave.Models;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace GitWave.Services
{
    public static class OAuthFetchService
    {
        private static readonly HttpClient httpClient = new HttpClient();

        public static async Task<GitHubUser> GetGitHubUserAsync(string accessToken)
        {
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "WPF-GitWave-App");
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", accessToken);

            var response = await httpClient.GetAsync("https://api.github.com/user");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var user = JsonSerializer.Deserialize<GitHubUser>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            user.AccessToken = accessToken;
            return user;
        }
    }
}
