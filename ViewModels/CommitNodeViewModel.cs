using GitWave.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace GitWave.ViewModels
{
    public class CommitNodeViewModel : BaseViewModel
    {
        private readonly GitHubApiService _apiService;
        private readonly string _owner;
        private readonly string _repo;
        private readonly PullRequestItemViewModel _parentVM;

        public string Message { get; set; }
        public string Sha { get; set; }
        public string ParentSha { get; set; }

        public string ShaShort => Sha?.Length >= 7 ? Sha.Substring(0, 7) : Sha;

        private FileChangeViewModel? _selectedChange;
        public FileChangeViewModel? SelectedChange
        {
            get => _selectedChange;
            set => SetProperty(ref _selectedChange, value);
        }

        public ObservableCollection<FileChangeViewModel> Files { get; } = new ObservableCollection<FileChangeViewModel>();

        public ICommand ViewFileDiffCommand { get; }

        public CommitNodeViewModel(GitHubApiService apiService, string owner, string repo, PullRequestItemViewModel parentVM)
        {
            _apiService = apiService;
            _owner = owner;
            _repo = repo;
            _parentVM = parentVM;

            // Use AsyncRelayCommand to properly handle async operations
            ViewFileDiffCommand = new AsyncRelayCommand(param => ExecuteViewFileDiffAsync(param as FileChangeViewModel));
        }

        public async Task LoadFilesAsync()
        {
            try
            {
                var files = await _apiService.GetCommitFilesAsync(_owner, _repo, Sha);
                Files.Clear();

                foreach (var f in files)
                {
                    Files.Add(new FileChangeViewModel
                    {
                        FilePath = f.Filename,
                        Status = ParseStatus(f.Status)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading files: {ex.Message}");
                Files.Clear();
            }
        }

        private async Task ExecuteViewFileDiffAsync(FileChangeViewModel file)
        {
            if (file == null) return;

            // Delegate to parent ViewModel which has centralized logic
            await _parentVM.ViewFileDiffAsync(file, ParentSha, Sha);
        }

        private FileChangeStatus ParseStatus(string status)
             => status switch { "added" => FileChangeStatus.Added, "removed" => FileChangeStatus.Deleted, _ => FileChangeStatus.Modified };
    }
}