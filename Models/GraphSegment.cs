using GitGUI.Services;

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
        public CurveType CurveType { get; set; } = CurveType.Straight;

        // For Bézier curves (optional)
        public (double x, double y)? ControlPoint1 { get; set; }
        public (double x, double y)? ControlPoint2 { get; set; }
    }
}
