using GitWave.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;

namespace GitWave.ViewModels
{
    public class CreatePullRequestViewModel : BaseViewModel
    {
        private readonly GitHubApiService _apiService;
        private readonly string _owner;
        private readonly string _repo;

        // --- Branch Lists ---
        public ObservableCollection<string> Branches { get; } = new ObservableCollection<string>();

        // --- Selected Branches ---
        private string _selectedTargetBranch;
        public string SelectedTargetBranch
        {
            get => _selectedTargetBranch;
            set => SetProperty(ref _selectedTargetBranch, value);
        }

        private string _selectedSourceBranch;
        public string SelectedSourceBranch
        {
            get => _selectedSourceBranch;
            set => SetProperty(ref _selectedSourceBranch, value);
        }

        // --- PR Content ---
        private string _title;
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        private string _body;
        public string Body
        {
            get => _body;
            set => SetProperty(ref _body, value);
        }

        // --- Commands ---
        public ICommand CreateCommand { get; }
        public ICommand CancelCommand { get; }

        /// <summary>
        /// Callback when PR is successfully created
        /// </summary>
        public event Action<int> PullRequestCreated;

        /// <summary>
        /// Callback to close the dialog/view
        /// </summary>
        public event Action CloseRequested;

        public CreatePullRequestViewModel(GitHubApiService apiService, string owner, string repo)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));

            CreateCommand = new AsyncRelayCommand(_ => CreatePullRequestAsync(), _ => CanCreatePullRequest());
            CancelCommand = new RelayCommand(_ => ExecuteCancel());

            // Load branches on initialization
            _ = LoadBranchesAsync();
        }

        /// <summary>
        /// Load available branches from GitHub
        /// </summary>
        private async Task LoadBranchesAsync()
        {
            try
            {
                var branches = await _apiService.GetBranchesAsync(_owner, _repo);

                Branches.Clear();
                foreach (var branch in branches)
                {
                    Branches.Add(branch);
                }

                // Set default branches
                if (Branches.Count > 0)
                {
                    SelectedTargetBranch = "main"; // or first branch if main doesn't exist
                    SelectedSourceBranch = Branches.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading branches: {ex.Message}");
                MessageBox.Show("Failed to load branches", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Validate if PR can be created
        /// </summary>
        private bool CanCreatePullRequest()
        {
            return !string.IsNullOrWhiteSpace(Title) &&
                   !string.IsNullOrWhiteSpace(SelectedTargetBranch) &&
                   !string.IsNullOrWhiteSpace(SelectedSourceBranch) &&
                   SelectedTargetBranch != SelectedSourceBranch;
        }

        /// <summary>
        /// Create the pull request
        /// </summary>
        private async Task CreatePullRequestAsync()
        {
            if (!CanCreatePullRequest())
            {
                MessageBox.Show("Please fill in all required fields and select different branches.",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var pr = await _apiService.CreatePullRequest(
                    _owner,
                    _repo,
                    Title,
                    SelectedSourceBranch,
                    SelectedTargetBranch
                );

                MessageBox.Show($"Pull Request #{pr.Number} created successfully!",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                // Notify listeners
                PullRequestCreated?.Invoke(pr.Number);
                CloseRequested?.Invoke();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating PR: {ex.Message}");
                MessageBox.Show($"Failed to create pull request:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Cancel and close the dialog
        /// </summary>
        private void ExecuteCancel()
        {
            CloseRequested?.Invoke();
        }
    }
}