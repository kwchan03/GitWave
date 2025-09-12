using GitGUI.Models;
using GitGUI.Pages;
using GitGUI.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Windows.Input;

namespace GitGUI.ViewModels
{
    public class LoginViewModel : BaseViewModel
    {
        private readonly string clientID = @"Ov23li8BSqYRxBYZ4dBh";
        private readonly string redirectUri = "http://localhost:8080/callback";
        private readonly string scope = "read:user user:email";

        public ICommand LoginCommand { get; }

        public LoginViewModel()
        {
            LoginCommand = new RelayCommand(_ => OpenGitHubLogin());

            // Register callback
            Globals.GithubCallback = async code =>
            {
                try
                {
                    // Step 1: exchange code → access token
                    string token = await OAuthTokenService.GetAccessTokenAsync(code);

                    // Step 2: fetch user info
                    GitHubUser user = await OAuthFetchService.GetGitHubUserAsync(token);

                    // Step 3: inject user into OperationViewModel
                    var operationVM = App.Services.GetRequiredService<OperationViewModel>();
                    operationVM.AuthenticatedUser = user;

                    // Step 4: navigate to OperationPage
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        var main = App.Services.GetRequiredService<MainWindow>();
                        var operationPage = App.Services.GetRequiredService<OperationPage>();
                        main.NavigateTo(operationPage);
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"GitHub login failed: {ex.Message}");
                }
            };
        }

        private void OpenGitHubLogin()
        {
            string authUrl = $"https://github.com/login/oauth/authorize" +
                             $"?client_id={clientID}" +
                             $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                             $"&scope={Uri.EscapeDataString(scope)}";

            Process.Start(new ProcessStartInfo
            {
                FileName = authUrl,
                UseShellExecute = true
            });
        }
    }
}
