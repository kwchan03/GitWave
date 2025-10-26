namespace GitGUI.Models;

public class CommitRow
{
    public CommitInfo Info { get; init; } = null!;

    // Bind-friendly pass-throughs
    public string Sha => Info.Sha;
    public string ShortSha => Sha.Length >= 7 ? Sha[..7] : Sha;
    public string Message => Info.MessageShort;
    public string Author => Info.AuthorName;
    public DateTimeOffset When => Info.AuthorWhen;
    public IReadOnlyList<string> Parents => Info.ParentShas;
    public IReadOnlyList<string> Refs => Info.Refs;

    // Graph layout (UI-only)
    public int PrimaryLane { get; set; }
    public List<GraphSeg> Segments { get; } = new();

    // NEW: tells the renderer there’s a commit above on the same lane
    public bool ConnectTop { get; set; }
}

public enum SegKind { Vertical, Merge, Branch }

public class GraphSeg
{
    public int FromLane { get; init; }
    public int ToLane { get; init; } // same as FromLane for vertical
    public SegKind Kind { get; init; }
    public int ColorIndex { get; init; } // usually lane index
}

public static class CommitRowFactory
{
    public static List<CommitRow> From(IEnumerable<CommitInfo> commits)
        => commits.Select(c => new CommitRow { Info = c }).ToList();
}
