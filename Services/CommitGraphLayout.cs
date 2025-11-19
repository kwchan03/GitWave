using GitGUI.Models;
using GitGUI.ViewModels;

namespace GitGUI.Services
{
    /// <summary>
    /// Improved Git commit graph layout algorithm following DoltHub principles.
    /// 
    /// Key differences from CommitGraphLayoutSimple:
    /// 1. Processes commits BACKWARDS (newest to oldest) for optimal column placement
    /// 2. Classifies children into BranchChildren vs MergeChildren
    /// 3. Places commits based on children positions (proactive, not reactive)
    /// 4. Searches for available columns to the RIGHT for merge commits
    /// 5. Generates Bézier curve paths for smooth merge visualization
    /// 
    /// Algorithm:
    /// - Row assignment: Topological order (0 = oldest, N = newest)
    /// - Column assignment: Based on children positions
    ///   - Head commits (no children) get new columns
    ///   - Commits with branch children get leftmost child's column
    ///   - Commits with merge children get searched right placement
    /// </summary>
    public static class CommitGraphLayout
    {
        public static void Layout(IList<CommitRowViewModel> rows, out int maxLaneUsed)
        {
            maxLaneUsed = 0;

            if (rows == null || rows.Count == 0)
                return;

            try
            {
                var state = new LayoutState(rows.Count);
                var commitMap = new Dictionary<string, CommitRowViewModel>(StringComparer.Ordinal);
                var commitIndexMap = new Dictionary<string, int>(StringComparer.Ordinal);

                // Build maps
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    row.Row = i;

                    if (string.IsNullOrEmpty(row.Sha))
                        continue;

                    commitMap[row.Sha] = row;
                    commitIndexMap[row.Sha] = i;
                }

                // Build children map
                var childrenMap = new Dictionary<string, List<int>>(StringComparer.Ordinal);

                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    if (row.Parents == null || row.Parents.Count == 0)
                        continue;

                    foreach (var parentSha in row.Parents)
                    {
                        if (string.IsNullOrEmpty(parentSha))
                            continue;

                        if (!childrenMap.ContainsKey(parentSha))
                            childrenMap[parentSha] = new List<int>();

                        childrenMap[parentSha].Add(i);
                    }
                }

                // Initialize all commits with default values
                foreach (var row in rows)
                {
                    row.PrimaryLane = 0;
                    row.BranchColorIndex = 0;
                    row.Segments.Clear();
                }

                // Process commits BACKWARD (newest to oldest)
                for (int i = rows.Count - 1; i >= 0; i--)
                {
                    var commit = rows[i];

                    // Get children indices
                    var childrenIndices = childrenMap.ContainsKey(commit.Sha)
                        ? childrenMap[commit.Sha]
                        : new List<int>();

                    // Process this commit
                    ProcessCommit(commit, childrenIndices, rows, state, commitMap);
                }

                maxLaneUsed = state.MaxLaneUsed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in CommitGraphLayout: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        private static void ProcessCommit(
            CommitRowViewModel commit,
            List<int> childrenIndices,
            IList<CommitRowViewModel> rows,
            LayoutState state,
            Dictionary<string, CommitRowViewModel> commitMap)
        {
            if (commit == null)
                return;

            commit.Segments.Clear();

            // Classify children
            var branchChildren = new List<int>();
            var mergeChildren = new List<int>();

            foreach (var childIdx in childrenIndices)
            {
                // SAFE: Check bounds
                if (childIdx < 0 || childIdx >= rows.Count)
                    continue;

                var child = rows[childIdx];
                if (child == null)
                    continue;

                if (child.Parents.Count == 1)
                {
                    branchChildren.Add(childIdx);
                }
                else if (child.Parents.Count > 1)
                {
                    mergeChildren.Add(childIdx);
                }
            }

            // Assign lane to this commit
            int lane;
            int color;

            if (branchChildren.Count > 0)
            {
                // CASE 1: Has branch children
                // Place in column of LEFTMOST branch child
                var leftmostChildIdx = branchChildren
                    .OrderBy(idx => rows[idx].PrimaryLane)
                    .First();

                var leftmostChild = rows[leftmostChildIdx];
                lane = leftmostChild.PrimaryLane;
                color = leftmostChild.BranchColorIndex;
            }
            else if (mergeChildren.Count > 0)
            {
                // CASE 2: Only merge children (no branch children)
                // Search RIGHT for available column
                var rightmostMergeChildLane = mergeChildren
                    .Select(idx => rows[idx].PrimaryLane)
                    .Max();

                lane = FindAvailableColumnToRight(rightmostMergeChildLane, commit.Row, state);
                color = state.NextColorIndex++;
            }
            else
            {
                // CASE 3: No children (head commit)
                // Find first free column
                lane = FindFirstFreeColumn(state.ActiveLanes);
                color = state.NextColorIndex++;
            }

            // Assign lane and color to this commit
            commit.PrimaryLane = lane;
            commit.BranchColorIndex = color;

            // Mark lane as used
            EnsureLaneExists(lane, state);
            state.ActiveLanes[lane] = commit.Sha;
            state.MaxLaneUsed = Math.Max(state.MaxLaneUsed, lane);
            state.AddSegment(lane, commit.Row);

            // Draw vertical lines through this row for other active lanes
            DrawPassThroughLines(commit, lane, state);

            // Handle parents - create connection segments
            if (commit.Parents.Count > 0)
            {
                HandleFirstParent(commit, lane, color, rows, state);

                for (int i = 1; i < commit.Parents.Count; i++)
                {
                    HandleMergeParent(commit, lane, commit.Parents[i], rows, state);
                }
            }
        }

        private static void DrawPassThroughLines(
            CommitRowViewModel commit,
            int commitLane,
            LayoutState state)
        {
            // Draw vertical lines for lanes that continue through this row
            for (int lane = 0; lane < state.ActiveLanes.Count; lane++)
            {
                if (lane != commitLane && !string.IsNullOrEmpty(state.ActiveLanes[lane]))
                {
                    var activeSha = state.ActiveLanes[lane];
                    int color = state.GetColor(activeSha);

                    commit.Segments.Add(new GraphSegment
                    {
                        FromLane = lane,
                        ToLane = lane,
                        Kind = SegmentKind.Vertical,
                        ColorIndex = color,
                        CurveType = CurveType.Straight
                    });
                }
            }
        }

        private static void HandleFirstParent(
            CommitRowViewModel commit,
            int commitLane,
            int commitColor,
            IList<CommitRowViewModel> rows,
            LayoutState state)
        {
            if (commit.Parents.Count == 0)
                return;

            var parentSha = commit.Parents[0];
            if (string.IsNullOrEmpty(parentSha))
                return;

            int parentLane;

            if (state.CommitLanes.TryGetValue(parentSha, out parentLane))
            {
                // Parent already assigned
            }
            else
            {
                // Parent not yet assigned - continue in same lane
                parentLane = commitLane;
                state.CommitLanes[parentSha] = parentLane;
                state.CommitColors[parentSha] = commitColor;
            }

            // Mark this lane as active for the parent
            EnsureLaneExists(parentLane, state);
            state.ActiveLanes[parentLane] = parentSha;
            state.AddSegment(parentLane, commit.Row);

            // Draw connection from commit to first parent
            commit.Segments.Add(new GraphSegment
            {
                FromLane = commitLane,
                ToLane = parentLane,
                Kind = SegmentKind.Vertical,
                ColorIndex = commitColor,
                CurveType = CurveType.Straight
            });
        }

        private static void HandleMergeParent(
            CommitRowViewModel commit,
            int commitLane,
            string parentSha,
            IList<CommitRowViewModel> rows,
            LayoutState state)
        {
            if (string.IsNullOrEmpty(parentSha))
                return;

            int parentLane;

            if (state.CommitLanes.TryGetValue(parentSha, out parentLane))
            {
                // Parent already assigned
            }
            else
            {
                // Parent not yet assigned - find free lane
                parentLane = FindFirstFreeColumn(state.ActiveLanes);
                state.CommitLanes[parentSha] = parentLane;
                state.CommitColors[parentSha] = state.NextColorIndex++;
            }

            EnsureLaneExists(parentLane, state);
            state.ActiveLanes[parentLane] = parentSha;
            state.AddSegment(parentLane, commit.Row);

            // Draw MERGE segment (curved line)
            int parentColor = state.GetColor(parentSha);
            commit.Segments.Add(new GraphSegment
            {
                FromLane = commitLane,
                ToLane = parentLane,
                Kind = SegmentKind.Merge,
                ColorIndex = parentColor,
                CurveType = CurveType.Bezier
            });
        }

        private static int FindAvailableColumnToRight(
            int startColumn,
            int commitRow,
            LayoutState state)
        {
            // Search columns to the right
            for (int col = startColumn + 1; col < state.Columns.Count; col++)
            {
                var segments = state.Columns[col];

                // Check if this column is available at this row
                bool isAvailable = segments.All(seg => commitRow > seg.EndRow);

                if (isAvailable)
                {
                    return col;
                }
            }

            // No available column found - create new one
            return state.Columns.Count;
        }

        private static int FindFirstFreeColumn(List<string?> activeLanes)
        {
            for (int i = 0; i < activeLanes.Count; i++)
            {
                if (string.IsNullOrEmpty(activeLanes[i]))
                    return i;
            }

            return activeLanes.Count;
        }

        private static void EnsureLaneExists(int lane, LayoutState state)
        {
            while (lane >= state.ActiveLanes.Count)
            {
                state.ActiveLanes.Add(null);
                state.Columns.Add(new List<ColumnSegment>());
            }
        }

        private class LayoutState
        {
            public List<string?> ActiveLanes { get; }
            public List<List<ColumnSegment>> Columns { get; }
            public Dictionary<string, int> CommitLanes { get; }
            public Dictionary<string, int> CommitColors { get; }
            public int NextColorIndex { get; set; } = 1;
            public int MaxLaneUsed { get; set; } = 0;

            public LayoutState(int estimatedSize)
            {
                ActiveLanes = new List<string?>(Math.Max(estimatedSize / 10, 10));
                Columns = new List<List<ColumnSegment>>(Math.Max(estimatedSize / 10, 10));
                CommitLanes = new Dictionary<string, int>(estimatedSize, StringComparer.Ordinal);
                CommitColors = new Dictionary<string, int>(estimatedSize, StringComparer.Ordinal);
            }

            public int GetColor(string? sha)
            {
                if (string.IsNullOrEmpty(sha))
                    return 0;

                return CommitColors.TryGetValue(sha, out var color) ? color : 0;
            }

            public void AddSegment(int lane, int row)
            {
                EnsureLaneExists(lane, this);

                var segments = Columns[lane];
                if (segments.Count == 0 || segments[^1].EndRow < row)
                {
                    segments.Add(new ColumnSegment { StartRow = row, EndRow = row });
                }
                else
                {
                    segments[^1].EndRow = row;
                }
            }
        }

        private class ColumnSegment
        {
            public int StartRow { get; set; }
            public int EndRow { get; set; }
        }

    }

    /// <summary>
    /// Curve type for smooth paths
    /// </summary>
    public enum CurveType
    {
        Straight,
        Bezier  // Cubic Bézier curve
    }
}