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
            try
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
            catch (Exception ex)
            {
                _log.Error($"CheckForActivePlanRepo: Unexpected error: {ex.Message}");
                _log.Debug(ex);
                MessageBox.Show(
                    $"An error occurred while checking for repository:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void NavigateToWorkspace()
        {
            try
            {
                _log.Info("NavigateToWorkspace: Starting navigation...");

                // 1. Get the Navigation Service
                var navService = Bootstrapper.Services.GetRequiredService<NavigationService>();

                // 2. Navigate BOTH regions to their main pages
                _log.Debug("Navigating to OperationViewModel...");
                await navService.Navigate<OperationViewModel>(NavigationRegion.SourceControl);

                _log.Debug("Navigating to PullRequestPageViewModel...");
                await navService.Navigate<PullRequestPageViewModel>(NavigationRegion.PullRequest);

                _log.Debug("Navigating to CommitGraphViewModel...");
                await navService.Navigate<CommitGraphViewModel>(NavigationRegion.CommitGraph);

                _log.Debug("Initializing OperationViewModel...");
                var operationVm = Bootstrapper.Services.GetRequiredService<OperationViewModel>();
                //if (operationVm != null)
                //{
                //    await operationVm.InitializeRepoAsync();
                //    _log.Info("NavigateToWorkspace: Navigation completed successfully");
                //}
                //else
                //{
                //    _log.Error("NavigateToWorkspace: OperationViewModel is null");
                //    MessageBox.Show(
                //        "Failed to initialize operation view.",
                //        "Error",
                //        MessageBoxButton.OK,
                //        MessageBoxImage.Error);
                //}
            }
            catch (Exception ex)
            {
                _log.Error($"NavigateToWorkspace: Error: {ex.Message}");
                _log.Debug(ex);
                MessageBox.Show(
                    $"Failed to navigate to workspace:\n{ex.Message}",
                    "Navigation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BrowseFolder()
        {
            try
            {
                var dialog = new OpenFolderDialog
                {
                    Title = "Select Repository Folder",
                    InitialDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments)
                };

                if (dialog.ShowDialog() == true)
                {
                    RepoPath = dialog.FolderName;
                    _log.Debug($"BrowseFolder: Selected folder: {RepoPath}");
                }
            }
            catch (Exception ex)
            {
                _log.Error($"BrowseFolder: Error: {ex.Message}");
                MessageBox.Show(
                    $"Failed to browse folder:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task OpenRepoAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(RepoPath))
                {
                    MessageBox.Show(
                        "Please select a repository folder.",
                        "Invalid Path",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                _log.Info($"OpenRepoAsync: Opening repository at '{RepoPath}'...");
                IsBusy = true;

                await Task.Run(() =>
                {
                    if (!System.IO.Directory.Exists(RepoPath))
                    {
                        throw new InvalidOperationException($"Directory does not exist: {RepoPath}");
                    }

                    _gitService.OpenRepository(RepoPath);
                });

                _log.Info("OpenRepoAsync: Repository opened successfully");

                // Everything after await runs on UI thread automatically
                NavigateToWorkspace();
            }
            catch (InvalidOperationException ex)
            {
                _log.Error($"OpenRepoAsync: Invalid operation: {ex.Message}");
                MessageBox.Show(
                    $"Failed to open repository:\n{ex.Message}",
                    "Invalid Repository",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                _log.Error($"OpenRepoAsync: Error: {ex.Message}");
                _log.Debug(ex);
                MessageBox.Show(
                    $"Failed to open repository:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task CreateRepoAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(RepoPath))
                {
                    MessageBox.Show(
                        "Please select a folder to create the repository.",
                        "Invalid Path",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                _log.Info($"CreateRepoAsync: Creating repository at '{RepoPath}'...");
                IsBusy = true;

                await Task.Run(() =>
                {
                    try
                    {
                        if (!System.IO.Directory.Exists(RepoPath))
                        {
                            System.IO.Directory.CreateDirectory(RepoPath);
                            _log.Debug($"CreateRepoAsync: Created directory: {RepoPath}");
                        }

                        _gitService.CreateRepository(RepoPath);
                        MessageBox.Show(
                        "Repository created successfully",
                        "Created Successfully",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                        _log.Info("CreateRepoAsync: Repository created successfully");
                    }
                    catch (Exception ex)
                    {
                        _log.Error($"CreateRepoAsync: Error: {ex.Message}");
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(
                                $"Error creating repository:\n{ex.Message}",
                                "Creation Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        });
                        throw;
                    }
                });

                NavigateToWorkspace();
            }
            catch (Exception ex)
            {
                _log.Error($"CreateRepoAsync: Unexpected error: {ex.Message}");
                _log.Debug(ex);
                // Error already shown in the task
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task CloneRepoAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(SourceUrl))
                {
                    MessageBox.Show(
                        "Please provide a source URL.",
                        "Missing URL",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
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
                        _log.Debug($"CloneRepoAsync: Selected destination: {RepoPath}");
                    }
                    else
                    {
                        _log.Info("CloneRepoAsync: User cancelled folder selection");
                        return; // User cancelled
                    }
                }

                _log.Info($"CloneRepoAsync: Cloning from '{SourceUrl}' to '{RepoPath}'...");
                IsBusy = true;

                await Task.Run(() =>
                {
                    _gitService.CloneRepository(SourceUrl, RepoPath, AuthenticatedUser);
                    _log.Info("CloneRepoAsync: Repository cloned successfully");
                });

                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("Repository cloned successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    NavigateToWorkspace();
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Clone failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
                _log.Error($"CloneRepoAsync: Unexpected error: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}