using GitWave.Models;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace GitWave.Services
{
    public sealed class GitHubAuthService
    {
        private readonly IGitCredentialProvider _gcm;
        private static readonly HttpClient _http = new HttpClient();

        public GitHubAuthService(IGitCredentialProvider gcm) { _gcm = gcm; }

        private void ConfigureHttpHeaders(string token)
        {
            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("WPF-GitWave-App");
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        /// One-time sign-in via GCM; returns a token usable for both API and Git.
        public async Task<(string Username, string Token)> SignInAsync()
        {
            // Ask for a host-wide github.com token
            var creds = _gcm.GetForUrl("https://github.com/");
            // Optionally persist immediately (but we also store after successful Git ops)
            _gcm.StoreForHost(creds.Username, creds.Secret);

            ConfigureHttpHeaders(creds.Secret);

            var res = await _http.GetAsync("https://api.github.com/user");
            res.EnsureSuccessStatusCode(); // throws if token invalid/SSO unauthorized
            Debug.WriteLine($"username: {creds.Username} secret: {creds.Secret}");
            return (creds.Username, creds.Secret);
        }

        public async Task<GitHubUser> GetCurrentUserAsync(string token)
        {
            ConfigureHttpHeaders(token);

            var res = await _http.GetAsync("https://api.github.com/user");
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadAsStringAsync();
            Debug.WriteLine($"json: {json}");
            Debug.WriteLine($"token: {token}");

            var user = JsonSerializer.Deserialize<GitHubUser>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            user.AccessToken = token; // keep for API usage
            return user;
        }
    }
}
