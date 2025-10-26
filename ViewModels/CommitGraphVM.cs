// GitGUI.ViewModels/CommitGraphVM.cs
using GitGUI.Models;
using GitGUI.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace GitGUI.ViewModels
{
    public sealed class CommitGraphVM : INotifyPropertyChanged, IDisposable
    {
        public ObservableCollection<CommitRow> Items { get; } = new();

        private CommitRow? _selectedItem;
        public CommitRow? SelectedItem
        {
            get => _selectedItem;
            set { if (_selectedItem != value) { _selectedItem = value; OnPropertyChanged(); } }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set { if (_isBusy != value) { _isBusy = value; OnPropertyChanged(); } }
        }

        private bool _hasMore;
        public bool HasMore
        {
            get => _hasMore;
            private set { if (_hasMore != value) { _hasMore = value; OnPropertyChanged(); } }
        }

        private double _graphWidth = 180; // px (LaneWidth * (maxLane+padding))
        public double GraphWidth
        {
            get => _graphWidth;
            private set { if (Math.Abs(_graphWidth - value) > 0.01) { _graphWidth = value; OnPropertyChanged(); } }
        }

        // Config
        public int InitialLoadCount { get; set; } = 1000;
        public int PageSize { get; set; } = 800;
        public double LaneWidth { get; set; } = 14.0;

        // Context
        public string? RepoPath { get; private set; }
        public string? BranchName { get; private set; }

        // Commands
        public ICommand LoadMoreCommand { get; }
        public ICommand RefreshCommand { get; }

        // Internals
        private int _loadedCount = 0;
        private CancellationTokenSource? _cts;

        public CommitGraphVM()
        {
            LoadMoreCommand = new RelayCommand(async _ => await LoadMoreAsync(), _ => HasMore && !IsBusy);
            RefreshCommand = new RelayCommand(async _ => await RefreshAsync(), _ => !IsBusy);
        }

        public async Task InitializeAsync(string repoPath, string? branchName = null)
        {
            RepoPath = repoPath;
            BranchName = branchName;
            await LoadInitialAsync();
        }

        public async Task LoadInitialAsync()
        {
            CancelInFlight();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            IsBusy = true;
            try
            {
                Items.Clear();
                _loadedCount = 0;

                // Compute on background thread
                var (rows, maxLaneUsed, totalCount) = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();

                    // ✅ simplified FetchCommits call (no branchName)
                    var infos = GitCommitFetcher.FetchCommits(RepoPath!, max: InitialLoadCount);
                    var list = CommitRowFactory.From(infos);
                    CommitGraphLayoutSimple.Layout(list, out int maxLane);
                    return (list, maxLane, infos.Count);
                }, token);

                // Assign on UI thread
                foreach (var r in rows) Items.Add(r);
                _loadedCount = Items.Count;

                // Compute width
                const double BaseGraphWidth = 24.0;
                const int PaddingLanes = 1;
                int lanes = maxLaneUsed + 1;
                GraphWidth = (lanes <= 1)
                    ? BaseGraphWidth
                    : Math.Max(BaseGraphWidth, LaneWidth * (lanes + PaddingLanes));

                // For simplicity, always allow loading more if count == limit
                HasMore = _loadedCount >= InitialLoadCount;
                RaiseCanExec();
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task LoadMoreAsync()
        {
            if (IsBusy || !HasMore || string.IsNullOrEmpty(RepoPath)) return;

            CancelInFlight();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            IsBusy = true;
            try
            {
                var (newRows, hasMore, maxLaneUsed) = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();

                    // ✅ again no branchName
                    var window = GitCommitFetcher.FetchCommits(RepoPath!, max: _loadedCount + PageSize);
                    var allRows = CommitRowFactory.From(window);
                    CommitGraphLayoutSimple.Layout(allRows, out int maxLane);

                    var tail = allRows.Skip(_loadedCount).ToList();
                    bool more = window.Count >= _loadedCount + PageSize;
                    return (tail, more, maxLane);
                }, token);

                foreach (var r in newRows) Items.Add(r);
                _loadedCount = Items.Count;

                // Grow width if new branches appear
                const double BaseGraphWidth = 24.0;
                const int PaddingLanes = 1;
                int lanes = maxLaneUsed + 1;
                var desiredWidth = (lanes <= 1)
                    ? BaseGraphWidth
                    : Math.Max(BaseGraphWidth, LaneWidth * (lanes + PaddingLanes));
                GraphWidth = Math.Max(GraphWidth, desiredWidth);

                HasMore = hasMore;
                RaiseCanExec();
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task RefreshAsync() => await LoadInitialAsync();

        public async Task ChangeBranchAsync(string? branchName)
        {
            BranchName = branchName;
            await LoadInitialAsync();
        }

        private void RaiseCanExec()
        {
            (LoadMoreCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void CancelInFlight()
        {
            try { _cts?.Cancel(); } catch { /* ignore */ }
            _cts?.Dispose();
            _cts = null;
        }

        public void Dispose() => CancelInFlight();

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
