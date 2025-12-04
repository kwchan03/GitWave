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
        private List<int> _lastRowForColor = new();
        private Dictionary<string, List<CommitRowViewModel>> _childrenMap;
        private List<List<ColumnSegment>> _columnSegments = new();
        private int _nextColorIndex = 0;

        // Debug helper
        private int _commitProcessCount = 0;

        public void Layout(List<CommitRowViewModel> commits)
        {
            _lanes.Clear();
            _colors.Clear();
            _lastRowForColor.Clear();
            _columnSegments.Clear();
            _nextColorIndex = 0;
            _commitProcessCount = 0;

            if (commits == null || commits.Count == 0) return;

            Debug.WriteLine("\n========== LAYOUT START ==========");
            Debug.WriteLine($"[Layout] Processing {commits.Count} commits");

            _childrenMap = BuildChildrenMap(commits);

            Debug.WriteLine("\n--- Children Map ---");
            foreach (var kvp in _childrenMap.Where(x => x.Value.Count > 0))
            {
                Debug.WriteLine($"  {kvp.Key.Substring(0, 7)} → {kvp.Value.Count} children");
            }

            Debug.WriteLine("\n--- Processing commits (backward) ---");
            for (int i = 0; i < commits.Count; i++)
            {
                var commit = commits[i];
                var children = _childrenMap[commit.Sha];

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

                // 1. Determine "Primary" children (Children who consider ME their main parent)
                // This is the definition of "Branch Continuity"
                var primaryChildren = children
                    .Where(c => c.Parents != null && c.Parents.Count > 0 && c.Parents[0] == commit.Sha)
                    .ToList();

                int assignedColor;

                if (branchChildren.Count > 0)
                {
                    // CASE A: CONTINUATION (Inherit from Segment)
                    var existingSegment = _columnSegments[lane].Last();
                    assignedColor = existingSegment.ColorIndex;
                }
                else
                {
                    // CASE B: NEW SEGMENT (Get Free Color)
                    assignedColor = GetFirstFreeColor(commit.Row);
                }

                _colors[commit.Sha] = assignedColor;

                // CRITICAL: Mark this color as CURRENTLY ACTIVE
                // It doesn't matter if it's new or old; it is now flowing through this commit.
                UpdateColorState(assignedColor, int.MaxValue);

                // Update segments using the new logic
                // Note: We pass 'isContinuation' so we know whether to Add or Extend
                bool isContinuation = branchChildren.Count > 0;
                UpdateLaneState(commit, lane, minChildRow, isContinuation);
            }

            Debug.WriteLine("\n========== APPLYING RESULTS ==========");
            ApplyResults(commits);
            Debug.WriteLine("========== LAYOUT END ==========\n");
            PrintDebugInfo(commits);
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
                            UpdateColorState(lastSegment.ColorIndex, lastSegment.EndRow);
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

        private int GetFirstFreeColor(int currentRow)
        {
            // 1. Check existing colors
            for (int i = 0; i < _lastRowForColor.Count; i++)
            {
                // If the color hasn't been used since "currentRow - 2", it is free.
                // (currentRow - 1) means it ended immediately above us, so it's visually "touching".
                Debug.WriteLine($"      last row: {_lastRowForColor[i]} current row: {currentRow}");
                if (_lastRowForColor[i] < currentRow - 1)
                {
                    return i;
                }
            }

            // 2. No gaps found? Create a new color index.
            _lastRowForColor.Add(-1); // Initialize placeholder
            return _lastRowForColor.Count - 1;
        }

        private void UpdateColorState(int colorIndex, int rowValue)
        {
            // Ensure list is big enough
            while (_lastRowForColor.Count <= colorIndex)
            {
                _lastRowForColor.Add(-1); // Initialize as "Never used"
            }

            _lastRowForColor[colorIndex] = rowValue;
        }

        private void UpdateLaneState(CommitRowViewModel commit, int lane, int minChildRow, bool isContinuation)
        {
            // Ensure list exists
            while (_columnSegments.Count <= lane) _columnSegments.Add(new List<ColumnSegment>());
            var segments = _columnSegments[lane];

            if (isContinuation)
            {
                // CASE 1: CONTINUATION
                // The segment is already falling through.
                // Sanity Check: Ensure it's open.
                if (segments.Count > 0 && segments.Last().EndRow != int.MaxValue)
                {
                    segments.Last().EndRow = int.MaxValue;
                }

                // FIX FOR LAST COMMIT (Oldest in Repo):
                // If this commit has NO parents, we must CLOSE the segment here.
                if (commit.Parents == null || commit.Parents.Count == 0)
                {
                    segments.Last().EndRow = commit.Row;
                }
            }
            else
            {
                // CASE 2: NEW SEGMENT (Tip or Merge Jump)

                int start;

                // >>> BUG FIX START <<<
                // If minChildRow equals our row, it means we have NO children above us.
                // We are the TIP of a branch. We must start exactly here.
                if (minChildRow == commit.Row)
                {
                    start = commit.Row;
                }
                else
                {
                    // We have children, but we are starting a NEW line (Merge Jump).
                    // Start slightly below the child to allow curve connection.
                    start = minChildRow + 1;
                }

                segments.Add(new ColumnSegment
                {
                    StartRow = start,
                    EndRow = int.MaxValue, // Mark as Active/Open
                    ColorIndex = _colors.ContainsKey(commit.Sha) ? _colors[commit.Sha] : 0
                });
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

        private List<GraphSegment> GenerateSegmentsForCommit(CommitRowViewModel commit)
        {
            Debug.WriteLine($"\n=== GenerateSegmentsForCommit: {commit.Sha} (Row {commit.Row}) ===");

            var segments = new List<GraphSegment>();

            // ---------------------------------------------------------
            // 1. VERTICAL SEGMENTS (Pipes)
            // ---------------------------------------------------------
            Debug.WriteLine("Step 1: Checking vertical segments...");

            for (int laneIndex = 0; laneIndex < _columnSegments.Count; laneIndex++)
            {
                var laneSegments = _columnSegments[laneIndex];
                if (laneSegments.Count == 0)
                {
                    Debug.WriteLine($"  Lane {laneIndex}: No lane segments.");
                    continue;
                }

                var intersectingSegment = laneSegments.FirstOrDefault(seg =>
                    seg.StartRow <= commit.Row && seg.EndRow >= commit.Row);

                if (intersectingSegment != null)
                {
                    Debug.WriteLine(
                        $"  Lane {laneIndex}: Found intersecting segment (Color {intersectingSegment.ColorIndex}) " +
                        $"[{intersectingSegment.StartRow} → {intersectingSegment.EndRow}]");

                    segments.Add(new GraphSegment
                    {
                        Kind = SegmentKind.Vertical,
                        FromLane = laneIndex,
                        ToLane = laneIndex,
                        ColorIndex = intersectingSegment.ColorIndex,
                        IsStart = (_childrenMap[commit.Sha].Count <= 0),
                        IsEnd = (commit.Parents.Count <= 0)
                    });
                }
                else
                {
                    Debug.WriteLine($"  Lane {laneIndex}: No intersecting segment at row {commit.Row}.");
                }
            }

            // ---------------------------------------------------------
            // 2. CURVED SEGMENTS (Connections)
            // ---------------------------------------------------------
            Debug.WriteLine("Step 2: Checking curved segments (merges / forks)...");

            // A. MERGES
            if (commit.Parents != null && commit.Parents.Count > 1)
            {
                Debug.WriteLine($"  Merge detected: {commit.Parents.Count} parents.");

                if (_lanes.TryGetValue(commit.Sha, out int commitLane))
                {
                    for (int i = 1; i < commit.Parents.Count; i++)
                    {
                        var parentSha = commit.Parents[i];

                        if (_lanes.TryGetValue(parentSha, out int parentLane))
                        {
                            Debug.WriteLine($"    Parent {parentSha}: commitLane={commitLane}, parentLane={parentLane}");

                            if (commitLane != parentLane)
                            {
                                int color = _colors.GetValueOrDefault(parentSha, 0);

                                Debug.WriteLine($"    -> Adding MERGE curve to lane {parentLane} (Color {color})");

                                segments.Add(new GraphSegment
                                {
                                    Kind = SegmentKind.Merge,
                                    FromLane = commitLane,
                                    ToLane = parentLane,
                                    ColorIndex = color
                                });
                            }
                            else
                            {
                                Debug.WriteLine("    Same lane → No merge curve needed.");
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"    Parent {parentSha}: No lane assigned!");
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("  ERROR: Commit does not have a lane assigned!");
                }
            }

            // B. FORKS
            if (_childrenMap.TryGetValue(commit.Sha, out var children))
            {
                int myLane = _lanes[commit.Sha];
                Debug.WriteLine($"  Fork check: {children.Count} children found.");

                foreach (var child in children)
                {
                    if (_lanes.TryGetValue(child.Sha, out int childLane))
                    {
                        Debug.WriteLine($"    Child {child.Sha}: myLane={myLane}, childLane={childLane}");

                        if (childLane != myLane)
                        {
                            bool isFirstParent =
                                child.Parents != null &&
                                child.Parents.Count > 0 &&
                                child.Parents[0] == commit.Sha;

                            Debug.WriteLine($"      Is first parent: {isFirstParent}");

                            if (isFirstParent)
                            {
                                int color = _colors.GetValueOrDefault(child.Sha, 0);

                                Debug.WriteLine($"      -> Adding BRANCH curve from child lane {childLane} (Color {color})");

                                segments.Add(new GraphSegment
                                {
                                    Kind = SegmentKind.Branch,
                                    FromLane = childLane,
                                    ToLane = myLane,
                                    ColorIndex = color
                                });
                            }
                            else
                            {
                                Debug.WriteLine("      Not first parent → Skip branch curve.");
                            }
                        }
                        else
                        {
                            Debug.WriteLine("      Same lane → No branch curve.");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"    Child {child.Sha}: No lane assigned!");
                    }
                }
            }
            else
            {
                Debug.WriteLine("  No children found.");
            }

            Debug.WriteLine($"=== Total Segments Generated: {segments.Count} ===\n");

            return segments;
        }


        public void PrintDebugInfo(List<CommitRowViewModel> commits)
        {
            Debug.WriteLine("\n========== COLUMN SEGMENTS DUMP ==========");
            for (int i = 0; i < _columnSegments.Count; i++)
            {
                Debug.Write($"Lane {i}: ");
                if (_columnSegments[i].Count == 0)
                {
                    Debug.WriteLine("(empty)");
                    continue;
                }

                foreach (var seg in _columnSegments[i])
                {
                    string end = seg.EndRow == int.MaxValue ? "Inf" : seg.EndRow.ToString();
                    Debug.Write($"[{seg.StartRow}->{end}] ");
                }
                Debug.WriteLine("");
            }

            Debug.WriteLine("\n========== COMMIT SEGMENTS GENERATION CHECK ==========");
            // Sample check for the first few commits
            foreach (var commit in commits.Take(5))
            {
                Debug.WriteLine($"Commit {commit.Sha.Substring(0, 7)} (Row {commit.Row})");

                // Manual check of what logic sees
                for (int l = 0; l < _columnSegments.Count; l++)
                {
                    var segs = _columnSegments[l];
                    var match = segs.FirstOrDefault(s => s.StartRow <= commit.Row && s.EndRow >= commit.Row);
                    if (match != null)
                    {
                        string end = match.EndRow == int.MaxValue ? "Inf" : match.EndRow.ToString();
                        Debug.WriteLine($"  -> Covered by Lane {l}: Segment [{match.StartRow}->{end}]");
                    }
                }
            }
            Debug.WriteLine("==================================================\n");
        }
    }

    public class ColumnSegment
    {
        public int StartRow { get; set; }
        public int EndRow { get; set; }
        public int ColorIndex { get; set; }
    }
}