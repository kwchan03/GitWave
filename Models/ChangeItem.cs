namespace GitWave.Models
{
    public class ChangeItem
    {
        public string FilePath { get; set; }
        public ChangeStatus Status { get; set; }
        public bool IsStaged { get; set; }
    }

    public enum ChangeStatus
    {
        Untracked,  // New file not yet tracked
        Modified,   // File modified
        Added,      // File added
        Deleted,    // File deleted
        Renamed,    // File renamed
        Conflicted, // Merge conflict
        Ignored     // File ignored by gitignore
    }
}
