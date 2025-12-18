using GitWave.Controls;
using GitWave.Core;
using GitWave.Models;
using LibGit2Sharp;
using OpenTap;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;

namespace GitWave.ViewModels
{
    public class CommitRowViewModel : BaseViewModel
    {
        private readonly IGitService _gitService;
        private readonly Repository _repo;  // ← Store repository

        // --- Commit Data ---
        public required string Sha { get; init; }
        public required string ShortSha { get; init; }
        public required string Message { get; init; }
        public required string Author { get; init; }
        public required string AuthorEmail { get; init; }
        public required string Committer { get; init; }
        public required string CommitterEmail { get; init; }
        public required DateTime CommitDate { get; init; } = DateTime.Now;
        public required IReadOnlyList<string> Parents { get; init; }

        // --- Graph Layout ---
        public int Row { get; set; }
        public int PrimaryLane { get; set; }
        public int BranchColorIndex { get; set; }
        public List<GraphSegment> Segments { get; set; } = new();
        public bool HasIncomingConnection { get; set; }
        public bool HasOutgoingConnection { get; set; }

        // --- Derived State ---
        public bool IsRootCommit => Parents.Count == 0;

        // --- UI Expansion ---
        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (SetProperty(ref _isExpanded, value) && value)
                {
                    LoadChangedFiles();
                }
            }
        }

        private bool _filesLoaded;

        public string ParentsString => string.Join(", ", Parents.Select(p => p[..7]));
        public ObservableCollection<FileChangeViewModel> ChangedFiles { get; } = new();

        private FileChangeViewModel? _selectedChange;
        public FileChangeViewModel? SelectedChange
        {
            get => _selectedChange;
            set => SetProperty(ref _selectedChange, value);
        }

        public ICommand ViewFileCommand { get; }

        /// <summary>
        /// Constructor - get repository once and store it
        /// ✅ Only ONE repository instance per ViewModel
        /// </summary>
        public CommitRowViewModel(IGitService gitService)
        {
            _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
            _repo = _gitService.GetRepository();  // ← Get once here
            ViewFileCommand = new RelayCommand(param => ExecuteViewFileDiff(param as FileChangeViewModel));
        }

        /// <summary>
        /// Loads the list of changed files for this commit.
        /// ✅ SIMPLIFIED: No lock needed, no background thread needed
        /// ✅ Uses stored repository instance
        /// </summary>
        private void LoadChangedFiles()
        {
            if (_filesLoaded)
                return;

            _filesLoaded = true;

            try
            {
                var commit = _repo.Lookup<Commit>(Sha);
                if (commit == null)
                    return;

                TreeChanges changes;

                if (Parents.Count == 0)
                {
                    // Root commit: compare with empty tree
                    changes = _repo.Diff.Compare<TreeChanges>(null, commit.Tree);
                }
                else
                {
                    // Normal commit: compare with first parent
                    var parent = _repo.Lookup<Commit>(Parents[0]);
                    if (parent == null)
                        return;

                    changes = _repo.Diff.Compare<TreeChanges>(parent.Tree, commit.Tree);
                }

                // Clear and populate changed files
                ChangedFiles.Clear();
                foreach (var change in changes)
                {
                    ChangedFiles.Add(new FileChangeViewModel
                    {
                        FilePath = change.Path,
                        Status = GetStatusString(change.Status),
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading changed files: {ex.Message}");
            }
        }

        /// <summary>
        /// Maps LibGit2Sharp ChangeKind to a simple status string (A/M/D).
        /// </summary>
        private FileChangeStatus GetStatusString(ChangeKind kind)
        {
            return kind switch
            {
                ChangeKind.Added => FileChangeStatus.Added,
                ChangeKind.Deleted => FileChangeStatus.Deleted,
                ChangeKind.Modified => FileChangeStatus.Modified,
                ChangeKind.Renamed => FileChangeStatus.Modified,
                ChangeKind.Copied => FileChangeStatus.Added,
                ChangeKind.TypeChanged => FileChangeStatus.Modified,
                _ => FileChangeStatus.Modified
            };
        }

        /// <summary>
        /// Opens a diff viewer for a specific file in this commit.
        /// ✅ SIMPLIFIED: Uses stored repository
        /// </summary>
        private void ExecuteViewFileDiff(FileChangeViewModel? file)
        {
            if (file == null)
                return;

            if (!file.FilePath.EndsWith(".TapPlan", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Diff Viewer currently supports only .TapPlan files.",
                    "Unsupported File Type",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                TestPlan? before = null;
                TestPlan? after = null;

                // ✅ Use stored repository - no need to create/dispose
                var commit = _repo.Lookup<Commit>(Sha);
                if (commit == null)
                {
                    MessageBox.Show("Commit not found.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (IsRootCommit)
                {
                    after = TestPlanHelper.LoadTestPlanFromCommit(_repo, commit, file.FilePath);
                }
                else
                {
                    var parent = _repo.Lookup<Commit>(Parents[0]);
                    if (parent == null)
                    {
                        MessageBox.Show("Parent commit not found.", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    before = TestPlanHelper.LoadTestPlanFromCommit(_repo, parent, file.FilePath);
                    after = TestPlanHelper.LoadTestPlanFromCommit(_repo, commit, file.FilePath);
                }

                if (before == null && after == null)
                {
                    MessageBox.Show("Unable to load either side for diff.\nCheck plugin discovery or file path.",
                        "Diff Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var vm = new DiffViewerViewModel();
                vm.Load(before, after);

                using (var win = new DiffViewerWindow(vm) { Owner = System.Windows.Application.Current.MainWindow })
                {
                    win.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Diff error: {ex}");
                MessageBox.Show("Diff failed:\n" + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}