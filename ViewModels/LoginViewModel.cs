using GitWave.Models;
using GitWave.Services;
using GitWave.UI.Pages;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Windows.Input;

namespace GitWave.ViewModels
{
    public class LoginViewModel : BaseViewModel
    {
        private readonly GitHubAuthService _auth;

        public ICommand LoginCommand { get; }

        // Inject GitHubAuthService via DI
        public LoginViewModel(GitHubAuthService auth)
        {
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));

            // Async login using GCM device flow
            LoginCommand = new RelayCommand(async _ => await SignInAsync());
        }

        private async Task SignInAsync()
        {
            try
            {
                // Triggers GCM (device flow) if not signed in; returns a token usable for API + Git
                var (_username, token) = await _auth.SignInAsync();

                // Fetch the GitHub user using the same token
                GitHubUser user = await _auth.GetCurrentUserAsync(token);

                // Make the token available to the rest of the app (for API calls)
                var operationVM = App.Services.GetRequiredService<OperationViewModel>();
                operationVM.AuthenticatedUser = user;

                // Navigate to the main page
                App.Current.Dispatcher.Invoke(() =>
                {
                    var main = App.Services.GetRequiredService<MainWindow>();
                    var repositoryPage = App.Services.GetRequiredService<RepositoryPage>();
                    main.NavigateTo(repositoryPage);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GitHub (GCM) login failed: {ex}");
                // TODO: surface a user-friendly message in your UI if desired
            }
        }
    }
}
