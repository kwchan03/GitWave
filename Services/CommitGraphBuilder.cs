//// GitWave.Services/CommitGraphBuilder.cs
//using GitWave.Models;
//using LibGit2Sharp;

//namespace GitWave.Services
//{
//    public static class CommitGraphBuilder
//    {
//        /// <summary>
//        /// Build CommitRow list (with lanes & segments) from LibGit2Sharp.Commit objects.
//        /// </summary>
//        public static List<CommitRow> BuildFromCommits(IEnumerable<Commit> commits)
//        {
//            // Materialize commits (repo lifetime safety + indexing)
//            var commitList = commits.ToList();

//            // Quick index: SHA -> index in commitList (used to check "appears later")
//            var indexBySha = new Dictionary<string, int>(StringComparer.Ordinal);
//            for (int i = 0; i < commitList.Count; i++)
//                indexBySha[commitList[i].Sha] = i;

//            // Build parent -> children map (for branch-out detection)
//            var children = new Dictionary<string, List<string>>(StringComparer.Ordinal);
//            foreach (var c in commitList)
//            {
//                foreach (var p in c.Parents)
//                {
//                    if (!children.TryGetValue(p.Sha, out var lst))
//                    {
//                        lst = new List<string>();
//                        children[p.Sha] = lst;
//                    }
//                    lst.Add(c.Sha);
//                }
//            }

//            // Convert commits into rows (CommitRowFactory handles wrapping Commit)
//            var rows = CommitRowFactory.FromCommits(commitList);

//            // Active lanes state: each slot holds the SHA expected in that lane (or null if free)
//            var activeLanes = new List<string?>();
//            int? prevPrimaryLane = null;

//            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
//            {
//                var row = rows[rowIndex];
//                var sha = row.Commit.Sha;
//                var parents = row.Parents?.ToArray() ?? Array.Empty<string>();

//                // 1) Find or allocate a lane for this commit
//                int laneIndex = activeLanes.IndexOf(sha);
//                if (laneIndex == -1)
//                {
//                    int free = activeLanes.IndexOf(null);
//                    if (free != -1) laneIndex = free;
//                    else
//                    {
//                        laneIndex = activeLanes.Count;
//                        activeLanes.Add(null); // placeholder, will set next line
//                    }
//                    activeLanes[laneIndex] = sha;
//                }

//                row.PrimaryLane = laneIndex;

//                // 2) Vertical rails for all active lanes
//                for (int lane = 0; lane < activeLanes.Count; lane++)
//                {
//                    if (activeLanes[lane] != null)
//                    {
//                        row.Segments.Add(new GraphSeg
//                        {
//                            FromLane = lane,
//                            ToLane = lane,
//                            Kind = SegKind.Vertical,
//                            ColorIndex = lane
//                        });
//                    }
//                }

//                // 3) Branch-out: allocate lanes for extra children (if any)
//                if (children.TryGetValue(sha, out var childList) && childList.Count > 1)
//                {
//                    // childList order is derived from commitList traversal
//                    for (int k = 1; k < childList.Count; k++)
//                    {
//                        var childSha = childList[k];

//                        // Only create a lane if that child appears later in the materialized list
//                        if (!indexBySha.ContainsKey(childSha)) continue;

//                        int newLane = activeLanes.IndexOf(null);
//                        if (newLane == -1)
//                        {
//                            newLane = activeLanes.Count;
//                            activeLanes.Add(childSha);
//                        }
//                        else
//                        {
//                            activeLanes[newLane] = childSha;
//                        }

//                        row.Segments.Add(new GraphSeg
//                        {
//                            FromLane = laneIndex,
//                            ToLane = newLane,
//                            Kind = SegKind.Branch,
//                            ColorIndex = newLane
//                        });
//                    }
//                }

//                // 4) Merge-in: handle secondary parents (parents[1..])
//                if (parents.Length > 1)
//                {
//                    for (int pIdx = 1; pIdx < parents.Length; pIdx++)
//                    {
//                        var parentSha = parents[pIdx];
//                        int parentLane = activeLanes.IndexOf(parentSha);
//                        if (parentLane == -1)
//                        {
//                            int freeLane = activeLanes.IndexOf(null);
//                            if (freeLane == -1)
//                            {
//                                parentLane = activeLanes.Count;
//                                activeLanes.Add(parentSha);
//                            }
//                            else
//                            {
//                                parentLane = freeLane;
//                                activeLanes[parentLane] = parentSha;
//                            }
//                        }

//                        row.Segments.Add(new GraphSeg
//                        {
//                            FromLane = laneIndex,
//                            ToLane = parentLane,
//                            Kind = SegKind.Merge,
//                            ColorIndex = parentLane
//                        });
//                    }
//                }

//                // 5) Advance this lane to first parent (or free it)
//                if (parents.Length > 0)
//                    activeLanes[laneIndex] = parents[0];
//                else
//                    activeLanes[laneIndex] = null;

//                // 6) Compact trailing null lanes to keep lane count minimal
//                while (activeLanes.Count > 0 && activeLanes[^1] == null)
//                    activeLanes.RemoveAt(activeLanes.Count - 1);

//                // 7) ConnectTop: did previous row occupy this same lane?
//                row.ConnectTop = (prevPrimaryLane.HasValue && prevPrimaryLane.Value == row.PrimaryLane);
//                prevPrimaryLane = row.PrimaryLane;
//            }

//            return rows;
//        }
//    }
//}
