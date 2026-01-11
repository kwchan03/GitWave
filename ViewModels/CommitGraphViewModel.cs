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

        private readonly TraceSource _log = Log.CreateSource("GitWave.Graph");
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
            _log.Info("LoadAllBranchesAsync called");
            _log.Info($"IsRepositoryOpen: {_git.IsRepositoryOpen}");

            if (!_git.IsRepositoryOpen)
            {
                _log.Info("Repository not open, returning early");
                return;
            }

            IsBusy = true;
            try
            {
                _log.Info("Starting load...");
                _log.Info($"Items.Count before clear: {Items.Count}");

                var (rows, maxLaneUsed) = await Task.Run(() =>
                {
                    _log.Info("Fetching commits...");
                    var commits = _git.FetchCommitsForGraph();
                    _log.Info($"Got {commits.Count()} commits");

                    var list = CommitRowFactory.FromCommits(commits, _git);
                    _log.Info($"CommitRowFactory created {list.Count} rows");

                    var layouter = new CommitGraphLayouter();
                    layouter.Layout(list);
                    _log.Info($"Layout completed, {list.Count} rows");

                    int maxLane = list.Count > 0 ? list.Max(r => r.PrimaryLane) : 0;
                    return (list, maxLane);
                });

                DebugDumpRows(rows);

                _log.Info("Clearing Items...");
                Items.Clear();
                _log.Info($"Items.Count after clear: {Items.Count}");

                _log.Info($"Adding {rows.Count()} rows to Items...");
                foreach (var r in rows)
                {
                    Items.Add(r);
                }
                _log.Info($"Items.Count after adding: {Items.Count}");

                int lanes = maxLaneUsed + 1;
                double calculatedWidth = CalculateGraphWidth(lanes);
                GraphWidth = Math.Max(MinGraphWidth, calculatedWidth);

                _log.Info($"✅ LoadAllBranchesAsync completed with {Items.Count} items");
            }
            catch (Exception ex)
            {
                _log.Error($"❌ Exception: {ex.Message}");
                _log.Debug(ex);
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