namespace GitGUI.Models
{
    public class CommitInfo
    {
        public string Sha { get; init; } = "";
        public string MessageShort { get; init; } = "";
        public string AuthorName { get; init; } = "";
        public DateTimeOffset AuthorWhen { get; init; }
        public IReadOnlyList<string> ParentShas { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Refs { get; init; } = Array.Empty<string>();
    }
}
