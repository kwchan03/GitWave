using GitGUI.Controls;
using GitGUI.Core;
using GitGUI.Models;
using GitGUI.Services;
using LibGit2Sharp;
using OpenTap;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace GitGUI.ViewModels
{
    public class OperationViewModel : BaseViewModel
    {
        private readonly IGitService _git;

        // ---------------------------
        // GitHub OAuth
        // ---------------------------
        private GitHubUser _authenticatedUser;
        public GitHubUser AuthenticatedUser
        {
            get => _authenticatedUser;
            set => SetProperty(ref _authenticatedUser, value);
        }

        // ---------------------------
        // Repository / Branch / Commit fields
        // ---------------------------
        private string _repoPath = "";
        public string RepoPath
        {
            get => _repoPath;
            set => SetProperty(ref _repoPath, value);
        }
        public string SourceUrl { get; set; } = "";
        public BranchInfo SelectedBranch { get; set; }
        public string NewBranchName { get; set; } = "";
        public string CommitMessage { get; set; } = "";
        public ChangeItem SelectedChange { get; set; }
        private string _currentBranch = "<none>";
        public string CurrentBranch
        {
            get => _currentBranch;
            private set => SetProperty(ref _currentBranch, value);
        }


        // ---------------------------
        // Collections
        // ---------------------------
        public ObservableCollection<CommitInfo> Commits { get; } = new ObservableCollection<CommitInfo>();
        public ObservableCollection<BranchInfo> Branches { get; } = new ObservableCollection<BranchInfo>();
        public ObservableCollection<ChangeItem> StagedChanges { get; } = new ObservableCollection<ChangeItem>();
        public ObservableCollection<ChangeItem> UnstagedChanges { get; } = new ObservableCollection<ChangeItem>();

        // ---------------------------
        // UI State / Logging
        // ---------------------------
        private string _outputLog = "";
        public string OutputLog
        {
            get => _outputLog;
            private set => SetProperty(ref _outputLog, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private const int MaxLogLength = 10000;
        private const int MaxLogLines = 1000;

        // ---------------------------
        // Commands
        // ---------------------------
        public ICommand BrowseFolderCommand { get; }
        public ICommand OpenRepoCommand { get; }
        public ICommand CreateRepoCommand { get; }
        public ICommand LoadBranchesCommand { get; }
        public ICommand CheckoutBranchCommand { get; }
        public ICommand CreateBranchCommand { get; }
        public ICommand MergeBranchCommand { get; }
        public ICommand RefreshChangesCommand { get; }
        public ICommand StageCommand { get; }
        public ICommand UnstageCommand { get; }
        public ICommand CommitCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand CloneRepoCommand { get; }
        public ICommand PullCommand { get; }
        public ICommand PushCommand { get; }
        public ICommand ShowCommand { get; }

        // ---------------------------
        // Constructor
        // ---------------------------
        public OperationViewModel(IGitService git)
        {
            _git = git ?? throw new ArgumentNullException(nameof(git));

            // ---------------------------
            // Repo operations
            // ---------------------------
            BrowseFolderCommand = new RelayCommand(_ => ExecuteBrowseFolder());
            OpenRepoCommand = new AsyncRelayCommand(_ => ExecuteOpenRepo());
            CreateRepoCommand = new AsyncRelayCommand(_ => ExecuteCreateRepo(), _ => !string.IsNullOrWhiteSpace(RepoPath));
            ClearLogCommand = new RelayCommand(_ => ClearLog());

            // ---------------------------
            // Branch operations
            // ---------------------------
            LoadBranchesCommand = new RelayCommand(_ => ExecuteLoadBranches());
            CheckoutBranchCommand = new AsyncRelayCommand(_ => ExecuteCheckoutBranch(), _ => SelectedBranch != null);
            CreateBranchCommand = new RelayCommand(_ => ExecuteCreateBranch(), _ => !string.IsNullOrWhiteSpace(NewBranchName));
            MergeBranchCommand = new RelayCommand(_ => ExecuteMergeBranch(), _ => SelectedBranch != null);

            // ---------------------------
            // File changes
            // ---------------------------
            RefreshChangesCommand = new RelayCommand(_ => ExecuteRefreshChanges());
            StageCommand = new RelayCommand(_ => ExecuteStageFile(), _ => SelectedChange != null);
            UnstageCommand = new RelayCommand(_ => ExecuteUnstageFile(), _ => SelectedChange != null);
            CommitCommand = new AsyncRelayCommand(_ => ExecuteCommit(), _ => !string.IsNullOrWhiteSpace(CommitMessage));

            // ---------------------------
            // GitHub operations
            // ---------------------------
            CloneRepoCommand = new AsyncRelayCommand(_ => ExecuteCloneRepo(), _ =>
                !string.IsNullOrWhiteSpace(SourceUrl) &&
                AuthenticatedUser != null);

            PullCommand = new AsyncRelayCommand(_ => ExecutePullCurrentBranch(), _ =>
                !string.IsNullOrWhiteSpace(RepoPath) && AuthenticatedUser != null);

            PushCommand = new AsyncRelayCommand(_ => ExecutePushCurrentBranch(), _ =>
                !string.IsNullOrWhiteSpace(RepoPath) && AuthenticatedUser != null);

            ShowCommand = new RelayCommand(_ => ExecuteShowDiff(), _ => SelectedChange != null);

        }

        // ---------------------------
        // Logging
        // ---------------------------
        public void ClearLog()
        {
            OutputLog = "";
            AppendLog("Log cleared");
        }

        private void AppendLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logEntry = $"{timestamp} – {message}{Environment.NewLine}";

            if (OutputLog.Length + logEntry.Length > MaxLogLength)
            {
                var lines = OutputLog.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
                var trimmed = lines.Skip(Math.Max(0, lines.Length - MaxLogLines)).ToArray();
                OutputLog = string.Join(Environment.NewLine, trimmed) + Environment.NewLine;
            }

            OutputLog += logEntry;
            OnPropertyChanged(nameof(OutputLog));
        }

        // ---------------------------
        // Helper for all actions
        // ---------------------------
        private void ExecuteAction(Action action, string successMessage = null, Action onSuccess = null)
        {
            try
            {
                IsBusy = true;
                action?.Invoke();
                if (!string.IsNullOrEmpty(successMessage))
                    AppendLog(successMessage);
                onSuccess?.Invoke();
            }
            catch (Exception ex)
            {
                AppendLog($"Error: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ---------------------------
        // Repo / Branch / File Operations
        // ---------------------------
        private void ExecuteBrowseFolder()
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Select or create a folder",
                ShowNewFolderButton = true
            };
            if (dlg.ShowDialog() == DialogResult.OK)
                RepoPath = dlg.SelectedPath;
        }
        private async Task ExecuteOpenRepo()
        {
            try
            {
                IsBusy = true;
                var exists = _git.OpenRepository(RepoPath);
                _git.SetRepoUserFromAuthenticatedUser(AuthenticatedUser);

                ExecuteLoadCommits();
                ExecuteLoadBranches();
                ExecuteRefreshChanges();

                // ✅ await: load the commit graph
                await Graph.InitializeAsync(RepoPath, branchName: null);
                AppendLog($"Opened repository at {RepoPath}");
            }
            catch (Exception ex) { AppendLog($"Error: {ex.Message}"); }
            finally { IsBusy = false; }
        }

        private async Task ExecuteCreateRepo()
        {
            try
            {
                IsBusy = true;
                _git.CreateRepository(RepoPath);
                _git.SetRepoUserFromAuthenticatedUser(AuthenticatedUser);

                ExecuteLoadCommits();
                ExecuteLoadBranches();
                ExecuteRefreshChanges();

                // ✅ await
                await Graph.InitializeAsync(RepoPath, branchName: null);
                AppendLog($"Created repository at {RepoPath}");
            }
            catch (Exception ex) { AppendLog($"Error: {ex.Message}"); }
            finally { IsBusy = false; }
        }

        private void ExecuteLoadCommits() => ExecuteAction(() =>
        {
            Commits.Clear();
            foreach (var c in _git.GetCommitLog())
                Commits.Add(c);
            AppendLog($"Loaded {Commits.Count} commits.");
        });

        private void ExecuteLoadBranches() => ExecuteAction(() =>
        {
            Branches.Clear();
            foreach (var b in _git.GetBranches()) Branches.Add(b);
            var current = Branches.FirstOrDefault(b => b.IsCurrent);
            CurrentBranch = current?.Name ?? "<none>";
        });

        private async Task ExecuteCheckoutBranch()
        {
            try
            {
                IsBusy = true;
                _git.CheckoutBranch(SelectedBranch.Name);
                CurrentBranch = (_git as GitLibService)?.CurrentBranchName ?? SelectedBranch.Name.Replace("origin/", "");
                ExecuteLoadBranches();
                ExecuteLoadCommits();
                ExecuteRefreshChanges();

                // ✅ await: show new branch history
                await Graph.ChangeBranchAsync(CurrentBranch);
                AppendLog($"Checked out {SelectedBranch?.Name}");
            }
            catch (Exception ex) { AppendLog($"Error: {ex.Message}"); }
            finally { IsBusy = false; }
        }

        private void ExecuteCreateBranch() => ExecuteAction(() =>
        {
            _git.CreateBranch(NewBranchName);
            CurrentBranch = NewBranchName;            // New branch is now current
            NewBranchName = "";                        // Clear the input
            ExecuteLoadBranches();                     // Refresh branch list
            ExecuteLoadCommits();                      // Refresh commits
            ExecuteRefreshChanges();                   // Refresh staged/unstaged changes
        }, $"Created branch {CurrentBranch}");

        // Merge the selected branch into current
        private void ExecuteMergeBranch() => ExecuteAction(() =>
        {
            if (SelectedBranch != null)
            {
                _git.MergeBranch(SelectedBranch.Name);
                ExecuteLoadBranches();                 // Refresh branch list
                ExecuteLoadCommits();                  // Refresh commits
                ExecuteRefreshChanges();               // Refresh staged/unstaged changes
            }
        }, $"Merged branch {SelectedBranch?.Name}");

        // Stage the selected file
        private void ExecuteStageFile() => ExecuteAction(() =>
        {
            if (SelectedChange != null)
                _git.StageFile(SelectedChange.FilePath);
            ExecuteRefreshChanges();                   // Refresh staged/unstaged changes
        }, SelectedChange != null ? $"Staged {SelectedChange.FilePath}" : null);

        // Unstage the selected file
        private void ExecuteUnstageFile() => ExecuteAction(() =>
        {
            if (SelectedChange != null)
                _git.UnstageFile(SelectedChange.FilePath);
            ExecuteRefreshChanges();                   // Refresh staged/unstaged changes
        }, SelectedChange != null ? $"Unstaged {SelectedChange.FilePath}" : null);

        // Commit the current message
        private async Task ExecuteCommit()
        {
            try
            {
                IsBusy = true;
                _git.Commit(CommitMessage);
                CommitMessage = "";
                ExecuteLoadCommits();
                ExecuteRefreshChanges();

                // ✅ await
                await Graph.RefreshAsync();
                AppendLog($"Committed");
            }
            catch (Exception ex) { AppendLog($"Error: {ex.Message}"); }
            finally { IsBusy = false; }
        }

        private void ExecuteRefreshChanges() => ExecuteAction(() =>
        {
            StagedChanges.Clear();
            UnstagedChanges.Clear();
            var (staged, unstaged) = _git.GetChanges();
            foreach (var s in staged) StagedChanges.Add(s);
            foreach (var u in unstaged) UnstagedChanges.Add(u);
        });

        // ---------------------------
        // GitHub Operations
        // ---------------------------
        private async Task ExecuteCloneRepo()
        {
            if (AuthenticatedUser == null || string.IsNullOrWhiteSpace(AuthenticatedUser.AccessToken))
            {
                AppendLog("Cannot clone: not authenticated with GitHub.");
                return;
            }

            if (string.IsNullOrWhiteSpace(SourceUrl))
            {
                AppendLog("Source URL is empty.");
                return;
            }

            // Ask user for destination folder
            string? parentDir = null;
            using (var dlg = new FolderBrowserDialog
            {
                Description = "Select a folder to place the new repository subfolder in",
                ShowNewFolderButton = true
            })
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                    parentDir = dlg.SelectedPath;
            }

            if (string.IsNullOrWhiteSpace(parentDir))
            {
                AppendLog("Clone cancelled: no destination folder selected.");
                return;
            }

            IsBusy = true;
            AppendLog($"Cloning {SourceUrl} into {parentDir}...");

            try
            {
                // Run cloning on a background thread to avoid freezing UI
                await Task.Run(() =>
                {
                    _git.CloneRepository(SourceUrl, parentDir, AuthenticatedUser);
                });

                AppendLog($"Successfully cloned {SourceUrl} to {parentDir}.");

                // Refresh branches, commits, and changes after cloning
                ExecuteLoadBranches();
                ExecuteLoadCommits();
                ExecuteRefreshChanges();

                await Graph.InitializeAsync(RepoPath, branchName: null);
            }
            catch (Exception ex)
            {
                AppendLog($"Error cloning repository: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecutePullCurrentBranch()
        {
            await Task.Run(() => _git.PullCurrentBranch(RepoPath, AuthenticatedUser));
            AppendLog("Pulled current branch");
            ExecuteLoadCommits();
            ExecuteRefreshChanges();
            await Graph.RefreshAsync(); // ✅
        }

        private async Task ExecutePushCurrentBranch()
        {
            await Task.Run(() => _git.PushCurrentBranch(RepoPath, AuthenticatedUser));
            AppendLog("Pushed current branch");
            // (graph usually unchanged on push, but harmless)
            await Graph.RefreshAsync(); // optional
        }

        private void ExecuteShowDiff()
        {
            if (SelectedChange == null)
            {
                AppendLog("No file selected.");
                return;
            }

            // ✅ Only allow .TapPlan files
            if (!SelectedChange.FilePath.EndsWith(".TapPlan", StringComparison.OrdinalIgnoreCase))
            {
                AppendLog("Selected file is not a TestPlan (.TapPlan).");
                System.Windows.MessageBox.Show("Diff Viewer currently supports only .TapPlan files.",
                                "Unsupported File Type",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var rel = SelectedChange.FilePath.Replace('\\', '/').TrimStart('/');
                var repoPath = RepoPath;

                TestPlan? before = null;
                TestPlan? after = null;

                using var repo = new Repository(repoPath);

                // ✅ Smart logic for which sides to compare
                if (SelectedChange.IsStaged)
                {
                    // HEAD → INDEX
                    before = GitHelper.GetTestPlanFromHEAD(repo, rel);
                    after = GitHelper.GetTestPlanFromIndex(repo, rel)
                         ?? GitHelper.GetTestPlanFromWorkingDirectory(repo, rel);
                    AppendLog($"Diff (HEAD → INDEX): {rel}");
                }
                else
                {
                    // INDEX → WORKDIR
                    before = GitHelper.GetTestPlanFromIndex(repo, rel)
                         ?? GitHelper.GetTestPlanFromHEAD(repo, rel);
                    after = GitHelper.GetTestPlanFromWorkingDirectory(repo, rel);
                    AppendLog($"Diff (INDEX → WORKDIR): {rel}");
                }

                if (before == null && after == null)
                {
                    AppendLog("Unable to load either side for diff.");
                    System.Windows.MessageBox.Show("Unable to load either side for diff.\nCheck plugin discovery or file path.",
                                    "Diff Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Compute semantic diff
                var diffs = TestPlanDiffService.ComparePlans(before, after);
                // debug
                foreach (var d in diffs.Where(d => d.Kind == StepChangeKind.Modified))
                {
                    var names = string.Join(", ", d.PropertyChanges.Select(pc => pc.Name));
                    System.Diagnostics.Debug.WriteLine($"Modified step {d.DisplayName}: {names}");
                }

                var vm = new DiffViewerTreeViewModel();
                vm.Load(before, after);
                System.Diagnostics.Debug.WriteLine($"Before count: {vm.BeforeRoots.Count}, After count: {vm.AfterRoots.Count}");

                // show
                var win = new DiffViewerWindow(vm) { Owner = System.Windows.Application.Current.MainWindow };
                win.ShowDialog();
            }
            catch (Exception ex)
            {
                AppendLog("Diff error: " + ex.Message);
                System.Windows.MessageBox.Show("Diff failed:\n" + ex, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public CommitGraphVM Graph { get; } = new CommitGraphVM();


    }
}
