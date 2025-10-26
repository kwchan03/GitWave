using GraphX.Common.Models;
using QuikGraph;
using System.ComponentModel;

namespace GitGUI.Models
{
    // 1. Commit Vertex (Node) Model for GraphX
    public class CommitVertex : VertexBase, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        // Data from CommitInfo
        public string Sha { get; set; } = "";
        public string MessageShort { get; set; } = "";
        public string AuthorName { get; set; } = "";
        public IReadOnlyList<string> Refs { get; set; } = Array.Empty<string>();

        // UI State
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        // Required by GraphX: Unique ID
        public override string ToString() => Sha.Substring(0, 7);

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // 2. Commit Edge (Line) Model for GraphX
    public class CommitEdge : Edge<CommitVertex>
    {
        public CommitEdge(CommitVertex source, CommitVertex target)
            : base(source, target) { }
    }

    // 3. Commit Graph Model for QuickGraph
    public class CommitGraph : AdjacencyGraph<CommitVertex, CommitEdge> { }
}