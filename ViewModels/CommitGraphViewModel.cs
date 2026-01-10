// GitWave.ViewModels/CommitGraphVM.cs
using GitWave.Core;
using GitWave.Models;
using GitWave.Services;
using OpenTap;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace GitWave.ViewModels
{
    public class CommitGraphViewModel : BaseViewModel
    {
        private readonly IGitService _git;

        private readonly TraceSource _log = Log.CreateSource("GitWave");
        public ObservableCollection<CommitRowViewModel> Items { get; } = new();

        private CommitRowViewModel? _selectedItem;
        public CommitRowViewModel? SelectedItem
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

        private double _graphWidth = 40.0;  // Start with minimum
        public double GraphWidth
        {
            get => _graphWidth;
            private set { if (Math.Abs(_graphWidth - value) > 0.01) { _graphWidth = value; OnPropertyChanged(); } }
        }

        private double _minGraphWidth = 40.0;
        public double MinGraphWidth
        {
            get => _minGraphWidth;
            private set { if (Math.Abs(_minGraphWidth - value) > 0.01) { _minGraphWidth = value; OnPropertyChanged(); } }
        }

        // Config
        public double LaneWidth { get; set; } = 14.0;
        private const double BasePadding = 24.0;
        private const int PaddingLanes = 1;

        // Context moved to IGitService
        public string? BranchName { get; private set; }

        // Commands
        public ICommand RefreshCommand { get; }

        private CancellationTokenSource? _cts;

        // ---------- ctor (DI) ----------
        public CommitGraphViewModel(IGitService git)
        {
            _git = git ?? throw new ArgumentNullException(nameof(git));
            MinGraphWidth = CalculateMinGraphWidth(0);
            RefreshCommand = new RelayCommand(async _ => await RefreshAsync(), _ => !IsBusy);
        }

        // ---------- loading API ----------
        public async Task LoadAllBranchesAsync()
        {
            if (!_git.IsRepositoryOpen) return;

            IsBusy = true;
            try
            {
                var (rows, maxLaneUsed) = await Task.Run(() =>
                {
                    // Preferred: fetch commits (all branches) from the injected service
                    var commits = _git.FetchCommitsForGraph();

                    // Build rows from LibGit2Sharp.Commit objects
                    var list = CommitRowFactory.FromCommits(commits, _git);
                    var layouter = new CommitGraphLayouter();
                    layouter.Layout(list);

                    int maxLane = list.Count > 0 ? list.Max(r => r.PrimaryLane) : 0;
                    return (list, maxLane);
                });

                DebugDumpRows(rows);
                Items.Clear();

                foreach (var r in rows)
                    Items.Add(r);
                System.Diagnostics.Debug.WriteLine($"[DBG] CommitGraphVM.Items.Count = {Items.Count}");

                // Compute and apply graph width with minimum enforcement
                int lanes = maxLaneUsed + 1;
                double calculatedWidth = CalculateGraphWidth(lanes);
                GraphWidth = Math.Max(MinGraphWidth, calculatedWidth);

                System.Diagnostics.Debug.WriteLine($"[DBG] Lanes: {lanes}, GraphWidth: {GraphWidth}, MinGraphWidth: {MinGraphWidth}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Calculate minimum graph width based on base padding and lane count
        /// </summary>
        private double CalculateMinGraphWidth(int laneCount)
        {
            return BasePadding + (laneCount * LaneWidth);
        }

        /// <summary>
        /// Calculate the actual graph width with proper padding
        /// </summary>
        private double CalculateGraphWidth(int laneCount)
        {
            if (laneCount <= 1)
                return BasePadding;

            return Math.Max(BasePadding, LaneWidth * (laneCount + PaddingLanes));
        }

        public async Task RefreshAsync() => await LoadAllBranchesAsync();

        // Debug helper
        private void DebugDumpRows(IEnumerable<CommitRowViewModel> rows)
        {
            System.Diagnostics.Debug.WriteLine("=== CommitGraph DebugDumpRows ===");
            int i = 0;
            foreach (var r in rows)
            {
                var segs = r.Segments.Count == 0 ? "(no-segs)" :
                           string.Join(", ", r.Segments.Select(s => $"[{s.FromLane}->{s.ToLane}]"));
                var parents = r.Parents != null && r.Parents.Count > 0 ? string.Join(",", r.Parents.Take(3)) : "(no-parents)";
                System.Diagnostics.Debug.WriteLine($"{i,3}: {r.Sha[..7]} lane={r.PrimaryLane} segs={segs} parents={parents}");
                i++;
            }
            System.Diagnostics.Debug.WriteLine("=== end dump ===");
        }
    }
}