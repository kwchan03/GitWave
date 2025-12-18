using GitWave.Controls;
using GitWave.Services;
using Octokit;
using OpenTap;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using MessageBox = System.Windows.MessageBox;

namespace GitWave.ViewModels
{
    public class PullRequestItemViewModel : BaseViewModel
    {
        private enum ReviewDecision
        {
            Approve,
            RequestChanges,
            Comment
        }
        private readonly GitHubApiService _apiService;
        private readonly string _owner;
        private readonly string _repo;
        private readonly PullRequest _model;

        // --- 1. Header & Status Data ---
        public int Number => _model.Number;
        public string Title => _model.Title;
        public string Author => _model.User.Login;

        private string _status;
        public string Status
        {
            get => _status;
            set
            {
                if (SetProperty(ref _status, value))
                {
                    OnPropertyChanged(nameof(StatusColor));
                    OnPropertyChanged(nameof(IsOpen));
                }
            }
        }

        public Brush StatusColor => Status == "Open" ? Brushes.Green : Brushes.Gray;
        public bool IsOpen => Status == "Open";

        // --- 2. Comment Input ---
        // Matches XAML Line 58: Text="{Binding CommentText}"
        private string _commentText;
        public string CommentText
        {
            get => _commentText;
            set => SetProperty(ref _commentText, value);
        }
        public event Func<Task> OnRequestRefresh;

        // --- 3. Child VMs ---
        public ConversationViewModel Conversation { get; }
        public ObservableCollection<CommitNodeViewModel> Commits { get; } = new ObservableCollection<CommitNodeViewModel>();

        // --- 4. Commands ---
        public ICommand MergeCommand { get; }
        public ICommand ClosePrCommand { get; }
        public ICommand CommentCommand { get; }
        public ICommand ApproveCommand { get; }
        public ICommand RequestChangesCommand { get; }
        public ICommand ViewCommitFileDiffCommand { get; }

        public PullRequestItemViewModel(GitHubApiService apiService, string owner, string repo, PullRequest prModel)
        {
            _apiService = apiService;
            _model = prModel;
            _owner = owner;
            _repo = repo;

            // Status Init
            _status = prModel.State.StringValue.Substring(0, 1).ToUpper() + prModel.State.StringValue.Substring(1);

            Conversation = new ConversationViewModel(apiService, owner, repo, prModel.Number);

            // Initialize Commands
            MergeCommand = new AsyncRelayCommand(_ => MergeAsync());
            ClosePrCommand = new AsyncRelayCommand(_ => ClosePrAsync());

            // Each button triggers the same submit logic but with a different decision
            CommentCommand = new AsyncRelayCommand(_ => SubmitReviewAsync(ReviewDecision.Comment));
            ApproveCommand = new AsyncRelayCommand(_ => SubmitReviewAsync(ReviewDecision.Approve));
            RequestChangesCommand = new AsyncRelayCommand(_ => SubmitReviewAsync(ReviewDecision.RequestChanges));
        }

        // --- 5. Action Logic ---
        public async Task LoadCommitsAndFilesAsync()
        {
            try
            {
                // Fetch commits
                var commits = await _apiService.GetCommits(_owner, _repo, Number);

                // Update UI on Main Thread
                Commits.Clear();

                // Process each commit and fetch its files
                foreach (var c in commits)
                {
                    var commitVM = new CommitNodeViewModel(_apiService, _owner, _repo)
                    {
                        Message = c.Commit.Message,
                        Sha = c.Sha,
                        ParentSha = c.Parents.FirstOrDefault()?.Sha ?? string.Empty,
                    };

                    await commitVM.LoadFilesAsync();
                    Commits.Add(commitVM);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading commits: {ex.Message}");
            }
        }

        private async void ExecuteViewFileDiff(FileChangeViewModel? file)
        {
            if (file == null) return;

            // 1. Check File Extension (Identical to reference)
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

                // 2. Fetch Content from GitHub API
                //    (We fetch raw strings instead of blobs)
                var tOld = _apiService.GetFileContentAsync(_owner, _repo, _model.Base.Sha, file.FilePath);
                var tNew = _apiService.GetFileContentAsync(_owner, _repo, _model.Head.Sha, file.FilePath);

                await Task.WhenAll(tOld, tNew);

                // 3. Parse Strings into TestPlans
                //    (Replaces GitHelper.LoadTestPlanFromCommit)
                if (!string.IsNullOrEmpty(await tOld))
                    before = GitHelper.DeserializeTestPlan(await tOld);

                if (!string.IsNullOrEmpty(await tNew))
                    after = GitHelper.DeserializeTestPlan(await tNew);

                // 4. Validate (Mirrors your reference check)
                if (before == null && after == null)
                {
                    MessageBox.Show("Unable to load either side for diff.",
                        "Diff Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 5. Load VM and Open Window (Identical to reference)
                var vm = new DiffViewerTreeViewModel();

                // Handle case where one side is missing (Added/Deleted file) by providing empty plan
                vm.Load(before ?? new TestPlan(), after ?? new TestPlan());

                // Use the existing Window control you have
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

        // Shared logic for all 3 dropdown buttons
        private async Task SubmitReviewAsync(ReviewDecision decision)
        {
            // Validation: Comments are mandatory for "Request Changes" and "Comment"
            if (decision != ReviewDecision.Approve && string.IsNullOrWhiteSpace(CommentText))
            {
                // Optional: Show a message box here saying "Please enter a comment."
                return;
            }

            try
            {
                var octokitEvent = decision switch
                {
                    ReviewDecision.Approve => Octokit.PullRequestReviewEvent.Approve,
                    ReviewDecision.RequestChanges => Octokit.PullRequestReviewEvent.RequestChanges,
                    _ => Octokit.PullRequestReviewEvent.Comment
                };

                await _apiService.SubmitReviewAsync(_owner, _repo, Number, octokitEvent, CommentText);

                // Clear text after success
                CommentText = string.Empty;

                // Refresh the conversation to show the new review
                await Conversation.LoadDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Review failed: {ex.Message}");
            }
        }

        private async Task MergeAsync()
        {
            try
            {
                await _apiService.MergePullRequestAsync(_owner, _repo, Number, $"Merge PR #{Number}", Octokit.PullRequestMergeMethod.Merge);
                Status = "Merged";
                if (OnRequestRefresh != null)
                {
                    await OnRequestRefresh.Invoke();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Merge failed: {ex.Message}");
            }
        }

        private async Task ClosePrAsync()
        {
            try
            {
                await _apiService.UpdatePullRequestState(_owner, _repo, Number, Octokit.ItemState.Closed);

                Status = "Closed";

                if (OnRequestRefresh != null)
                {
                    await OnRequestRefresh.Invoke();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Close PR failed: {ex.Message}");
                MessageBox.Show($"Failed to close PR: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private FileChangeStatus ParseStatus(string status)
            => status switch { "added" => FileChangeStatus.Added, "removed" => FileChangeStatus.Deleted, _ => FileChangeStatus.Modified };
    }
}