using System.IO;
using System.Windows.Media; // Required for Brushes

namespace GitWave.Models
{
    public class ChangeItem
    {
        public string FilePath { get; set; }
        public ChangeStatus Status { get; set; }
        public bool IsStaged { get; set; }

        // Helper to get just the filename for display
        public string FileName => Path.GetFileName(FilePath);

        // 1. Icon Glyph (Segoe MDL2 Assets)
        public string IconGlyph
        {
            get
            {
                return Status switch
                {
                    ChangeStatus.Added => "\uE710", // (+) Add
                    ChangeStatus.Untracked => "\uE710", // (+) Add (Treat untracked like added)
                    ChangeStatus.Modified => "\uE70F", // (Pencil) Edit
                    ChangeStatus.Renamed => "\uE70F", // (Pencil) Rename
                    ChangeStatus.Deleted => "\uE107", // (x) Delete
                    ChangeStatus.Conflicted => "\uE7BA", // (!) Warning/Conflict
                    ChangeStatus.Ignored => "\uE71A", // (-) Minus/Ignore
                    _ => "\uE9CE"                        // (?) Unknown
                };
            }
        }

        // 2. Icon Color
        public Brush IconColor
        {
            get
            {
                return Status switch
                {
                    ChangeStatus.Added => Brushes.Green,
                    ChangeStatus.Untracked => Brushes.Green,
                    ChangeStatus.Modified => Brushes.Goldenrod,
                    ChangeStatus.Renamed => Brushes.Goldenrod,
                    ChangeStatus.Deleted => Brushes.Red,
                    ChangeStatus.Conflicted => Brushes.OrangeRed,
                    ChangeStatus.Ignored => Brushes.Gray,
                    _ => Brushes.Black
                };
            }
        }
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