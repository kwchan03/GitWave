using System.Collections.ObjectModel;

namespace GitGUI.Models
{
    public class DiffRow
    {
        public string Label { get; set; } = "";   // e.g. "Root/Group/Delay123"
        public string Before { get; set; } = "";  // text shown in left panel
        public string After { get; set; } = "";  // text shown in right panel
        public System.Windows.Media.Brush BeforeBrush { get; set; } = System.Windows.Media.Brushes.Transparent;
        public System.Windows.Media.Brush AfterBrush { get; set; } = System.Windows.Media.Brushes.Transparent;
    }

    public class DiffViewerViewModel
    {
        public ObservableCollection<DiffRow> Rows { get; } = new();

        public DiffViewerViewModel(IEnumerable<DiffRow> rows)
        {
            foreach (var r in rows) Rows.Add(r);
        }
    }

}
