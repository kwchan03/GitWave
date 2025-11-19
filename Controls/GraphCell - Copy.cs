using GitGUI.Models;
using GitGUI.Services;
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
    /// Improved GraphCell with enhanced rendering logic
    /// 
    /// Key improvements from original:
    /// - Respects CurveType from layout algorithm
    /// - Branch lines are now straight (not curved)
    /// - Vertical line logic simplified and unified
    /// - Better control point support
    /// - Cleaner, more maintainable code
    /// </summary>
    public sealed class GraphCellnew : FrameworkElement
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

        static GraphCellnew()
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

        public GraphCellnew()
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

            var renderer = new CellRenderer(dc, Row, LaneWidth, height);
            renderer.Render();
        }

        private sealed class CellRenderer
        {
            private readonly DrawingContext _dc;
            private readonly CommitRowViewModel _row;
            private readonly double _laneWidth;
            private readonly double _height;
            private readonly double _midY;
            private readonly double _topY;
            private readonly double _bottomY;

            public CellRenderer(DrawingContext dc, CommitRowViewModel row, double laneWidth, double height)
            {
                _dc = dc;
                _row = row;
                _laneWidth = laneWidth;
                _height = height;
                _midY = height / 2.0;
                _topY = -1.0; // Extend slightly beyond cell for seamless lines
                _bottomY = height + 1.0;
            }

            public void Render()
            {
                // Draw in specific order for proper layering:
                // 1. Vertical lines (all lanes continue straight)
                // 2. Merge lines (converging from bottom)
                // 3. Branch lines (diverging to top)
                // 4. Commit dot (foreground, always on top)

                DrawVerticalLines();
                DrawMergeLines();
                DrawBranchLines();
                DrawCommitDot();
            }

            private double GetLaneX(int lane) => (lane + 0.5) * _laneWidth;

            private Brush GetBrush(int colorIndex) => CachedBrushes[colorIndex % CachedBrushes.Length];

            private Pen GetPen(int colorIndex) => CachedPens[colorIndex % CachedPens.Length];

            /// <summary>
            /// Draw vertical lines for all lanes (pass-through and primary)
            /// These represent commits continuing on their branch
            /// 
            /// IMPROVEMENT: Now includes primary lane (no need for separate method)
            /// </summary>
            private void DrawVerticalLines()
            {
                if (_row.Segments == null) return;

                foreach (var segment in _row.Segments)
                {
                    // Draw all vertical pass-through segments
                    if (segment.Kind == SegmentKind.Vertical &&
                        segment.FromLane == segment.ToLane)
                    {
                        double x = GetLaneX(segment.FromLane);
                        var pen = GetPen(segment.ColorIndex);
                        _dc.DrawLine(pen, new Point(x, _topY), new Point(x, _bottomY));
                    }
                }
            }

            /// <summary>
            /// Draw merge lines: lines from parent lanes TO this commit
            /// These represent multiple branches converging into a merge commit
            /// 
            /// IMPROVEMENT: Now respects CurveType from layout algorithm!
            /// </summary>
            private void DrawMergeLines()
            {
                if (_row.Segments == null) return;

                foreach (var segment in _row.Segments)
                {
                    if (segment.Kind == SegmentKind.Merge)
                    {
                        double x1 = GetLaneX(segment.ToLane);   // Parent lane
                        double x2 = GetLaneX(segment.FromLane); // Commit lane
                        var pen = GetPen(segment.ColorIndex);

                        // IMPROVEMENT: Respect CurveType!
                        // Merge lines can be straight OR curved based on layout decision
                        if (segment.CurveType == CurveType.Straight)
                        {
                            // Straight line for simple merges
                            _dc.DrawLine(pen, new Point(x1, _bottomY), new Point(x2, _midY));
                        }
                        else // CurveType.Bezier
                        {
                            // Smooth curve for more complex merges
                            // Try to use explicit control points if provided
                            if (segment.ControlPoint1.HasValue && segment.ControlPoint2.HasValue)
                            {
                                DrawBezierCurveWithControlPoints(
                                    _dc, pen,
                                    new Point(x1, _bottomY),
                                    segment.ControlPoint1.Value,
                                    segment.ControlPoint2.Value,
                                    new Point(x2, _midY),
                                    _laneWidth, _height);
                            }
                            else
                            {
                                // Fall back to auto-calculated control points
                                DrawBezierCurve(_dc, pen, x1, _bottomY, x2, _midY);
                            }
                        }
                    }
                }
            }

            /// <summary>
            /// Draw branch lines: lines from this commit TO child lanes
            /// These represent a branch splitting off from the current commit
            /// 
            /// IMPROVEMENT: Branch lines are now STRAIGHT (not curved!)
            /// This makes branches look cleaner and more intentional
            /// </summary>
            private void DrawBranchLines()
            {
                if (_row.Segments == null) return;

                foreach (var segment in _row.Segments)
                {
                    if (segment.Kind == SegmentKind.Branch)
                    {
                        double x1 = GetLaneX(segment.FromLane); // Commit lane
                        double x2 = GetLaneX(segment.ToLane);   // Child lane
                        var pen = GetPen(segment.ColorIndex);

                        // IMPROVEMENT: Branch lines are straight!
                        // They cleanly split off without curves
                        _dc.DrawLine(pen, new Point(x1, _midY), new Point(x2, _topY));
                    }
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
            /// Draw a smooth cubic Bézier curve between two points with auto-calculated control points
            /// </summary>
            private static void DrawBezierCurve(
                DrawingContext dc,
                Pen pen,
                double x1, double y1,
                double x2, double y2)
            {
                // Create smooth cubic Bézier curve
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

            /// <summary>
            /// Draw a Bézier curve using explicit control points from layout algorithm
            /// This enables custom curve shapes beyond auto-calculation
            /// </summary>
            private static void DrawBezierCurveWithControlPoints(
                DrawingContext dc,
                Pen pen,
                Point start,
                (double x, double y) cp1Local,
                (double x, double y) cp2Local,
                Point end,
                double laneWidth,
                double cellHeight)
            {
                // Convert normalized control points to screen coordinates
                // Note: Control points from layout are normalized (0-1)
                // This is an example; adjust based on your layout algorithm's coordinate system

                var control1 = new Point(
                    start.X + (cp1Local.x * laneWidth),
                    start.Y + (cp1Local.y * cellHeight)
                );

                var control2 = new Point(
                    end.X + (cp2Local.x * laneWidth),
                    end.Y + (cp2Local.y * cellHeight)
                );

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