// GitGUI.ViewModels/CommitGraphVM.cs
using GitGUI.Core;
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
        private readonly IGitService _git;

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

        private double _graphWidth = 180;
        public double GraphWidth
        {
            get => _graphWidth;
            private set { if (Math.Abs(_graphWidth - value) > 0.01) { _graphWidth = value; OnPropertyChanged(); } }
        }

        // Config
        public double LaneWidth { get; set; } = 14.0;
        public int MaxCommits { get; set; } = 2000;

        // Context moved to IGitService
        public string? BranchName { get; private set; }

        // Commands
        public ICommand RefreshCommand { get; }

        private CancellationTokenSource? _cts;

        // ---------- ctor (DI) ----------
        public CommitGraphVM(IGitService git)
        {
            _git = git ?? throw new ArgumentNullException(nameof(git));
            RefreshCommand = new RelayCommand(async _ => await RefreshAsync(), _ => !IsBusy);
        }

        // ---------- loading API ----------
        public async Task LoadAllBranchesAsync()
        {
            if (!_git.IsRepositoryOpen) return;

            CancelInFlight();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            IsBusy = true;
            try
            {
                Items.Clear();

                var (rows, maxLaneUsed) = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();

                    // Preferred: fetch commits (all branches) from the injected service
                    var commits = _git.FetchCommitsForGraph(MaxCommits);

                    // Build rows from LibGit2Sharp.Commit objects
                    //var rowsFromCommits = CommitGraphBuilder.BuildFromCommits(commits);
                    //int maxLane = rowsFromCommits.Count > 0 ? rowsFromCommits.Max(r => r.PrimaryLane) : 0;
                    var list = CommitRowFactory.FromCommits(commits);
                    CommitGraphLayoutSimple.Layout(list, out int maxLane);
                    return (list, maxLane);
                }, token);

                DebugDumpRows(rows, 60);

                foreach (var r in rows)
                    Items.Add(r);
                System.Diagnostics.Debug.WriteLine($"[DBG] CommitGraphVM.Items.Count = {Items.Count}");

                // compute width
                const double BaseGraphWidth = 24.0;
                const int PaddingLanes = 1;
                int lanes = maxLaneUsed + 1;
                GraphWidth = (lanes <= 1)
                    ? BaseGraphWidth
                    : Math.Max(BaseGraphWidth, LaneWidth * (lanes + PaddingLanes));
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task RefreshAsync() => await LoadAllBranchesAsync();

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

        // Debug helper - put this inside CommitGraphVM
        private void DebugDumpRows(IEnumerable<CommitRow> rows, int count = 40)
        {
            System.Diagnostics.Debug.WriteLine("=== CommitGraph DebugDumpRows ===");
            int i = 0;
            foreach (var r in rows)
            {
                if (i++ >= count) break;
                var segs = r.Segments.Count == 0 ? "(no-segs)" :
                           string.Join(", ", r.Segments.Select(s => $"{s.Kind}[{s.FromLane}->{s.ToLane}]"));
                var parents = r.Parents != null && r.Parents.Count > 0 ? string.Join(",", r.Parents.Take(3)) : "(no-parents)";
                System.Diagnostics.Debug.WriteLine($"{i,3}: {r.Sha[..7]} lane={r.PrimaryLane} segs={segs} parents={parents}");
            }
            System.Diagnostics.Debug.WriteLine("=== end dump ===");
        }

    }
}