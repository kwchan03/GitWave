using GitGUI.Core;
using GitWave.Core;
using GitWave.Models;
using GitWave.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using OpenTap;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;

namespace GitWave.ViewModels
{
    public class RepositoryViewModel : BaseViewModel
    {
        private readonly IGitService _gitService;
        private readonly TapDockContextService _tapContext;
        private readonly TraceSource _log = Log.CreateSource("GitWave");

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

        public RepositoryViewModel(IGitService gitService, TapDockContextService tapContext)
        {
            _gitService = gitService;
            _tapContext = tapContext;

            OpenRepoCommand = new AsyncRelayCommand(_ => OpenRepoAsync());
            CreateRepoCommand = new AsyncRelayCommand(_ => CreateRepoAsync());
            CloneRepoCommand = new AsyncRelayCommand(_ => CloneRepoAsync());
            BrowseFolderCommand = new RelayCommand(_ => BrowseFolder());

            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                CheckForActivePlanRepo();
            }, DispatcherPriority.ApplicationIdle);
        }

        // --- Actions ---
        private void CheckForActivePlanRepo()
        {
            _log.Info("CheckForActivePlanRepo: Starting check...");

            // 1. Get the current TestPlan path
            string planPath = _tapContext.TryGetCurrentTestPlanPath();
            _log.Info($"CheckForActivePlanRepo: Active Plan Path -> '{planPath ?? "<null>"}'");

            if (string.IsNullOrWhiteSpace(planPath))
            {
                _log.Info("CheckForActivePlanRepo: No active plan path found. Aborting.");
                return;
            }

            // 2. Check if this plan lives inside a Git Repo
            string? detectedRoot = _gitService.FindGitRoot(planPath);
            _log.Info($"CheckForActivePlanRepo: Detected Git Root -> '{detectedRoot ?? "<null>"}'");

            if (string.IsNullOrEmpty(detectedRoot))
            {
                _log.Info("CheckForActivePlanRepo: Plan is not in a Git repository.");
                return;
            }

            // 3. Validate state
            if (_gitService.IsRepositoryOpen)
            {
                _log.Info($"CheckForActivePlanRepo: A repository is already open at '{_gitService.GetRepositoryPath()}'. Skipping prompt.");
                return;
            }

            // 4. Prompt the User
            _log.Info("CheckForActivePlanRepo: Prompting user to open repo...");

            var result = MessageBox.Show(
                $"The active Test Plan is part of a Git repository at:\n{detectedRoot}\n\nDo you want to open it?",
                "Repository Detected",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            _log.Info($"CheckForActivePlanRepo: User response -> {result}");

            if (result == MessageBoxResult.Yes)
            {
                // 5. Trigger Open
                RepoPath = detectedRoot;
                _log.Info($"CheckForActivePlanRepo: Opening repository at '{RepoPath}'...");

                _ = OpenRepoAsync();
            }
            else
            {
                _log.Info("CheckForActivePlanRepo: User declined to open the repository.");
            }
        }

        private async void NavigateToWorkspace()
        {
            // 1. Get the Navigation Service
            var navService = Bootstrapper.Services.GetRequiredService<NavigationService>();

            // 2. Navigate BOTH regions to their main pages
            await navService.Navigate<OperationViewModel>(NavigationRegion.SourceControl);
            await navService.Navigate<PullRequestPageViewModel>(NavigationRegion.PullRequest);
            await navService.Navigate<CommitGraphViewModel>(NavigationRegion.CommitGraph);

            var operationVm = Bootstrapper.Services.GetRequiredService<OperationViewModel>();
            if (operationVm != null)
            {
                await operationVm.InitializeRepoAsync();
            }
        }
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

                // Everything after await runs on UI thread automatically
                NavigateToWorkspace();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed: {ex.Message}");
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

                await Task.Run(() =>
                {
                    try
                    {
                        _gitService.CreateRepository(RepoPath);
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show($"Error creating repository: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                });

                NavigateToWorkspace();
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

                await Task.Run(() =>
                {
                    try
                    {
                        _gitService.CloneRepository(SourceUrl, RepoPath, AuthenticatedUser);
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show($"Clone failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                });

                // Marshal UI updates back to STA thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("Repository cloned successfully!");
                    NavigateToWorkspace();
                });
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}