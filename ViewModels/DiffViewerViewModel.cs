// Models/DiffViewerViewModel.cs
using GitGUI.Models;
using System.Collections.ObjectModel;

public class DiffViewerViewModel
{
    public string Title { get; }
    public ObservableCollection<DiffRow> Rows { get; }

    public DiffViewerViewModel(IEnumerable<DiffRow> rows, string title = "Diff Viewer")
    {
        Rows = new ObservableCollection<DiffRow>(rows ?? Enumerable.Empty<DiffRow>());
        Title = title;
    }
}
