using GitGUI.Models;

namespace GitGUI.Services
{
    public static class CommitGraphLayout
    {
        /// <summary>
        /// Simple and correct commit graph layout algorithm.
        /// This algorithm properly handles git graph visualization by:
        /// 1. Only drawing lines where they actually exist in the git history
        /// 2. Properly handling root commits (no line below)
        /// 3. Correctly showing branch points
        /// </summary>
        public static void Layout(IList<CommitRow> rows, out int maxLaneUsed)
        {
            if (rows.Count == 0)
            {
                maxLaneUsed = 0;
                return;
            }

            // Create a mapping of commit SHA to row index
            var commitToIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Count; i++)
            {
                commitToIndex[rows[i].Sha] = i;
            }

            // Track which commits are currently "active" (have children not yet processed)
            var activeCommits = new Dictionary<string, int>(StringComparer.Ordinal);
            var laneAssignments = new Dictionary<string, int>(StringComparer.Ordinal);
            maxLaneUsed = 0;

            // Process commits from newest to oldest (top to bottom in display)
            for (int i = 0; i < rows.Count; i++)
            {
                var currentRow = rows[i];
                currentRow.Segments.Clear();

                var parents = currentRow.Parents ?? Array.Empty<string>();

                // Determine the lane for this commit
                int currentLane;
                if (laneAssignments.TryGetValue(currentRow.Sha, out currentLane))
                {
                    // This commit was already assigned a lane (it's a parent of a previous commit)
                    currentRow.ConnectTop = true;
                }
                else
                {
                    // New commit - find the best lane
                    currentLane = FindBestLane(activeCommits, parents);
                    laneAssignments[currentRow.Sha] = currentLane;
                    currentRow.ConnectTop = false;
                }

                currentRow.PrimaryLane = currentLane;
                maxLaneUsed = Math.Max(maxLaneUsed, currentLane);

                // Draw vertical lines for commits that are being continued
                // Only draw lines for commits that have children in the rows above
                foreach (var kvp in activeCommits)
                {
                    if (kvp.Key != currentRow.Sha)
                    {
                        // Check if this active commit has any children in the rows above
                        bool hasChildrenAbove = false;
                        for (int j = 0; j < i; j++)
                        {
                            var rowAbove = rows[j];
                            if (rowAbove.Parents?.Contains(kvp.Key) == true)
                            {
                                hasChildrenAbove = true;
                                break;
                            }
                        }

                        if (hasChildrenAbove)
                        {
                            currentRow.Segments.Add(new GraphSeg
                            {
                                FromLane = kvp.Value,
                                ToLane = kvp.Value,
                                Kind = SegKind.Vertical,
                                ColorIndex = kvp.Value
                            });
                        }
                    }
                }

                // Handle parents
                if (parents.Count == 0)
                {
                    // No parents - this is a root commit, just draw the dot
                    // Remove this commit from active (it has no children to connect to)
                    activeCommits.Remove(currentRow.Sha);
                }
                else
                {
                    // First parent continues on the same lane
                    var firstParent = parents[0];
                    if (!laneAssignments.ContainsKey(firstParent))
                    {
                        laneAssignments[firstParent] = currentLane;
                    }
                    activeCommits[firstParent] = currentLane;

                    // Draw the main vertical line to first parent
                    currentRow.Segments.Add(new GraphSeg
                    {
                        FromLane = currentLane,
                        ToLane = currentLane,
                        Kind = SegKind.Vertical,
                        ColorIndex = currentLane
                    });

                    // Handle additional parents (merge commits)
                    for (int j = 1; j < parents.Count; j++)
                    {
                        var mergeParent = parents[j];
                        int mergeLane = FindMergeLane(activeCommits, currentLane);

                        if (!laneAssignments.ContainsKey(mergeParent))
                        {
                            laneAssignments[mergeParent] = mergeLane;
                        }
                        activeCommits[mergeParent] = mergeLane;

                        // Draw merge line
                        currentRow.Segments.Add(new GraphSeg
                        {
                            FromLane = currentLane,
                            ToLane = mergeLane,
                            Kind = SegKind.Merge,
                            ColorIndex = mergeLane
                        });

                        maxLaneUsed = Math.Max(maxLaneUsed, mergeLane);
                    }
                }
            }
        }

        private static int FindBestLane(Dictionary<string, int> activeCommits, IReadOnlyList<string> parents)
        {
            if (parents.Count == 0)
                return 0;

            // If any parent is already active, try to use its lane
            foreach (var parent in parents)
            {
                if (activeCommits.TryGetValue(parent, out int parentLane))
                {
                    return parentLane;
                }
            }

            // Find the first available lane
            int lane = 0;
            while (activeCommits.Values.Contains(lane))
            {
                lane++;
            }
            return lane;
        }

        private static int FindMergeLane(Dictionary<string, int> activeCommits, int currentLane)
        {
            // Find the first available lane to the right of current lane
            int lane = currentLane + 1;
            while (activeCommits.Values.Contains(lane))
            {
                lane++;
            }
            return lane;
        }
    }
}
