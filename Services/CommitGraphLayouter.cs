using GitGUI.Models;
using GitGUI.ViewModels;
using System.Diagnostics;

namespace GitGUI.Services
{
    /// <summary>
    /// Implements DoltHub-style commit graph layout algorithm.
    /// WITH DETAILED DEBUG LOGGING for lane assignment
    /// </summary>
    public class CommitGraphLayouter
    {
        private Dictionary<string, int> _lanes = new();
        private Dictionary<string, int> _colors = new();
        private List<List<ColumnSegment>> _columnSegments = new();
        private int _nextColorIndex = 0;
        private List<BranchPath> _branchPaths = new();

        // Debug helper
        private int _commitProcessCount = 0;

        public void Layout(List<CommitRowViewModel> commits)
        {
            _lanes.Clear();
            _colors.Clear();
            _columnSegments.Clear();
            _nextColorIndex = 0;
            _commitProcessCount = 0;

            if (commits == null || commits.Count == 0) return;

            Debug.WriteLine("\n========== LAYOUT START ==========");
            Debug.WriteLine($"[Layout] Processing {commits.Count} commits");

            var childrenMap = BuildChildrenMap(commits);

            Debug.WriteLine("\n--- Children Map ---");
            foreach (var kvp in childrenMap.Where(x => x.Value.Count > 0))
            {
                Debug.WriteLine($"  {kvp.Key.Substring(0, 7)} → {kvp.Value.Count} children");
            }

            Debug.WriteLine("\n--- Processing commits (backward) ---");
            for (int i = 0; i < commits.Count; i++)
            {
                var commit = commits[i];
                var children = childrenMap[commit.Sha];

                Debug.WriteLine($"\n[Commit {_commitProcessCount++}] {commit.Sha.Substring(0, 7)} (Row {commit.Row})");
                Debug.WriteLine($"  Children: {children.Count}");

                int minChildRow = children.Count == 0
                    ? commit.Row
                    : children.Select(c => c.Row).Min();

                Debug.WriteLine($"  MinChildRow: {minChildRow}");

                var (branchChildren, mergeChildren) = ClassifyChildren(commit, children);
                Debug.WriteLine($"  Classification: {branchChildren.Count} branch, {mergeChildren.Count} merge");


                // ALWAYS calculate the lane. 
                // We trust that parents haven't been touched yet.
                int lane = AssignLane(commit, branchChildren, mergeChildren, minChildRow);

                // Save it so parents can see it later
                _lanes[commit.Sha] = lane;

                if (children.Count > 0)
                {
                    _colors[commit.Sha] = _colors.GetValueOrDefault(children[0].Sha, 0);
                    Debug.WriteLine($"  Color (inherited): {_colors[commit.Sha]}");
                }
                else
                {
                    _colors[commit.Sha] = _nextColorIndex++;
                    Debug.WriteLine($"  Color (new): {_colors[commit.Sha]}");
                }

                // Update segments using the new logic
                // Note: We pass 'isContinuation' so we know whether to Add or Extend
                bool isContinuation = branchChildren.Count > 0;
                UpdateLaneState(commit, lane, minChildRow, isContinuation);
                //CreateSegments(commit, lane, commit.Row);
            }

            Debug.WriteLine("\n========== APPLYING RESULTS ==========");
            ApplyResults(commits);
            Debug.WriteLine("========== LAYOUT END ==========\n");
        }

        private Dictionary<string, List<CommitRowViewModel>> BuildChildrenMap(
            List<CommitRowViewModel> commits)
        {
            var map = new Dictionary<string, List<CommitRowViewModel>>();

            // Initialize empty lists
            foreach (var c in commits) map[c.Sha] = new List<CommitRowViewModel>();

            foreach (var commit in commits)
            {
                if (commit.Parents == null || commit.Parents.Count == 0)
                    continue;

                foreach (var parentSha in commit.Parents)
                {
                    if (map.ContainsKey(parentSha))
                    {
                        map[parentSha].Add(commit);
                    }
                }
            }

            return map;
        }

        private (List<CommitRowViewModel>, List<CommitRowViewModel>) ClassifyChildren(
            CommitRowViewModel currentCommit,
            List<CommitRowViewModel> children)
        {
            var branchChildren = new List<CommitRowViewModel>();
            var mergeChildren = new List<CommitRowViewModel>();

            foreach (var child in children)
            {
                if (child.Parents != null && child.Parents.Count > 0)
                {
                    // CRITICAL FIX: Check if current commit is the FIRST parent
                    if (child.Parents[0] == currentCommit.Sha)
                    {
                        branchChildren.Add(child);
                    }
                    else
                    {
                        // If current commit is the 2nd, 3rd, etc. parent, it's a merge line
                        mergeChildren.Add(child);
                    }
                }
            }
            return (branchChildren, mergeChildren);
        }

        /// <summary>
        /// Assign lane with detailed logging
        /// </summary>
        private int AssignLane(
            CommitRowViewModel commit,
            List<CommitRowViewModel> branchChildren,
            List<CommitRowViewModel> mergeChildren,
            int minChildRow)
        {
            Debug.WriteLine($"    [AssignLane] Evaluating...");

            // CASE 1: Has branch children
            if (branchChildren.Count > 0)
            {
                Debug.WriteLine($"    [AssignLane] CASE 1: Has {branchChildren.Count} branch children");

                // 1. Find all lanes currently occupied by these children
                var childLanes = branchChildren
                    .Select(c => _lanes.GetValueOrDefault(c.Sha, -1))
                    .Where(l => l != -1)
                    .ToList();

                // 2. Pick the Winner: The Left-Most Lane
                int winningLane = childLanes.Min();

                // 3. Handle the Losers: Create Gaps
                foreach (var lane in childLanes)
                {
                    if (lane != winningLane)
                    {
                        // TypeScript Equivalent: 
                        // updateColumnEnd(childX!, index - 1, commit.hash);

                        // C# Action: Find the active segment in this lane and force it to stop
                        // ONE ROW ABOVE the current commit.
                        var segments = _columnSegments[lane];
                        if (segments.Any())
                        {
                            var lastSegment = segments.Last();
                            // Cut it short. 
                            // This creates the whitespace needed for the curve.
                            lastSegment.EndRow = commit.Row - 1;
                        }
                    }
                }

                // 4. Extend the Winner
                // We return this lane, and the calling function will 
                // call `CreateSegments` (or equivalent) to extend this lane 
                // to the current row.
                return winningLane;
            }

            // CASE 2: Only merge children
            if (mergeChildren.Count > 0)
            {
                Debug.WriteLine($"    [AssignLane] CASE 2: Only merge children ({mergeChildren.Count})");

                // Combine logic to find the true right-most boundary
                int maxChildLane = -1;

                // Check ALL children (branch + merge) to find the absolute right boundary
                // Assuming 'commit.Children' or combining branchChildren + mergeChildren
                var allChildren = branchChildren.Concat(mergeChildren);

                foreach (var child in allChildren)
                {
                    if (_lanes.TryGetValue(child.Sha, out int l))
                    {
                        if (l > maxChildLane) maxChildLane = l;
                    }
                }
                Debug.WriteLine($"    [AssignLane] Rightmost lane {maxChildLane}");

                int lane = FindAvailableColumnToRight(maxChildLane, minChildRow);
                Debug.WriteLine($"    [AssignLane] → Found available lane to right: {lane}");
                return lane;
            }

            // CASE 3: No children
            Debug.WriteLine($"    [AssignLane] CASE 3: Head commit (no children)");
            int headLane = FindFirstFreeColumn(minChildRow);
            Debug.WriteLine($"    [AssignLane] → Found free column: {headLane}");
            return headLane;
        }

        private int FindAvailableColumnToRight(int startLane, int minChildRow)
        {
            Debug.WriteLine($"      [FindRight] Starting from lane {startLane}, minChildRow {minChildRow}");

            // Start searching at the next lane
            for (int lane = startLane + 1; ; lane++)
            {
                // ... add new column logic ...

                // If we exceed known columns, add a new one.
                // This implies the lane is definitely free.
                if (lane >= _columnSegments.Count)
                {
                    _columnSegments.Add(new List<ColumnSegment>());
                    return lane;
                }

                var segments = _columnSegments[lane];

                if (_columnSegments[lane].Count == 0)
                {
                    Debug.WriteLine($"      [FindRight] Lane {lane} is empty → AVAILABLE");
                    return lane;
                }

                var lastSegment = _columnSegments[lane].Last();

                // FIX 1: If EndRow is MaxValue, this lane is currently ACTIVE (occupied).
                // We cannot reuse it.
                if (lastSegment.EndRow == int.MaxValue)
                {
                    // Lane is busy. Skip.
                    Debug.WriteLine($"      [FindRight] Lane {lane} occupied until row {lastSegment.EndRow} → checking next...");
                    continue;
                }

                // FIX 2: Standard check for closed segments
                // "minChildRow >= EndRow" means the previous usage finished above us.
                if (minChildRow >= lastSegment.EndRow)
                {
                    Debug.WriteLine($"      [FindRight] Lane {lane} is free (minChildRow {minChildRow} >= endRow {lastSegment.EndRow}) → AVAILABLE");
                    return lane;
                }
            }
        }

        private int FindFirstFreeColumn(int minChildRow)
        {
            Debug.WriteLine($"      [FindFree] Looking for free column, minChildRow {minChildRow}");

            for (int lane = 0; lane < _columnSegments.Count; lane++)
            {
                if (_columnSegments[lane].Count == 0)
                {
                    Debug.WriteLine($"      [FindFree] Lane {lane} is empty → AVAILABLE");
                    return lane;
                }

                bool isFree = minChildRow >= _columnSegments[lane].Last().EndRow;
                if (isFree)
                {
                    Debug.WriteLine($"      [FindFree] Lane {lane} is free → AVAILABLE");
                    return lane;
                }
                else
                {
                    Debug.WriteLine($"      [FindFree] Lane {lane} occupied → trying next");
                }
            }

            int newLane = _columnSegments.Count;
            _columnSegments.Add(new List<ColumnSegment>());
            Debug.WriteLine($"      [FindFree] All lanes occupied, creating new lane {newLane}");
            return newLane;
        }

        private void UpdateLaneState(CommitRowViewModel commit, int lane, int minChildRow, bool isContinuation)
        {
            // Ensure list exists
            while (_columnSegments.Count <= lane) _columnSegments.Add(new List<ColumnSegment>());
            var segments = _columnSegments[lane];

            if (isContinuation)
            {
                // CASE 1: Continuing an existing branch.
                // The segment should ALREADY be Open (EndRow = MaxValue).
                // We don't need to do anything. It automatically "extends" to this row.

                // Sanity check (optional):
                if (segments.Count > 0 && segments.Last().EndRow != int.MaxValue)
                {
                    // If this happens, logic elsewhere closed it prematurely.
                    // Re-open it or log error.
                    System.Diagnostics.Debug.WriteLine($"[Warning] Continuing segment on lane {lane} but last segment is not open.");
                }
            }
            else
            {
                // CASE 2: Starting a NEW line (Branch Tip or Merge Jump)
                // We start a segment that goes from "Top" down to "Infinity".

                // If we have children (Merge), visual line starts at minChildRow + 1
                // If we have NO children (Branch Tip), visual line starts at commit.Row
                int start = (commit.Parents == null || commit.Parents.Count == 0)
                  ? commit.Row
                  : minChildRow + 1;

                segments.Add(new ColumnSegment
                {
                    StartRow = start,
                    EndRow = int.MaxValue // Mark as Active/Open
                });
            }
        }

        private void CreateSegments(CommitRowViewModel commit, int lane, int row)
        {
            while (_columnSegments.Count <= lane)
            {
                _columnSegments.Add(new List<ColumnSegment>());
            }

            var segments = _columnSegments[lane];

            if (segments.Count == 0)
            {
                segments.Add(new ColumnSegment { StartRow = row, EndRow = row });
                Debug.WriteLine($"    [Segment] Lane {lane}: NEW segment [{row}, {row}]");
            }
            else
            {
                var lastSegment = segments[segments.Count - 1];

                if (lastSegment.EndRow == row - 1)
                {
                    lastSegment.EndRow = row;
                    Debug.WriteLine($"    [Segment] Lane {lane}: EXTEND segment to [{lastSegment.StartRow}, {row}]");
                }
                else if (lastSegment.EndRow < row)
                {
                    segments.Add(new ColumnSegment { StartRow = row, EndRow = row });
                    Debug.WriteLine($"    [Segment] Lane {lane}: NEW gap-segment [{row}, {row}] (gap from {lastSegment.EndRow})");
                }
            }
        }

        private void ApplyResults(List<CommitRowViewModel> commits)
        {
            foreach (var commit in commits)
            {
                commit.PrimaryLane = _lanes[commit.Sha];
                commit.BranchColorIndex = _colors[commit.Sha];
                commit.HasIncomingConnection = commit.Parents?.Count > 0;
                commit.HasOutgoingConnection = commits.Any(c =>
                    c.Parents?.Contains(commit.Sha) == true);
                commit.Segments = GenerateSegmentsForCommit(commit);
            }

            // Uncomment to enable branch paths
            //BuildBranchPaths(commits);
        }

        /// <summary>
        /// Debug: Print final lane assignments
        /// </summary>
        public void PrintLaneAssignments(List<CommitRowViewModel> commits)
        {
            Debug.WriteLine("\n========== FINAL LANE ASSIGNMENTS ==========");
            foreach (var commit in commits.OrderBy(c => c.Row))
            {
                int lane = _lanes.GetValueOrDefault(commit.Sha, -1);
                int color = _colors.GetValueOrDefault(commit.Sha, -1);
                Debug.WriteLine($"  Row {commit.Row:D2} | Lane {lane:D2} | Color {color:D2} | {commit.Sha.Substring(0, 7)}");
            }
            Debug.WriteLine("==========================================\n");
        }

        private void BuildBranchPaths(List<CommitRowViewModel> commits)
        {
            if (!commits.Any()) return;

            var visited = new HashSet<string>();
            _branchPaths.Clear();

            var headCommits = commits.Where(commit =>
                !commits.Any(c => c.Parents?.Contains(commit.Sha) == true)
            ).ToList();

            Debug.WriteLine($"[BranchPaths] Found {headCommits.Count} HEAD commits");

            foreach (var headCommit in headCommits)
            {
                if (visited.Contains(headCommit.Sha))
                    continue;

                var path = TraceBranchPath(headCommit, commits, visited);
                if (path.Points.Count > 0)
                {
                    _branchPaths.Add(path);
                    Debug.WriteLine($"[BranchPaths] Path color={path.ColorIndex}, points={path.Points.Count}");
                }
            }
        }

        private BranchPath TraceBranchPath(
            CommitRowViewModel headCommit,
            List<CommitRowViewModel> allCommits,
            HashSet<string> visited)
        {
            if (!_colors.TryGetValue(headCommit.Sha, out int colorIndex))
            {
                colorIndex = 0;
            }

            var path = new BranchPath { ColorIndex = colorIndex };

            var current = headCommit;
            int pathLength = 0;
            const int maxPathLength = 10000;

            while (current != null && !visited.Contains(current.Sha) && pathLength < maxPathLength)
            {
                if (current == null || string.IsNullOrEmpty(current.Sha))
                    break;

                visited.Add(current.Sha);

                int lane = 0;
                if (!_lanes.TryGetValue(current.Sha, out lane))
                {
                    lane = 0;
                }
                path.Points.Add((current.Row, lane));
                pathLength++;

                if (current.Parents?.Count > 0)
                {
                    var firstParentSha = current.Parents[0];

                    if (string.IsNullOrEmpty(firstParentSha))
                        break;

                    current = allCommits.FirstOrDefault(c => c?.Sha == firstParentSha);

                    if (current == null)
                        break;
                }
                else
                {
                    break;
                }
            }

            return path;
        }

        public List<BranchPath> GetBranchPaths() => _branchPaths;

        private List<GraphSegment> GenerateSegmentsForCommit(CommitRowViewModel commit)
        {
            var segments = new List<GraphSegment>();

            if (commit == null || commit.Parents == null || commit.Parents.Count == 0)
                return segments;

            if (!_lanes.TryGetValue(commit.Sha, out var commitLane))
                return segments;

            for (int i = 0; i < commit.Parents.Count; i++)
            {
                var parentSha = commit.Parents[i];

                if (string.IsNullOrEmpty(parentSha))
                    continue;

                if (!_lanes.TryGetValue(parentSha, out var parentLane))
                    continue;

                var kind = i == 0 ? SegmentKind.Branch : SegmentKind.Merge;

                segments.Add(new GraphSegment
                {
                    Kind = kind,
                    FromLane = commitLane,
                    ToLane = parentLane,
                    ColorIndex = _colors.GetValueOrDefault(commit.Sha, 0)
                });
            }

            return segments;
        }
    }

    public class ColumnSegment
    {
        public int StartRow { get; set; }
        public int EndRow { get; set; }
        public string EndCommitSha { get; set; }
        public int BranchOrder { get; set; }

        public override string ToString() => $"Segment({StartRow}-{EndRow})";
    }
}