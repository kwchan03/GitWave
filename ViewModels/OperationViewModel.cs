using GitWave.Controls;
using GitWave.Core;
using GitWave.Models;
using GitWave.UI.Pages;
using LibGit2Sharp;
using Microsoft.Extensions.DependencyInjection;
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

        public GitHubUser AuthenticatedUser { get; set; }

        // --- State ---
        private string _repoPath = "";
        public string RepoPath
        {
            get => _repoPath;
            set => SetProperty(ref _repoPath, value);
        }

        public BranchInfo SelectedBranch { get; set; }
        public string NewBranchName { get; set; } = "";
        public string CommitMessage { get; set; } = "";
        private string _currentBranch = "<none>";
        public string CurrentBranch
        {
            get => _currentBranch;
            private set => SetProperty(ref _currentBranch, value);
        }

        public CommitGraphViewModel Graph { get; }

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

        public OperationViewModel(IGitService git)
        {
            _git = git ?? throw new ArgumentNullException(nameof(git));
            Graph = new CommitGraphViewModel(_git);

            // Navigation
            ShowPullRequestsPageCommand = new RelayCommand(_ => NavigateTo<PullRequestPage>());
            ShowRepositoryPageCommand = new RelayCommand(_ => NavigateTo<RepositoryPage>());

            // Branch operations
            LoadBranchesCommand = new RelayCommand(_ => ExecuteLoadBranches());
            CheckoutBranchCommand = new AsyncRelayCommand(_ => ExecuteCheckoutBranch(), _ => SelectedBranch != null);
            CreateBranchCommand = new RelayCommand(_ => ExecuteCreateBranch());
            MergeBranchCommand = new RelayCommand(_ => ExecuteMergeBranch(), _ => SelectedBranch != null);

            // File changes
            RefreshChangesCommand = new RelayCommand(_ => ExecuteRefreshChanges());
            StageCommand = new RelayCommand(p => ExecuteStageFile(p));
            UnstageCommand = new RelayCommand(p => ExecuteUnstageFile(p));
            ShowCommand = new RelayCommand(p => ExecuteShowDiff(p));
            StageAllCommand = new RelayCommand(_ => ExecuteStageAll());
            UnstageAllCommand = new RelayCommand(_ => ExecuteUnstageAll());
            CommitCommand = new AsyncRelayCommand(_ => ExecuteCommit());

            // Remote operations
            PullCommand = new AsyncRelayCommand(_ => ExecutePullCurrentBranch(), _ => !string.IsNullOrWhiteSpace(RepoPath));
            PushCommand = new AsyncRelayCommand(_ => ExecutePushCurrentBranch(), _ => !string.IsNullOrWhiteSpace(RepoPath));
        }

        // --- Initialization ---

        /// <summary>
        /// Called by RepositoryViewModel when a repo is opened/created.
        /// </summary>
        public async void InitializeRepo()
        {
            if (string.IsNullOrEmpty(RepoPath)) return;

            try
            {
                IsBusy = true;

                // Ensure service knows about the repo
                _git.OpenRepository(RepoPath);

                // Load UI Data
                ExecuteLoadCommits();
                ExecuteLoadBranches();
                ExecuteRefreshChanges();

                // Load Graph
                await Graph.LoadAllBranchesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing repo: {ex.Message}");
                MessageBox.Show($"Error loading repository: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // --- Actions ---

        private void NavigateTo<T>() where T : System.Windows.Controls.Page
        {
            var main = App.Services.GetRequiredService<MainWindow>();
            var page = App.Services.GetRequiredService<T>();
            main.NavigateTo(page);
        }

        private void ExecuteLoadCommits()
        {
            try
            {
                Commits.Clear();
                foreach (var c in _git.GetCommitLog()) Commits.Add(c);
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        private void ExecuteLoadBranches()
        {
            try
            {
                Branches.Clear();
                foreach (var b in _git.GetBranches()) Branches.Add(b);
                var current = Branches.FirstOrDefault(b => b.IsCurrent);
                CurrentBranch = current?.Name ?? "<none>";
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        private async Task ExecuteCheckoutBranch()
        {
            try
            {
                IsBusy = true;
                _git.CheckoutBranch(SelectedBranch.Name);

                // Refresh everything
                ExecuteLoadBranches();
                ExecuteLoadCommits();
                ExecuteRefreshChanges();
                await Graph.RefreshAsync();
            }
            catch (Exception ex) { MessageBox.Show($"Checkout failed: {ex.Message}"); }
            finally { IsBusy = false; }
        }

        private void ExecuteCreateBranch()
        {
            try
            {
                _git.CreateBranch(NewBranchName);
                NewBranchName = "";
                ExecuteLoadBranches();
                ExecuteLoadCommits();
                ExecuteRefreshChanges();
            }
            catch (Exception ex) { MessageBox.Show($"Create Branch failed: {ex.Message}"); }
        }

        private void ExecuteMergeBranch()
        {
            try
            {
                _git.MergeBranch(SelectedBranch.Name);
                ExecuteLoadBranches();
                ExecuteLoadCommits();
                ExecuteRefreshChanges();
            }
            catch (Exception ex) { MessageBox.Show($"Merge failed: {ex.Message}"); }
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
                _git.Commit(CommitMessage);
                CommitMessage = "";
                ExecuteLoadCommits();
                ExecuteRefreshChanges();
                await Graph.RefreshAsync();
            }
            catch (Exception ex) { MessageBox.Show($"Commit failed: {ex.Message}"); }
            finally { IsBusy = false; }
        }

        private void ExecuteRefreshChanges()
        {
            try
            {
                StagedChanges.Clear();
                UnstagedChanges.Clear();
                var (staged, unstaged) = _git.GetChanges();
                foreach (var s in staged) StagedChanges.Add(s);
                foreach (var u in unstaged) UnstagedChanges.Add(u);
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        private async Task ExecutePullCurrentBranch()
        {
            try
            {
                await Task.Run(() => _git.PullCurrentBranch(RepoPath, AuthenticatedUser));
                ExecuteLoadCommits();
                ExecuteRefreshChanges();
                await Graph.RefreshAsync();
            }
            catch (Exception ex) { MessageBox.Show($"Pull failed: {ex.Message}"); }
        }

        private async Task ExecutePushCurrentBranch()
        {
            try
            {
                await Task.Run(() => _git.PushCurrentBranch(RepoPath, AuthenticatedUser));
                await Graph.RefreshAsync();
            }
            catch (Exception ex) { MessageBox.Show($"Push failed: {ex.Message}"); }
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