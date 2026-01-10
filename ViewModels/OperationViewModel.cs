using GitWave.Controls;
using GitWave.Core;
using GitWave.Models;
using GitWave.Services;
using LibGit2Sharp;
using OpenTap;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;

namespace GitWave.ViewModels
{
    public class OperationViewModel : BaseViewModel
    {
        private readonly IGitService _git;
        private readonly CommitGraphViewModel _commitGraph;
        private readonly RepositoryWatcherService _repoWatcher;

        // --- State ---
        private string _repoPath = "";
        public string RepoPath
        {
            get => _repoPath;
            set => SetProperty(ref _repoPath, value);
        }

        private GitHubUser _authenticatedUser;
        public GitHubUser AuthenticatedUser
        {
            get => _authenticatedUser;
            set => SetProperty(ref _authenticatedUser, value);
        }

        private BranchInfo _selectedBranch;
        public BranchInfo SelectedBranch
        {
            get => _selectedBranch;
            set => SetProperty(ref _selectedBranch, value);
        }
        private string _newBranchName = "";
        public string NewBranchName
        {
            get => _newBranchName;
            set => SetProperty(ref _newBranchName, value);
        }
        private string _commitMessage = "";
        public string CommitMessage
        {
            get => _commitMessage;
            set => SetProperty(ref _commitMessage, value);
        }
        private string _currentBranch = "<none>";
        public string CurrentBranch
        {
            get => _currentBranch;
            private set => SetProperty(ref _currentBranch, value);
        }

        public CommitGraphViewModel CommitGraph
        {
            get => _commitGraph;
        }

        // --- Collections ---
        public ObservableCollection<CommitInfo> Commits { get; } = new ObservableCollection<CommitInfo>();
        public ObservableCollection<BranchInfo> Branches { get; } = new ObservableCollection<BranchInfo>();
        public ObservableCollection<ChangeItem> StagedChanges { get; } = new ObservableCollection<ChangeItem>();
        public ObservableCollection<ChangeItem> UnstagedChanges { get; } = new ObservableCollection<ChangeItem>();

        // --- Commands ---
        public ICommand ShowPullRequestsPageCommand { get; }
        public ICommand ShowRepositoryPageCommand { get; }

        // Branch Ops
        public ICommand LoadBranchesCommand { get; }
        public ICommand CheckoutBranchCommand { get; }
        public ICommand CreateBranchCommand { get; }
        public ICommand DeleteBranchCommand { get; }
        public ICommand MergeBranchCommand { get; }

        // File Ops
        public ICommand RefreshChangesCommand { get; }
        public ICommand StageCommand { get; }
        public ICommand UnstageCommand { get; }
        public ICommand ShowCommand { get; }
        public ICommand StageAllCommand { get; }
        public ICommand UnstageAllCommand { get; }
        public ICommand CommitCommand { get; }

        // Remote Ops
        public ICommand PullCommand { get; }
        public ICommand PushCommand { get; }

        public OperationViewModel(IGitService git, CommitGraphViewModel commitGraph, RepositoryWatcherService repoWatcher)
        {
            _git = git ?? throw new ArgumentNullException(nameof(git));
            _commitGraph = commitGraph ?? throw new ArgumentNullException(nameof(commitGraph));
            _repoWatcher = repoWatcher ?? throw new ArgumentNullException(nameof(repoWatcher));

            _repoWatcher.OnRepositoryChanged += HandleRepositoryChanged;

            // Branch operations
            LoadBranchesCommand = new RelayCommand(_ => ExecuteLoadBranches());
            CheckoutBranchCommand = new AsyncRelayCommand(_ => ExecuteCheckoutBranch(), _ => SelectedBranch != null);
            CreateBranchCommand = new AsyncRelayCommand(_ => ExecuteCreateBranch());
            MergeBranchCommand = new AsyncRelayCommand(_ => ExecuteMergeBranch(), _ => SelectedBranch != null);
            DeleteBranchCommand = new AsyncRelayCommand(_ => ExecuteDeleteBranch());

            // File changes
            StageCommand = new RelayCommand(p => ExecuteStageFile(p));
            UnstageCommand = new RelayCommand(p => ExecuteUnstageFile(p));
            ShowCommand = new RelayCommand(p => ExecuteShowDiff(p));
            StageAllCommand = new RelayCommand(_ => ExecuteStageAll());
            UnstageAllCommand = new RelayCommand(_ => ExecuteUnstageAll());
            CommitCommand = new AsyncRelayCommand(_ => ExecuteCommit());

            // Remote operations
            PullCommand = new AsyncRelayCommand(_ => ExecutePullCurrentBranch(), _ => !string.IsNullOrWhiteSpace(RepoPath));
            PushCommand = new AsyncRelayCommand(_ => ExecutePushCurrentBranch(), _ => !string.IsNullOrWhiteSpace(RepoPath));

            if (_git.IsRepositoryOpen)
            {
                _ = InitializeRepoAsync();
            }
        }

        // --- Initialization ---

        /// <summary>
        /// Called by RepositoryViewModel when a repo is opened/created.
        /// </summary>
        public async Task InitializeRepoAsync()
        {
            if (!_git.IsRepositoryOpen)
            {
                _repoWatcher.StopWatching();
                return;
            }
            // 2. Retrieve the active path from the service
            RepoPath = _git.GetRepositoryPath();
            AuthenticatedUser = _git.AuthenticatedUser;

            try
            {
                IsBusy = true;
                _repoWatcher.StopWatching();

                await Task.Run(() =>
                {
                    ExecuteLoadCommits();
                    ExecuteLoadBranches();
                    ExecuteRefreshChanges();
                });
                // 4. Load graph on background thread
                await _commitGraph.LoadAllBranchesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing repo: {ex.Message}");
                MessageBox.Show($"Error loading repository: {ex.Message}");
            }
            finally
            {
                if (!string.IsNullOrEmpty(RepoPath))
                {
                    _repoWatcher.StartWatching(RepoPath);
                }
                IsBusy = false;
            }
        }


        private void ExecuteLoadCommits()
        {
            try
            {
                // A. Fetch
                var logs = _git.GetCommitLog().ToList();

                // B. Update
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Commits.Clear();
                    foreach (var c in logs) Commits.Add(c);
                });
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        private void ExecuteLoadBranches()
        {
            try
            {
                // A. Fetch on Background Thread
                var branchList = _git.GetBranches().ToList();
                var currentName = branchList.FirstOrDefault(b => b.IsCurrent)?.Name ?? "<none>";

                // B. Update on UI Thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Branches.Clear();
                    foreach (var b in branchList) Branches.Add(b);
                    CurrentBranch = currentName;
                });
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        private async Task ExecuteCheckoutBranch()
        {
            try
            {
                IsBusy = true;
                _repoWatcher.StopWatching();

                await Task.Run(() =>
                {
                    _git.CheckoutBranch(SelectedBranch.Name);
                    ExecuteLoadCommits();
                    ExecuteLoadBranches();
                    ExecuteRefreshChanges();
                });
                await _commitGraph.RefreshAsync();
            }
            catch (Exception ex) { MessageBox.Show($"Checkout failed: {ex.Message}"); }
            finally
            {
                IsBusy = false;
                _repoWatcher.StartWatching(RepoPath);
            }
        }

        private async Task ExecuteCreateBranch()
        {
            if (string.IsNullOrWhiteSpace(NewBranchName))
            {
                MessageBox.Show("Please enter a branch name.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Capture the name to use inside the thread
                string branchName = NewBranchName;

                IsBusy = true;

                await Task.Run(() =>
                {
                    _git.CreateBranch(branchName);

                    ExecuteLoadBranches();
                    ExecuteLoadCommits();
                    ExecuteRefreshChanges();
                });

                NewBranchName = "";
                MessageBox.Show($"Branch '{branchName}' created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Create Branch failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteMergeBranch()
        {
            try
            {
                await Task.Run(() =>
                {
                    _git.MergeBranch(SelectedBranch.Name);
                    ExecuteLoadBranches();
                    ExecuteLoadCommits();
                    ExecuteRefreshChanges();
                });
                await _commitGraph.RefreshAsync();
                MessageBox.Show($"Merge successfully");
            }
            catch (Exception ex) { MessageBox.Show($"Merge failed: {ex.Message}"); }
        }

        private async Task ExecuteDeleteBranch()
        {
            if (SelectedBranch == null) return;

            // Prevent deleting current branch
            if (SelectedBranch.IsCurrent)
            {
                MessageBox.Show("Cannot delete the currently checked out branch.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Confirm deletion
            var result = MessageBox.Show(
                $"Are you sure you want to delete branch '{SelectedBranch.Name}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await Task.Run(() =>
                {
                    _git.DeleteBranch(SelectedBranch.Name);
                    MessageBox.Show($"Branch '{SelectedBranch.Name}' deleted successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    ExecuteLoadBranches();
                    ExecuteLoadCommits();
                    ExecuteRefreshChanges();
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Delete branch failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteStageFile(object? parameter)
        {
            if (parameter is ChangeItem item)
            {
                _git.StageFile(item.FilePath);
                ExecuteRefreshChanges();
            }
        }

        private void ExecuteUnstageFile(object? parameter)
        {
            if (parameter is ChangeItem item)
            {
                _git.UnstageFile(item.FilePath);
                ExecuteRefreshChanges();
            }
        }

        private void ExecuteStageAll()
        {
            // Snapshot the list to avoid modification exceptions during iteration
            var items = UnstagedChanges.ToList();
            foreach (var item in items)
            {
                _git.StageFile(item.FilePath);
            }
            ExecuteRefreshChanges();
        }

        private void ExecuteUnstageAll()
        {
            var items = StagedChanges.ToList();
            foreach (var item in items)
            {
                _git.UnstageFile(item.FilePath);
            }
            ExecuteRefreshChanges();
        }

        private async Task ExecuteCommit()
        {
            try
            {
                IsBusy = true;
                if (string.IsNullOrWhiteSpace(CommitMessage))
                {
                    MessageBox.Show("Commit message cannot be empty.");
                    return;
                }
                await Task.Run(() => _git.Commit(CommitMessage));
                CommitMessage = "";
                ExecuteLoadCommits();
                ExecuteRefreshChanges();
                await _commitGraph.RefreshAsync();
            }
            catch (Exception ex) { MessageBox.Show($"Commit failed: {ex.Message}"); }
            finally { IsBusy = false; }
        }

        private void ExecuteRefreshChanges()
        {
            try
            {
                // A. Fetch
                var (staged, unstaged) = _git.GetChanges();

                // Snapshot to lists to prevent enumeration issues
                var stagedList = staged.ToList();
                var unstagedList = unstaged.ToList();

                // B. Update
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StagedChanges.Clear();
                    UnstagedChanges.Clear();
                    foreach (var s in stagedList) StagedChanges.Add(s);
                    foreach (var u in unstagedList) UnstagedChanges.Add(u);
                });
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        private void HandleRepositoryChanged(string repositoryPath)
        {
            Debug.WriteLine($"[OPS] Repository changed, refreshing view...");

            // Refresh on UI thread
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    ExecuteRefreshChanges();
                    Debug.WriteLine($"[OPS] ✅ Repository view refreshed");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[OPS] ❌ Error refreshing view: {ex.Message}");
                }
            });
        }

        private async Task ExecutePullCurrentBranch()
        {
            try
            {
                IsBusy = true;
                _repoWatcher.StopWatching();

                await Task.Run(() =>
                {
                    _git.PullCurrentBranch(RepoPath, _git.AuthenticatedUser);

                    // Refresh Data
                    ExecuteLoadCommits();
                    ExecuteLoadBranches();
                    ExecuteRefreshChanges();
                });

                await _commitGraph.RefreshAsync();

                // Success Feedback
                MessageBox.Show("Pull completed successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Pull failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _repoWatcher.StartWatching(RepoPath);
                IsBusy = false;
            }
        }

        private async Task ExecutePushCurrentBranch()
        {
            try
            {
                IsBusy = true;

                await Task.Run(() => _git.PushCurrentBranch(RepoPath, _git.AuthenticatedUser));

                // Refresh on UI Thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ExecuteLoadCommits();
                    ExecuteLoadBranches();
                });

                await _commitGraph.RefreshAsync();

                // Success Feedback
                MessageBox.Show("Push completed successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Push failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false; // Hides the Progress Bar
            }
        }

        private void ExecuteShowDiff(object? parameter)
        {
            // Diff logic remains identical to previous implementation
            var item = parameter as ChangeItem;
            if (item == null) return;

            if (!item.FilePath.EndsWith(".TapPlan", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Diff Viewer currently supports only .TapPlan files.", "Unsupported File Type", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var rel = item.FilePath.Replace('\\', '/').TrimStart('/');

                // Re-open repo locally for lightweight access
                using var repo = new Repository(RepoPath);

                TestPlan? before = null;
                TestPlan? after = null;

                if (item.IsStaged)
                {
                    before = TestPlanHelper.GetTestPlanFromHEAD(repo, rel);
                    after = TestPlanHelper.GetTestPlanFromIndex(repo, rel) ?? TestPlanHelper.GetTestPlanFromWorkingDirectory(repo, rel);
                }
                else
                {
                    before = TestPlanHelper.GetTestPlanFromIndex(repo, rel) ?? TestPlanHelper.GetTestPlanFromHEAD(repo, rel);
                    after = TestPlanHelper.GetTestPlanFromWorkingDirectory(repo, rel);
                }

                if (before == null && after == null)
                {
                    MessageBox.Show("Unable to load either side for diff.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var vm = new DiffViewerViewModel();
                vm.Load(before, after);

                var win = new DiffViewerWindow(vm) { Owner = System.Windows.Application.Current.MainWindow };
                win.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Diff failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}