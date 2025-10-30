// GitGUI.Models/CommitRow.cs
using LibGit2Sharp;

namespace GitGUI.Models
{
    public class CommitRow
    {
        public Commit Commit { get; init; } = null!;

        // Bind-friendly pass-throughs (safe null handling)
        public string Sha => Commit?.Sha ?? string.Empty;
        public string ShortSha => Sha.Length >= 7 ? Sha[..7] : Sha;
        public string Message => Commit?.MessageShort ?? string.Empty;
        public string Author => Commit?.Author?.Name ?? string.Empty;
        public DateTimeOffset When => Commit?.Author?.When ?? default;

        // parent SHAs (materialized list)
        public IReadOnlyList<string> Parents => Commit?.Parents?.Select(p => p.Sha).ToArray() ?? Array.Empty<string>();

        // Graph layout (UI-only)
        public int PrimaryLane { get; set; }
        public List<GraphSeg> Segments { get; } = new();

        // tells the renderer there’s a commit above on the same lane
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
}
