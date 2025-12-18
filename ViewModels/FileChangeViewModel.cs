using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace GitWave.ViewModels
{
    public class FileChangeViewModel
    {
        public required string FilePath { get; init; }
        public required FileChangeStatus Status { get; set; }

        public string FileName => System.IO.Path.GetFileName(FilePath);

        public string IconGlyph
        {
            get
            {
                return Status switch
                {
                    FileChangeStatus.Added => "\uE710",     // Add icon
                    FileChangeStatus.Modified => "\uE70F",  // Edit icon
                    FileChangeStatus.Deleted => "\uE107",   // Delete icon
                    _ => ""
                };
            }
        }

        public Brush IconColor
        {
            get
            {
                return Status switch
                {
                    FileChangeStatus.Added => Brushes.Green,
                    FileChangeStatus.Modified => Brushes.Goldenrod,
                    FileChangeStatus.Deleted => Brushes.Red,
                    _ => Brushes.Black
                };
            }
        }
    }

    public enum FileChangeStatus
    {
        Added,
        Modified,
        Deleted
    }
}
