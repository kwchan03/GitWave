using GitWave.Controls;
using GitWave.Services;
using OpenTap;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;

namespace GitWave.ViewModels
{
    public class CommitNodeViewModel : BaseViewModel
    {
        private readonly GitHubApiService _apiService;
        private readonly string _owner;
        private readonly string _repo;

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

        public CommitNodeViewModel(GitHubApiService apiService, string owner, string repo)
        {
            _apiService = apiService;
            _owner = owner;
            _repo = repo;

            ViewFileDiffCommand = new RelayCommand(param => ExecuteViewFileDiff(param as FileChangeViewModel));
        }

        public async Task LoadFilesAsync()
        {
            try
            {
                // Fetch files for this commit
                var files = await _apiService.GetCommitFilesAsync(_owner, _repo, Sha);

                // Update UI
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

        private async void ExecuteViewFileDiff(FileChangeViewModel file)
        {
            if (!file.FilePath.EndsWith(".TapPlan", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Diff Viewer currently supports only .TapPlan files.",
                    "Unsupported File Type", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var tOld = _apiService.GetFileContentAsync(_owner, _repo, ParentSha, file.FilePath);
                var tNew = _apiService.GetFileContentAsync(_owner, _repo, Sha, file.FilePath);

                await Task.WhenAll(tOld, tNew);

                var before = TestPlanHelper.DeserializeTestPlan(await tOld);
                var after = TestPlanHelper.DeserializeTestPlan(await tNew);

                if (before == null && after == null)
                {
                    MessageBox.Show("Unable to load either side for diff.\nCheck plugin discovery or file path.",
                        "Diff Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var vm = new DiffViewerViewModel();
                vm.Load(before ?? new TestPlan(), after ?? new TestPlan());

                using (var win = new DiffViewerWindow(vm) { Owner = System.Windows.Application.Current.MainWindow })
                {
                    win.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Diff failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private FileChangeStatus ParseStatus(string status)
             => status switch { "added" => FileChangeStatus.Added, "removed" => FileChangeStatus.Deleted, _ => FileChangeStatus.Modified };
    }
}