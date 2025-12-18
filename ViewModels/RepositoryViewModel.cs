using GitWave.Core;
using GitWave.Models; // For GitHubUser if needed
using GitWave.Services;
using GitWave.UI.Pages;
using Microsoft.Extensions.DependencyInjection; // For navigation
using Microsoft.Win32;
using System.Windows;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;

namespace GitWave.ViewModels
{
    public class RepositoryViewModel : BaseViewModel
    {
        private readonly IGitService _gitService;

        // --- Properties ---

        private string _repoPath;
        public string RepoPath
        {
            get => _repoPath;
            set => SetProperty(ref _repoPath, value);
        }

        private string _sourceUrl;
        public string SourceUrl
        {
            get => _sourceUrl;
            set => SetProperty(ref _sourceUrl, value);
        }

        private GitHubUser _authenticatedUser;
        public GitHubUser AuthenticatedUser
        {
            get => _authenticatedUser;
            set => SetProperty(ref _authenticatedUser, value);
        }

        // --- Commands ---
        public ICommand OpenRepoCommand { get; }
        public ICommand CreateRepoCommand { get; }
        public ICommand CloneRepoCommand { get; }
        public ICommand BrowseFolderCommand { get; }

        public RepositoryViewModel(IGitService gitService)
        {
            _gitService = gitService;

            // If the service stores the user, fetch it
            if (_gitService is GitLibService gitLib)
            {
                // Assuming GitLibService might expose the user, or you pass it in via another way.
                // For now, we can leave AuthenticatedUser null or set it if available.
            }

            OpenRepoCommand = new AsyncRelayCommand(_ => OpenRepoAsync());
            CreateRepoCommand = new AsyncRelayCommand(_ => CreateRepoAsync());
            CloneRepoCommand = new AsyncRelayCommand(_ => CloneRepoAsync());
            BrowseFolderCommand = new RelayCommand(_ => BrowseFolder());
        }

        // --- Actions ---

        private void BrowseFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Repository Folder",
                InitialDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments)
            };

            if (dialog.ShowDialog() == true)
            {
                RepoPath = dialog.FolderName;
            }
        }

        private async Task OpenRepoAsync()
        {
            if (string.IsNullOrWhiteSpace(RepoPath)) return;

            try
            {
                IsBusy = true;
                await Task.Run(() => _gitService.OpenRepository(RepoPath));

                // If user is logged in, sync it
                // _gitService.SetRepoUserFromAuthenticatedUser(AuthenticatedUser);

                NavigateToOperations();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open repository: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task CreateRepoAsync()
        {
            if (string.IsNullOrWhiteSpace(RepoPath)) return;

            try
            {
                IsBusy = true;
                await Task.Run(() => _gitService.CreateRepository(RepoPath));
                NavigateToOperations();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating repository: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task CloneRepoAsync()
        {
            if (string.IsNullOrWhiteSpace(SourceUrl))
            {
                MessageBox.Show("Please provide a Source URL.");
                return;
            }

            // If path is empty, ask for a parent directory to clone into
            if (string.IsNullOrWhiteSpace(RepoPath))
            {
                var dlg = new OpenFolderDialog
                {
                    Title = "Select destination folder to clone into"
                };

                if (dlg.ShowDialog() == true)
                {
                    RepoPath = dlg.FolderName;
                }
                else
                {
                    return; // User cancelled
                }
            }

            try
            {
                IsBusy = true;

                // Note: CloneRepository signature might vary based on your IGitService
                // Assuming: CloneRepository(url, destinationPath, user)
                await Task.Run(() => _gitService.CloneRepository(SourceUrl, RepoPath, AuthenticatedUser));

                MessageBox.Show("Repository cloned successfully!");
                NavigateToOperations();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Clone failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void NavigateToOperations()
        {
            // Use App.Services to get the MainWindow for navigation
            try
            {
                var mainWindow = App.Services.GetRequiredService<MainWindow>();
                var operationPage = App.Services.GetRequiredService<OperationPage>();

                // Ensure the OperationViewModel knows which repo we just opened
                if (operationPage.DataContext is OperationViewModel opVm)
                {
                    opVm.RepoPath = this.RepoPath;
                    opVm.InitializeRepo();
                }

                mainWindow.NavigateTo(operationPage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation failed: {ex.Message}");
            }
        }
    }
}