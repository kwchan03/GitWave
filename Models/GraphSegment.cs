namespace GitGUI.Models
{
    /// <summary>
    /// Represents a visual connection between commits in the graph.
    /// </summary>
    public class GraphSegment
    {
        public int FromLane { get; set; }
        public int ToLane { get; set; }
        public SegmentKind Kind { get; set; }
        public int ColorIndex { get; set; }
        public bool IsStart { get; set; } = false;
        public bool IsEnd { get; set; } = false;
    }
}
