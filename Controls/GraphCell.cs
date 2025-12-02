using GitGUI.Models;
using GitGUI.ViewModels;
using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace GitGUI.Controls
{
    /// <summary>
    /// Improved GraphCell with cleaner rendering logic
    /// Key improvements:
    /// - Simplified line drawing logic
    /// - Better layering (draw in correct order)
    /// - Cleaner connection handling
    /// - Better performance with cached pens
    /// </summary>
    public sealed class GraphCell : FrameworkElement
    {
        #region Dependency Properties

        public static readonly DependencyProperty RowProperty =
            DependencyProperty.Register(
                nameof(Row),
                typeof(CommitRowViewModel),
                typeof(GraphCell),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public CommitRowViewModel? Row
        {
            get => (CommitRowViewModel?)GetValue(RowProperty);
            set => SetValue(RowProperty, value);
        }

        public static readonly DependencyProperty LaneWidthProperty =
            DependencyProperty.Register(
                nameof(LaneWidth),
                typeof(double),
                typeof(GraphCell),
                new FrameworkPropertyMetadata(14.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double LaneWidth
        {
            get => (double)GetValue(LaneWidthProperty);
            set => SetValue(LaneWidthProperty, value);
        }

        /// <summary>
        /// Branch paths computed by layouter - pass from parent ItemsControl
        /// </summary>
        public static readonly DependencyProperty BranchPathsProperty =
            DependencyProperty.Register(
                nameof(BranchPaths),
                typeof(List<BranchPath>),
                typeof(GraphCell),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public List<BranchPath> BranchPaths
        {
            get => (List<BranchPath>)GetValue(BranchPathsProperty);
            set => SetValue(BranchPathsProperty, value);
        }
        #endregion

        #region Rendering Constants

        private const double DotRadius = 3.5;
        private const double LineWidth = 2.0;
        private const double DotBorderWidth = 2.0;

        // Professional color palette (inspired by GitKraken/SourceTree)
        private static readonly Color[] BranchColors = new[]
        {
            Color.FromRgb(64, 158, 255),    // Blue
            Color.FromRgb(103, 194, 58),    // Green
            Color.FromRgb(245, 166, 35),    // Orange
            Color.FromRgb(208, 2, 27),      // Red
            Color.FromRgb(126, 87, 194),    // Purple
            Color.FromRgb(0, 188, 212),     // Cyan
            Color.FromRgb(255, 152, 0),     // Amber
            Color.FromRgb(156, 39, 176),    // Deep Purple
            Color.FromRgb(0, 150, 136),     // Teal
            Color.FromRgb(255, 87, 34)      // Deep Orange
        };

        // Cached brushes and pens for performance
        private static readonly Brush[] CachedBrushes = BranchColors.Select(c => new SolidColorBrush(c)).ToArray();
        private static readonly Pen[] CachedPens = CachedBrushes.Select(b => CreatePen(b, LineWidth)).ToArray();
        private static readonly Pen DotBorderPen = CreatePen(Brushes.White, DotBorderWidth);

        static GraphCell()
        {
            // Freeze brushes and pens for better performance
            foreach (var brush in CachedBrushes)
                brush.Freeze();
            foreach (var pen in CachedPens)
                pen.Freeze();
            DotBorderPen.Freeze();
        }

        private static Pen CreatePen(Brush brush, double thickness)
        {
            var pen = new Pen(brush, thickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            return pen;
        }

        #endregion

        #region Constructor

        public GraphCell()
        {
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
        }

        #endregion

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (Row == null) return;

            double height = RenderSize.Height;
            if (height <= 0) return;

            var renderer = new CellRenderer(dc, Row, BranchPaths, LaneWidth, height);
            renderer.Render();
        }

        private sealed class CellRenderer
        {
            private readonly DrawingContext _dc;
            private readonly CommitRowViewModel _row;
            private readonly List<BranchPath> _branchPaths;
            private readonly double _laneWidth;
            private readonly double _height;
            private readonly double _midY;
            private readonly double _topY;
            private readonly double _bottomY;

            public CellRenderer(DrawingContext dc, CommitRowViewModel row, List<BranchPath> branchPaths, double laneWidth, double height)
            {
                _dc = dc;
                _row = row;
                _branchPaths = branchPaths;
                _laneWidth = laneWidth;
                _height = height;
                _midY = height / 2.0;
                _topY = -1.0; // Extend slightly beyond cell for seamless lines
                _bottomY = height + 1.0;
            }

            public void Render()
            {
                // Draw in specific order for proper layering:
                // 1. Pass-through vertical lines (background)
                // 2. Merge lines (from parent lanes to commit)
                // 3. Branch lines (from commit to child lanes)
                // 4. Primary lane connection lines (top to commit, commit to bottom)
                // 5. Commit dot (foreground, always on top)

                DrawBranchPaths();
                DrawPassThroughLines();
                DrawMergeLines();
                DrawBranchLines();
                DrawPrimaryLaneLines();
                DrawCommitDot();
            }

            private double GetLaneX(int lane) => (lane + 0.5) * _laneWidth;

            private Brush GetBrush(int colorIndex) => CachedBrushes[colorIndex % CachedBrushes.Length];

            private Pen GetPen(int colorIndex) => CachedPens[colorIndex % CachedPens.Length];

            /// <summary>
            /// Draw branch path lines that pass through this row.
            /// 
            /// For each branch path, check if this row is between two consecutive points.
            /// If so, draw the line segment from point1 to point2.
            /// 
            /// This creates continuous visual lines even through empty rows!
            /// </summary>
            private void DrawBranchPaths()
            {
                if (_branchPaths == null || _branchPaths.Count == 0)
                    return;

                foreach (var path in _branchPaths)
                {
                    // For each pair of consecutive commits in this path
                    for (int i = 0; i < path.Points.Count - 1; i++)
                    {
                        var (row1, lane1) = path.Points[i];
                        var (row2, lane2) = path.Points[i + 1];

                        // Check if current row is between row1 and row2
                        int minRow = Math.Min(row1, row2);
                        int maxRow = Math.Max(row1, row2);

                        // ✓ KEY: Draw if current row is BETWEEN the two path points
                        // This includes empty rows!
                        if (_row.Row >= minRow && _row.Row <= maxRow)
                        {
                            double x1 = GetLaneX(lane1);
                            double x2 = GetLaneX(lane2);
                            var pen = GetPen(path.ColorIndex);

                            if (x1 == x2)
                            {
                                // Vertical line (same lane - continuous through empty rows!)
                                _dc.DrawLine(pen,
                                    new Point(x1, _topY),
                                    new Point(x1, _bottomY));
                            }
                            else
                            {
                                // Curve line (different lanes - merge happening)
                                DrawBezierCurve(_dc, pen, x1, _topY, x2, _bottomY);
                            }

                            // Found the path segment for this row, move to next path
                            return;
                        }
                    }
                }
            }

            /// <summary>
            /// Draw vertical lines for lanes that pass through this row
            /// (not the primary lane, those are drawn separately)
            /// </summary>
            private void DrawPassThroughLines()
            {
                if (_row.Segments == null) return;

                foreach (var segment in _row.Segments)
                {
                    // Only draw pass-through vertical lines (not the primary lane)
                    if (segment.Kind == SegmentKind.Vertical &&
                        segment.FromLane == segment.ToLane &&
                        segment.FromLane != _row.PrimaryLane)
                    {
                        double x = GetLaneX(segment.FromLane);
                        var pen = GetPen(segment.ColorIndex);
                        _dc.DrawLine(pen, new Point(x, _topY), new Point(x, _bottomY));
                    }
                }
            }

            /// <summary>
            /// Draw merge lines: diagonal/curved lines from parent lanes TO commit
            /// These represent merge parents connecting to this commit
            /// </summary>
            private void DrawMergeLines()
            {
                if (_row.Segments == null) return;

                foreach (var segment in _row.Segments)
                {
                    if (segment.Kind == SegmentKind.Merge)
                    {
                        double x1 = GetLaneX(segment.ToLane);   // Parent lane (where line comes FROM in visual space)
                        double x2 = GetLaneX(segment.FromLane); // Commit lane (where line goes TO)
                        var pen = GetPen(segment.ColorIndex);

                        // CRITICAL FIX: Merge lines go FROM parent (bottom) TO commit (mid)
                        // The layout algorithm sets FromLane=commitLane, ToLane=parentLane
                        // But visually, we draw from bottom (parent) up to mid (commit)
                        if (x1 == x2)
                            _dc.DrawLine(pen, new Point(x1, _topY), new Point(x1, _bottomY));
                        else
                            DrawBezierCurve(_dc, pen, x1, _bottomY, x2, _midY);
                    }
                }
            }

            /// <summary>
            /// Draw branch lines: diagonal/curved lines from commit TO child lanes
            /// These represent branches splitting off from this commit
            /// </summary>
            private void DrawBranchLines()
            {
                if (_row.Segments == null) return;

                foreach (var segment in _row.Segments)
                {
                    if (segment.Kind == SegmentKind.Branch)
                    {
                        double x1 = GetLaneX(segment.FromLane); // Commit lane
                        double x2 = GetLaneX(segment.ToLane);   // Parent lane
                        var pen = GetPen(segment.ColorIndex);

                        // Branch lines go FROM commit (mid) TO child (top)
                        // The layout sets FromLane=commitLane, ToLane=childLane
                        if (x1 == x2)
                            _dc.DrawLine(pen, new Point(x1, _topY), new Point(x1, _bottomY));
                        else
                            DrawBezierCurve(_dc, pen, x1, _midY, x2, _topY);
                    }
                }
            }

            /// <summary>
            /// Draw the primary lane vertical line for this commit
            /// - Top half: from top of cell to commit dot (if HasIncomingConnection)
            /// - Bottom half: from commit dot to bottom of cell (if HasOutgoingConnection)
            /// </summary>
            private void DrawPrimaryLaneLines()
            {
                double x = GetLaneX(_row.PrimaryLane);
                var pen = GetPen(_row.BranchColorIndex);

                // Draw line from top to commit if there's an incoming connection
                if (_row.HasIncomingConnection)
                {
                    _dc.DrawLine(pen, new Point(x, _topY), new Point(x, _midY));
                }

                // Draw line from commit to bottom if there's an outgoing connection
                if (_row.HasOutgoingConnection)
                {
                    _dc.DrawLine(pen, new Point(x, _midY), new Point(x, _bottomY));
                }
            }

            /// <summary>
            /// Draw the commit dot at the center of this commit's lane
            /// </summary>
            private void DrawCommitDot()
            {
                double x = GetLaneX(_row.PrimaryLane);
                var brush = GetBrush(_row.BranchColorIndex);
                var center = new Point(x, _midY);

                // Draw dot with white border for better visibility
                _dc.DrawEllipse(brush, DotBorderPen, center, DotRadius, DotRadius);
            }

            /// <summary>
            /// Draw a smooth cubic bezier curve between two points
            /// </summary>
            private static void DrawBezierCurve(
                DrawingContext dc,
                Pen pen,
                double x1, double y1,
                double x2, double y2)
            {
                // Create smooth cubic bezier curve
                // Control points are positioned to create a natural curve
                double controlOffset = Math.Abs(y2 - y1) * 0.5;

                var start = new Point(x1, y1);
                var end = new Point(x2, y2);

                // Place control points between start and end for smooth S-curve
                var control1 = new Point(x1, y1 + (y2 > y1 ? controlOffset : -controlOffset));
                var control2 = new Point(x2, y2 - (y2 > y1 ? controlOffset : -controlOffset));

                var figure = new PathFigure
                {
                    StartPoint = start,
                    Segments = { new BezierSegment(control1, control2, end, true) }
                };

                var geometry = new PathGeometry { Figures = { figure } };
                dc.DrawGeometry(null, pen, geometry);
            }
        }

        #endregion
    }
}