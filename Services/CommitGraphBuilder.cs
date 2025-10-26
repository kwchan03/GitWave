using GitGUI.Models;

namespace GitGUI.Services
{
    public static class CommitGraphBuilder
    {
        public static List<CommitRow> Build(IReadOnlyList<CommitInfo> commits)
        {
            var rows = new List<CommitRow>(commits.Count);

            // Active lanes by index → commit SHA (top of line)
            var activeLanes = new List<string>();

            // Color index is usually same as lane index, but you can remap later if needed
            var laneColor = new Dictionary<int, int>();

            foreach (var c in commits)
            {
                var row = new CommitRow { Info = c };

                // 1️⃣ Assign lane
                int lane;
                if (activeLanes.Contains(c.Sha))
                {
                    // Continue same lane
                    lane = activeLanes.IndexOf(c.Sha);
                }
                else
                {
                    // Reuse first empty lane if available
                    lane = activeLanes.IndexOf(null!);
                    if (lane == -1)
                    {
                        lane = activeLanes.Count;
                        activeLanes.Add(c.Sha);
                        laneColor[lane] = lane; // color = lane index
                    }
                    else
                    {
                        activeLanes[lane] = c.Sha;
                    }
                }

                row.PrimaryLane = lane;

                // 2️⃣ ConnectTop = true if same lane was already active above
                row.ConnectTop = true;

                // 3️⃣ Build vertical pass-through for all active lanes
                for (int i = 0; i < activeLanes.Count; i++)
                {
                    if (i == lane) continue; // skip main line
                    if (activeLanes[i] != null)
                    {
                        row.Segments.Add(new GraphSeg
                        {
                            FromLane = i,
                            ToLane = i,
                            Kind = SegKind.Vertical,
                            ColorIndex = laneColor[i]
                        });
                    }
                }

                // 4️⃣ Add merge / branch segments to parents
                if (c.ParentShas is { Count: > 0 })
                {
                    // First parent continues on same lane
                    var firstParent = c.ParentShas[0];
                    activeLanes[lane] = firstParent;

                    row.Segments.Add(new GraphSeg
                    {
                        FromLane = lane,
                        ToLane = lane,
                        Kind = SegKind.Vertical,
                        ColorIndex = laneColor[lane]
                    });

                    // Other parents = merges → allocate new lanes if needed
                    for (int i = 1; i < c.ParentShas.Count; i++)
                    {
                        var parentSha = c.ParentShas[i];

                        // Try to reuse an empty slot
                        int mergeLane = activeLanes.IndexOf(null!);
                        if (mergeLane == -1)
                        {
                            mergeLane = activeLanes.Count;
                            activeLanes.Add(parentSha);
                            laneColor[mergeLane] = mergeLane;
                        }
                        else
                        {
                            activeLanes[mergeLane] = parentSha;
                        }

                        row.Segments.Add(new GraphSeg
                        {
                            FromLane = lane,
                            ToLane = mergeLane,
                            Kind = SegKind.Merge,
                            ColorIndex = laneColor[lane]
                        });
                    }
                }
                else
                {
                    // No parents — this branch line ends
                    activeLanes[lane] = null!;
                }

                rows.Add(row);
            }

            return rows;
        }
    }
}
