using System.Collections.ObjectModel;
using Brush = System.Windows.Media.Brush;

namespace GitGUI.Models
{
    public abstract class DiffTreeNode
    {
        public ObservableCollection<DiffTreeNode> Children { get; } = new();
        public Brush Background { get; }
        public string Display { get; protected set; } = "";

        protected DiffTreeNode(Brush background)
        {
            Background = background;
        }
    }
}
