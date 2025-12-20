using GitWave.Core;
using GitWave.Services;
using GitWave.UI.Controls;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace GitWave.ViewModels
{
    public class PullRequestPageViewModel : BaseViewModel
    {
        private readonly IGitService _gitService;
        private GitHubApiService _apiService;
        private string _owner;
        private string _repo;

        // The List on the Left
        public ObservableCollection<PullRequestItemViewModel> PullRequests { get; } = new ObservableCollection<PullRequestItemViewModel>();

        // The Selected Item (Drives the Right Panel)
        private bool _isDetailVisible;
        public bool IsDetailVisible
        {
            get => _isDetailVisible;
            set => SetProperty(ref _isDetailVisible, value);
        }

        private PullRequestItemViewModel _selectedPullRequest;
        public PullRequestItemViewModel SelectedPullRequest
        {
            get => _selectedPullRequest;
            set
            {
                // When the selection changes...
                if (SetProperty(ref _selectedPullRequest, value))
                {
                    IsDetailVisible = _selectedPullRequest != null;

                    if (_selectedPullRequest != null)
                    {
                        _ = _selectedPullRequest.LoadCommitsAndFilesAsync();
                    }
                }
            }
        }

        public ICommand CreatePrCommand { get; }

        public PullRequestPageViewModel(IGitService gitService)
        {
            _gitService = gitService;
            CreatePrCommand = new RelayCommand(_ => CreatePr());

            // Auto-load
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            var user = _gitService.AuthenticatedUser;
            if (user == null)
            {
                Debug.WriteLine("Debug: No authenticated user found in GitService.");
                return;
            }

            _apiService = new GitHubApiService(user.AccessToken);
            var (owner, repo) = _gitService.GetRemoteRepoInfo();

            if (string.IsNullOrEmpty(owner)) return;

            _owner = owner;
            _repo = repo;

            try
            {
                var prs = await _apiService.GetPullRequests(owner, repo);

                PullRequests.Clear();
                foreach (var pr in prs)
                {
                    // Map Octokit -> ViewModel
                    var vm = new PullRequestItemViewModel(_apiService, owner, repo, pr)
                    {
                        Status = pr.State.StringValue.Substring(0, 1).ToUpper() + pr.State.StringValue.Substring(1),
                    };
                    vm.OnRequestRefresh += RefreshPullRequestsAsync;
                    PullRequests.Add(vm);
                }
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine("Error loading PRs: " + ex.Message);
            }
        }

        /// <summary>
        /// Open Create Pull Request dialog
        /// </summary>
        private void CreatePr()
        {
            if (_apiService == null || string.IsNullOrEmpty(_owner))
            {
                System.Windows.MessageBox.Show("Not authenticated or repository info not loaded.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Create the ViewModel
                var createPrVM = new CreatePullRequestViewModel(_apiService, _owner, _repo);

                // Handle PR creation success
                createPrVM.PullRequestCreated += async (prNumber) =>
                {
                    // Refresh PR list
                    await RefreshPullRequestsAsync();
                };

                // Create the View
                var createPrView = new CreatePullRequestView
                {
                    DataContext = createPrVM
                };

                // Create and show the dialog window
                var dialog = new Window
                {
                    Title = "Create Pull Request",
                    Content = createPrView,
                    Width = 600,
                    Height = 500,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Owner = System.Windows.Application.Current.MainWindow,
                    ShowInTaskbar = false,
                    ResizeMode = ResizeMode.NoResize
                };

                // Close dialog when requested
                createPrVM.CloseRequested += () =>
                {
                    dialog.Close();
                };

                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening Create PR dialog: {ex.Message}");
                System.Windows.MessageBox.Show("Failed to open Create PR dialog",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Refresh the PR list
        /// </summary>
        private async Task RefreshPullRequestsAsync()
        {
            try
            {
                var prs = await _apiService.GetPullRequests(_owner, _repo);

                PullRequests.Clear();
                foreach (var pr in prs)
                {
                    var vm = new PullRequestItemViewModel(_apiService, _owner, _repo, pr)
                    {
                        Status = pr.State.StringValue.Substring(0, 1).ToUpper() + pr.State.StringValue.Substring(1),
                    };
                    vm.OnRequestRefresh += RefreshPullRequestsAsync;
                    PullRequests.Add(vm);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error refreshing PRs: {ex.Message}");
            }
        }
    }
}