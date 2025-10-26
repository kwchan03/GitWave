using GitGUI.Models;

namespace GitGUI.Services
{
    /// <summary>
    /// Simple and correct commit graph layout algorithm.
    /// This algorithm works by:
    /// 1. Only drawing lines where they actually exist in the git history
    /// 2. Root commits have no line below (just a dot)
    /// 3. Branches start from commits that actually have multiple children
    /// </summary>
    public static class CommitGraphLayoutSimple
    {
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

            // Build a map of commit -> its children
            var childrenMap = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                foreach (var parent in row.Parents ?? Array.Empty<string>())
                {
                    if (!childrenMap.ContainsKey(parent))
                        childrenMap[parent] = new List<string>();
                    childrenMap[parent].Add(row.Sha);
                }
            }

            // Track lane assignments
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
                    currentRow.ConnectTop = true;
                }
                else
                {
                    // Find the best lane based on parents
                    currentLane = FindBestLaneForCommit(currentRow, parents, laneAssignments, commitToIndex, rows);
                    laneAssignments[currentRow.Sha] = currentLane;
                    currentRow.ConnectTop = false;
                }

                currentRow.PrimaryLane = currentLane;
                maxLaneUsed = Math.Max(maxLaneUsed, currentLane);

                // Draw vertical lines for commits that are being continued
                // Only draw lines for commits that have children in the rows above
                foreach (var kvp in laneAssignments)
                {
                    if (kvp.Key != currentRow.Sha && childrenMap.TryGetValue(kvp.Key, out var children))
                    {
                        // Check if any of the children are in the rows above
                        bool hasChildrenAbove = false;
                        for (int j = 0; j < i; j++)
                        {
                            if (children.Contains(rows[j].Sha))
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

                // Handle branch creation - if this commit has multiple children, create branch lines
                if (childrenMap.TryGetValue(currentRow.Sha, out var currentChildren) && currentChildren.Count > 1)
                {
                    // This commit has multiple children - it's a branch point
                    // The first child continues on the same lane, others get new lanes
                    for (int childIndex = 1; childIndex < currentChildren.Count; childIndex++)
                    {
                        var childSha = currentChildren[childIndex];
                        int branchLane = FindBranchLane(laneAssignments, currentLane);

                        if (!laneAssignments.ContainsKey(childSha))
                        {
                            laneAssignments[childSha] = branchLane;
                        }

                        // Draw branch line from current commit to the new lane
                        currentRow.Segments.Add(new GraphSeg
                        {
                            FromLane = currentLane,
                            ToLane = branchLane,
                            Kind = SegKind.Branch,
                            ColorIndex = branchLane
                        });

                        maxLaneUsed = Math.Max(maxLaneUsed, branchLane);
                    }
                }

                // Handle parents
                if (parents.Count == 0)
                {
                    // No parents - this is a root commit, just draw the dot
                    // No line below for root commits
                }
                else
                {
                    // First parent continues on the same lane
                    var firstParent = parents[0];
                    if (!laneAssignments.ContainsKey(firstParent))
                    {
                        laneAssignments[firstParent] = currentLane;
                    }

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
                        int mergeLane = FindMergeLane(laneAssignments, currentLane);

                        if (!laneAssignments.ContainsKey(mergeParent))
                        {
                            laneAssignments[mergeParent] = mergeLane;
                        }

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

        private static int FindBestLaneForCommit(CommitRow currentRow, IReadOnlyList<string> parents,
            Dictionary<string, int> laneAssignments, Dictionary<string, int> commitToIndex, IList<CommitRow> rows)
        {
            if (parents.Count == 0)
                return 0;

            // If any parent is already assigned a lane, try to use its lane
            foreach (var parent in parents)
            {
                if (laneAssignments.TryGetValue(parent, out int parentLane))
                {
                    return parentLane;
                }
            }

            // Find the first available lane
            int lane = 0;
            while (laneAssignments.Values.Contains(lane))
            {
                lane++;
            }
            return lane;
        }

        private static int FindMergeLane(Dictionary<string, int> laneAssignments, int currentLane)
        {
            // Find the first available lane to the right of current lane
            int lane = currentLane + 1;
            while (laneAssignments.Values.Contains(lane))
            {
                lane++;
            }
            return lane;
        }

        private static int FindBranchLane(Dictionary<string, int> laneAssignments, int currentLane)
        {
            // Find the first available lane to the right of current lane
            int lane = currentLane + 1;
            while (laneAssignments.Values.Contains(lane))
            {
                lane++;
            }
            return lane;
        }
    }
}
