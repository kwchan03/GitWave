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

            try
            {
                // 3. Test the token
                var res = await _http.GetAsync("https://api.github.com/user");

                // 4. THIS LINE CAUSES THE CRASH - Wrap it!
                res.EnsureSuccessStatusCode();

                // 5. If success, save/update host entry
                _gcm.StoreForHost(creds.Username, creds.Secret);

                return (creds.Username, creds.Secret);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("401"))
            {
                // 5. CAUGHT BAD TOKEN: Revoke it immediately
                _gcm.Revoke("https://github.com/");

                // 6. Throw a friendly error or recursively call SignInAsync() to prompt again
                throw new Exception("Authentication failed. The saved credential was invalid and has been cleared. Please try logging in again.", ex);
            }
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
