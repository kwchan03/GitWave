using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace GitWave.UI.Pages
{
    /// <summary>
    /// Interaction logic for GitHubLogin.xaml
    /// </summary>
    public partial class GitHubLoginPage : Page
    {
        private readonly string clientId = ConfigurationManager.AppSettings["GitHubClientId"];
        private readonly string clientSecret = ConfigurationManager.AppSettings["GitHubClientSecret"];
        private readonly string redirectUri = "http://localhost:8080/";

        private TaskCompletionSource<string> _tcsToken = new TaskCompletionSource<string>();

        public GitHubLoginPage()
        {
            InitializeComponent();
            Loaded += GitHubLoginPage_Loaded;
        }

        /// <summary>
        /// Call this after navigation to this Page.
        /// </summary>
        public Task<string> GetGitHubAccessTokenAsync()
        {
            return _tcsToken.Task;
        }

        private async void GitHubLoginPage_Loaded(object sender, RoutedEventArgs e)
        {
            var listener = new HttpListener();
            listener.Prefixes.Add(redirectUri);
            listener.Start();

            var state = $"nonce_{Guid.NewGuid():N}";
            var authorizeUrl =
                $"https://github.com/login/oauth/authorize" +
                $"?client_id={clientId}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&scope=read:user%20repo" +
                $"&state={state}";

            // Navigate the embedded browser
            BrowserControl.Navigate(authorizeUrl);

            // Wait for GitHub redirect
            var context = await Task.Run(() => listener.GetContext());
            var code = context.Request.QueryString["code"];
            var returnedState = context.Request.QueryString["state"];

            // (Optional: validate returnedState == state)

            // Respond to the browser so the user sees something
            var responseString = "<html><body><h2>You can close this tab.</h2></body></html>";
            var buffer = Encoding.UTF8.GetBytes(responseString);
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();

            listener.Stop();

            // Instead of Close(), raise navigation back to a “result” page or
            // trigger a callback indicating the token has arrived.
            try
            {
                var token = await ExchangeCodeForTokenAsync(code);
                _tcsToken.SetResult(token);
            }
            catch (Exception ex)
            {
                _tcsToken.SetException(ex);
            }
        }

        private async Task<string> ExchangeCodeForTokenAsync(string code)
        {
            using (var http = new HttpClient())
            {
                var tokenRequestUrl = "https://github.com/login/oauth/access_token";
                var body = new[]
                {
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("client_secret", clientSecret),
                    new KeyValuePair<string, string>("code", code),
                    new KeyValuePair<string, string>("redirect_uri", redirectUri)
                };
                var content = new FormUrlEncodedContent(body);
                http.DefaultRequestHeaders.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                var response = await http.PostAsync(tokenRequestUrl, content);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();

                // Parse the JSON response to extract the access token
                var tokenData = System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(json);
                if (string.IsNullOrEmpty(tokenData?.AccessToken))
                    throw new Exception("No access token returned from GitHub.");

                return tokenData.AccessToken;
            }
        }

        // Define a class to deserialize the JSON response
        private class TokenResponse
        {
            public string AccessToken { get; set; }
        }
    }
}
