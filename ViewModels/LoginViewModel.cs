using GitGUI.Core;
using GitWave.Core;
using GitWave.Models;
using GitWave.Services;
using Microsoft.Extensions.DependencyInjection;
using OpenTap;
using System.Windows;
using System.Windows.Input;

namespace GitWave.ViewModels
{
    public class LoginViewModel : BaseViewModel
    {
        private readonly GitHubAuthService _auth;
        private readonly IGitService _gitService;

        // 1. Create a Log Source for this component
        private readonly TraceSource _log = Log.CreateSource("GitWave");

        public ICommand LoginCommand { get; }

        public LoginViewModel(GitHubAuthService auth, IGitService gitService)
        {
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));

            LoginCommand = new RelayCommand(async _ => await SignInAsync());
        }

        private async Task SignInAsync()
        {
            try
            {
                // 2. Log start of process
                _log.Info("Starting GitHub login process...");

                // Authenticate
                var (_username, token) = await _auth.SignInAsync();
                _log.Debug($"Device flow finished. Token received for user: {_username}");

                // Fetch User Details
                GitHubUser user = await _auth.GetCurrentUserAsync(token);

                // 3. Log success
                _log.Info($"Successfully retrieved user profile: {user.Login}");

                // Update Service
                var gitService = Bootstrapper.Services.GetRequiredService<IGitService>();
                gitService.AuthenticatedUser = user;

                // (Optional) Log navigation attempt
                _log.Debug("Updating navigation state...");

                var navService = Bootstrapper.Services.GetRequiredService<NavigationService>();

                if (_gitService.IsRepositoryOpen)
                {
                    _log.Debug("Navigating to Operation and Pull Request Views...");
                    navService.Navigate<OperationViewModel>(NavigationRegion.SourceControl);
                    navService.Navigate<PullRequestPageViewModel>(NavigationRegion.PullRequest);
                    navService.Navigate<CommitGraphViewModel>(NavigationRegion.CommitGraph);
                }
                else
                {
                    _log.Debug("Navigating to Repository View...");
                    navService.Navigate<RepositoryViewModel>(NavigationRegion.SourceControl);
                    navService.Navigate<RepositoryViewModel>(NavigationRegion.PullRequest);
                    navService.Navigate<RepositoryViewModel>(NavigationRegion.CommitGraph);
                }
            }
            catch (Exception ex)
            {
                // 4. Log the full error to OpenTAP
                _log.Error($"GitHub Login Failed: {ex.Message}");
                _log.Debug(ex); // Logs the full stack trace

                MessageBox.Show($"Login Failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}