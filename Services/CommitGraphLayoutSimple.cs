//using GitWave.Models;
//using GitWave.ViewModels;

//namespace GitWave.Services
//{
//    /// <summary>
//    /// Simplified Git commit graph layout algorithm.
//    /// Features:
//    /// - Main branch always in lane 0
//    /// - Automatic lane and color assignment
//    /// - Merge detection: commits with multiple parents → Merge segments
//    /// - Branch detection: commits with multiple children → Branch segments
//    /// 
//    /// Connection tracking:
//    /// - HasIncomingConnection: true if commit has a connection from above (was pre-assigned a lane)
//    /// - HasOutgoingConnection: true if commit has parents (connections going down to parent commits)
//    /// </summary>
//    public static class CommitGraphLayoutSimple
//    {
//        public static void Layout(IList<CommitRowViewModel> rows, out int maxLaneUsed)
//        {
//            if (rows.Count == 0)
//            {
//                maxLaneUsed = 0;
//                return;
//            }

//            var state = new LayoutState();
//            var mainLineCommits = BuildMainLineCommits(rows);
//            var commitMap = BuildCommitMap(rows);
//            var childrenMap = BuildChildrenMap(rows);

//            for (int i = 0; i < rows.Count; i++)
//            {
//                ProcessCommit(rows[i], state, mainLineCommits, commitMap, childrenMap);
//            }

//            maxLaneUsed = state.MaxLaneUsed;
//        }

//        #region Data Structures

//        private class LayoutState
//        {
//            public Dictionary<string, int> CommitLanes { get; } = new(StringComparer.Ordinal);
//            public Dictionary<string, int> CommitColors { get; } = new(StringComparer.Ordinal);
//            public List<string?> ActiveLanes { get; } = new();
//            public int NextColorIndex { get; set; } = 1; // Color 0 reserved for main branch
//            public int MaxLaneUsed { get; set; } = 0;
//        }

//        private static Dictionary<string, CommitRowViewModel> BuildCommitMap(IList<CommitRowViewModel> rows)
//        {
//            return rows.ToDictionary(r => r.Sha, StringComparer.Ordinal);
//        }

//        private static HashSet<string> BuildMainLineCommits(IList<CommitRowViewModel> rows)
//        {
//            var mainLine = new HashSet<string>(StringComparer.Ordinal);

//            if (rows.Count == 0) return mainLine;

//            var commitMap = BuildCommitMap(rows);
//            var current = rows[0].Sha;

//            // Follow first-parent chain from HEAD to build main line
//            while (!string.IsNullOrEmpty(current))
//            {
//                mainLine.Add(current);

//                if (!commitMap.TryGetValue(current, out var row) || row.Parents.Count == 0)
//                    break;

//                current = row.Parents[0]; // Always follow first parent
//            }

//            return mainLine;
//        }

//        private static Dictionary<string, List<string>> BuildChildrenMap(IList<CommitRowViewModel> rows)
//        {
//            var childrenMap = new Dictionary<string, List<string>>(StringComparer.Ordinal);

//            foreach (var row in rows)
//            {
//                foreach (var parentSha in row.Parents)
//                {
//                    if (!childrenMap.TryGetValue(parentSha, out var children))
//                    {
//                        children = new List<string>();
//                        childrenMap[parentSha] = children;
//                    }
//                    children.Add(row.Sha);
//                }
//            }

//            return childrenMap;
//        }

//        #endregion

//        #region Main Processing

//        private static void ProcessCommit(
//            CommitRowViewModel row,
//            LayoutState state,
//            HashSet<string> mainLineCommits,
//            Dictionary<string, CommitRowViewModel> commitMap,
//            Dictionary<string, List<string>> childrenMap)
//        {
//            row.Segments.Clear();

//            var sha = row.Sha;
//            var parents = row.Parents?.ToArray() ?? Array.Empty<string>();

//            // Step 1: Assign lane and color to this commit
//            var (lane, color, wasPreAssigned) = AssignLaneAndColor(sha, parents, state, mainLineCommits);

//            row.PrimaryLane = lane;
//            row.BranchColorIndex = color;

//            // HasIncomingConnection: true if this commit was pre-assigned (has connection from above)
//            row.HasIncomingConnection = wasPreAssigned;

//            // HasOutgoingConnection: true if this commit has any parents (connections going down)
//            row.HasOutgoingConnection = parents.Length > 0;

//            state.MaxLaneUsed = Math.Max(state.MaxLaneUsed, lane);

//            // Step 2: Draw pass-through lines for other active lanes
//            DrawPassThroughLines(row, lane, state);

//            // Step 3: Clear current lane (this commit is being processed)
//            state.ActiveLanes[lane] = null;

//            // Step 4: Handle parents
//            // - If 1 parent: normal continuation
//            // - If 2+ parents: MERGE - draw merge segments from additional parents
//            if (parents.Length > 0)
//            {
//                HandleParents(row, lane, color, parents, state, mainLineCommits);
//            }

//            // Step 5: Handle branching
//            // - If this commit has 2+ children: BRANCH - draw branch segments to additional children
//            HandleBranching(row, sha, lane, color, state, mainLineCommits, childrenMap);
//        }

//        private static (int lane, int color, bool wasPreAssigned) AssignLaneAndColor(
//            string sha,
//            string[] parents,
//            LayoutState state,
//            HashSet<string> mainLineCommits)
//        {
//            // Check if lane was pre-assigned (incoming branch)
//            if (state.CommitLanes.TryGetValue(sha, out int lane))
//            {
//                int color = state.CommitColors[sha];
//                return (lane, color, true);
//            }

//            // Main branch commits always use lane 0 and color 0
//            if (mainLineCommits.Contains(sha))
//            {
//                lane = 0;
//                int color = 0;

//                EnsureLaneExists(lane, state);
//                state.CommitLanes[sha] = lane;
//                state.CommitColors[sha] = color;

//                return (lane, color, false);
//            }

//            // Branch commits: find free lane (skip lane 0 reserved for main)
//            lane = FindFirstFreeLane(state.ActiveLanes, skipLane0: true);
//            EnsureLaneExists(lane, state);

//            // Inherit color from first parent if available, otherwise assign new color
//            int branchColor;
//            if (parents.Length > 0 && state.CommitColors.TryGetValue(parents[0], out int parentColor))
//            {
//                branchColor = parentColor;
//            }
//            else
//            {
//                branchColor = state.NextColorIndex++;
//            }

//            state.CommitLanes[sha] = lane;
//            state.CommitColors[sha] = branchColor;

//            return (lane, branchColor, false);
//        }

//        private static void DrawPassThroughLines(CommitRowViewModel row, int commitLane, LayoutState state)
//        {
//            for (int lane = 0; lane < state.ActiveLanes.Count; lane++)
//            {
//                if (lane != commitLane && state.ActiveLanes[lane] != null)
//                {
//                    var activeSha = state.ActiveLanes[lane];
//                    var laneColor = state.CommitColors[activeSha!];

//                    row.Segments.Add(new GraphSegment
//                    {
//                        FromLane = lane,
//                        ToLane = lane,
//                        Kind = SegmentKind.Vertical,
//                        ColorIndex = laneColor
//                    });
//                }
//            }
//        }

//        private static void HandleParents(
//            CommitRowViewModel row,
//            int commitLane,
//            int commitColor,
//            string[] parents,
//            LayoutState state,
//            HashSet<string> mainLineCommits)
//        {
//            // First parent: continues the current branch
//            HandleFirstParent(row, commitLane, commitColor, parents[0], state, mainLineCommits);

//            // Additional parents (if any): MERGE - draw merge segments
//            // This handles the case where this commit has multiple parents (merge commit)
//            for (int i = 1; i < parents.Length; i++)
//            {
//                HandleMergeParent(row, commitLane, parents[i], state, mainLineCommits);
//            }
//        }

//        private static void HandleFirstParent(
//            CommitRowViewModel row,
//            int commitLane,
//            int commitColor,
//            string parentSha,
//            LayoutState state,
//            HashSet<string> mainLineCommits)
//        {
//            int parentLane;
//            int parentColor;

//            // Check if parent already has lane assigned
//            if (state.CommitLanes.TryGetValue(parentSha, out parentLane))
//            {
//                parentColor = state.CommitColors[parentSha];
//            }
//            else
//            {
//                // Parent lane not yet assigned - determine lane based on main line status
//                if (mainLineCommits.Contains(parentSha))
//                {
//                    parentLane = 0;
//                    parentColor = 0;
//                }
//                else
//                {
//                    // Continue in same lane
//                    parentLane = commitLane;
//                    parentColor = commitColor;
//                }

//                state.CommitLanes[parentSha] = parentLane;
//                state.CommitColors[parentSha] = parentColor;
//            }

//            // Reserve the lane for the parent
//            EnsureLaneExists(parentLane, state);
//            state.ActiveLanes[parentLane] = parentSha;

//            // Draw vertical connection to first parent
//            row.Segments.Add(new GraphSegment
//            {
//                FromLane = commitLane,
//                ToLane = parentLane,
//                Kind = SegmentKind.Vertical,
//                ColorIndex = commitColor
//            });
//        }

//        private static void HandleMergeParent(
//            CommitRowViewModel row,
//            int commitLane,
//            string parentSha,
//            LayoutState state,
//            HashSet<string> mainLineCommits)
//        {
//            int parentLane;
//            int parentColor;

//            // Check if merge parent already has lane assigned
//            if (state.CommitLanes.TryGetValue(parentSha, out parentLane))
//            {
//                parentColor = state.CommitColors[parentSha];
//            }
//            else
//            {
//                // Assign new lane for merge parent
//                if (mainLineCommits.Contains(parentSha))
//                {
//                    parentLane = 0;
//                    parentColor = 0;
//                }
//                else
//                {
//                    parentLane = FindFirstFreeLane(state.ActiveLanes, skipLane0: true);
//                    parentColor = state.NextColorIndex++;
//                }

//                EnsureLaneExists(parentLane, state);
//                state.CommitLanes[parentSha] = parentLane;
//                state.CommitColors[parentSha] = parentColor;
//                state.ActiveLanes[parentLane] = parentSha;
//                state.MaxLaneUsed = Math.Max(state.MaxLaneUsed, parentLane);
//            }

//            // Draw MERGE segment from this commit to the merge parent
//            row.Segments.Add(new GraphSegment
//            {
//                FromLane = commitLane,
//                ToLane = parentLane,
//                Kind = SegmentKind.Merge,
//                ColorIndex = parentColor
//            });
//        }

//        private static void HandleBranching(
//            CommitRowViewModel row,
//            string sha,
//            int commitLane,
//            int commitColor,
//            LayoutState state,
//            HashSet<string> mainLineCommits,
//            Dictionary<string, List<string>> childrenMap)
//        {
//            // Check if this commit has multiple children (fork point)
//            if (!childrenMap.TryGetValue(sha, out var children) || children.Count <= 1)
//                return; // Not a branch point - only 0 or 1 child

//            // This commit has 2+ children - BRANCH point
//            // First child already handled in first parent continuation
//            // Additional children need branch segments
//            for (int i = 1; i < children.Count; i++)
//            {
//                var childSha = children[i];

//                // Skip if child already has a lane assigned
//                if (state.CommitLanes.ContainsKey(childSha))
//                    continue;

//                int childLane;
//                int childColor;

//                // Check if child is on main line
//                if (mainLineCommits.Contains(childSha))
//                {
//                    childLane = 0;
//                    childColor = 0;
//                }
//                else
//                {
//                    // Allocate new lane for this branch (skip lane 0 reserved for main)
//                    childLane = FindFirstFreeLane(state.ActiveLanes, skipLane0: true);
//                    childColor = state.NextColorIndex++;
//                }

//                EnsureLaneExists(childLane, state);

//                // Pre-assign the child's lane and color
//                state.CommitLanes[childSha] = childLane;
//                state.CommitColors[childSha] = childColor;

//                // Reserve the lane for this child
//                state.ActiveLanes[childLane] = childSha;

//                state.MaxLaneUsed = Math.Max(state.MaxLaneUsed, childLane);

//                // Draw BRANCH segment FROM current commit TO child's lane
//                row.Segments.Add(new GraphSegment
//                {
//                    FromLane = commitLane,
//                    ToLane = childLane,
//                    Kind = SegmentKind.Branch,
//                    ColorIndex = childColor
//                });

//                // Also draw vertical line in the child's lane continuing downward
//                row.Segments.Add(new GraphSegment
//                {
//                    FromLane = childLane,
//                    ToLane = childLane,
//                    Kind = SegmentKind.Vertical,
//                    ColorIndex = childColor
//                });
//            }
//        }

//        #endregion

//        #region Utility Methods

//        private static int FindFirstFreeLane(List<string?> activeLanes, bool skipLane0 = false)
//        {
//            int startLane = skipLane0 ? 1 : 0;

//            for (int i = startLane; i < activeLanes.Count; i++)
//            {
//                if (activeLanes[i] == null)
//                    return i;
//            }

//            return activeLanes.Count;
//        }

//        private static void EnsureLaneExists(int lane, LayoutState state)
//        {
//            while (lane >= state.ActiveLanes.Count)
//            {
//                state.ActiveLanes.Add(null);
//            }
//        }

//        #endregion
//    }
//}