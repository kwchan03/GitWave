using GitWave.Models;
using GitWave.ViewModels;
using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace GitWave.UI.Controls
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
        #endregion

        #region Rendering Constants

        private const double DotRadius = 3.5;
        private const double LineWidth = 2.0;
        private const double DotBorderWidth = 2.0;

        private static readonly Color[] BranchColors = new[]
        {
            Color.FromRgb(1, 10, 64),     // #010A40
            Color.FromRgb(252, 66, 201),  // #FC42C9
            Color.FromRgb(61, 145, 240),  // #3D91F0
            Color.FromRgb(41, 227, 193),  // #29E3C1
            Color.FromRgb(197, 161, 90),  // #C5A15A
            Color.FromRgb(250, 121, 120), // #FA7978
            Color.FromRgb(93, 98, 128),   // #5D6280
            Color.FromRgb(90, 197, 141),  // #5AC58D
            Color.FromRgb(92, 90, 197),   // #5C5AC5
            Color.FromRgb(235, 115, 64)   // #EB7340
        };

        // Cached brushes and pens for performance
        private static readonly Brush[] CachedBrushes;
        private static readonly Pen[] CachedPens;
        private static readonly Pen DotBorderPen;

        static GraphCell()
        {
            // Freeze brushes and pens for better performance
            CachedBrushes = BranchColors.Select(c =>
            {
                var b = new SolidColorBrush(c);
                b.Freeze();
                return b;
            }).ToArray();

            CachedPens = CachedBrushes.Select(b =>
            {
                var p = new Pen(b, LineWidth)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round
                };
                p.Freeze();
                return p;
            }).ToArray();

            DotBorderPen = new Pen(Brushes.White, DotBorderWidth);
            DotBorderPen.Freeze();
        }
        #endregion

        public GraphCell()
        {
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
        }

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            //base.OnRender(dc);

            if (Row == null) return;

            // Safety check for size
            if (RenderSize.Width <= 0 || RenderSize.Height <= 0) return;

            var renderer = new CellRenderer(dc, Row, LaneWidth, RenderSize.Height);
            renderer.Render();
        }

        private sealed class CellRenderer
        {
            private readonly DrawingContext _dc;
            private readonly CommitRowViewModel _row;
            private readonly double _laneWidth;
            private readonly double _midY;
            private readonly double _topY;
            private readonly double _bottomY;

            public CellRenderer(DrawingContext dc, CommitRowViewModel row, double laneWidth, double height)
            {
                _dc = dc;
                _row = row;
                _laneWidth = laneWidth;
                _midY = height * 0.5;
                _topY = -1.0; // Extend lines slightly outside bounds to prevent anti-aliasing gaps between rows
                _bottomY = height + 1.0;
            }

            public void Render()
            {
                // 2. Middle: Curves (Merge & Branch)
                DrawConnections();

                // 4. Foreground: Commit Dot
                DrawCommitDot();
            }

            private double GetLaneX(int lane) => (lane + 0.5) * _laneWidth;

            private Brush GetBrush(int colorIndex) => CachedBrushes[colorIndex % CachedBrushes.Length];

            private Pen GetPen(int colorIndex) => CachedPens[colorIndex % CachedPens.Length];

            private void DrawConnections()
            {
                foreach (var seg in _row.Segments)
                {
                    if (seg.Kind == SegmentKind.Vertical)
                    {
                        DrawVerticalSegment(seg);
                    }

                    var pen = GetPen(seg.ColorIndex);

                    if (seg.Kind == SegmentKind.Merge)
                    {
                        // Child (Mid) -> Parent (Bottom)
                        double xChild = GetLaneX(seg.FromLane);
                        double xParent = GetLaneX(seg.ToLane);
                        DrawBezierCurve(_dc, pen, xChild, _midY, xParent, _bottomY);
                    }
                    else if (seg.Kind == SegmentKind.Branch)
                    {
                        // Child (Top) -> Parent (Mid)
                        // Note: FromLane is Child, ToLane is Parent (Me)
                        double xChild = GetLaneX(seg.FromLane);
                        double xParent = GetLaneX(seg.ToLane);
                        DrawBezierCurve(_dc, pen, xChild, _topY, xParent, _midY);
                    }
                }
            }

            private void DrawVerticalSegment(GraphSegment seg)
            {
                double x = GetLaneX(seg.FromLane);
                var pen = GetPen(seg.ColorIndex);

                bool hasTop = seg.IsEnd;
                bool hasBottom = seg.IsStart;

                if (hasTop)
                    _dc.DrawLine(pen, new Point(x, _topY), new Point(x, _midY));    // Ends Here
                else if (hasBottom)
                    _dc.DrawLine(pen, new Point(x, _midY), new Point(x, _bottomY)); // Starts Here
                else
                    _dc.DrawLine(pen, new Point(x, _topY), new Point(x, _bottomY));   // Full Pass-Through
            }

            private void DrawCurvedSegment(GraphSegment seg)
            {
                var pen = GetPen(seg.ColorIndex);

                // FromLane = Commit Lane (Mid)
                // ToLane   = Parent Lane (Bottom) 
                // (Assuming Layouter generates segments TO parents)

                double xCommit = GetLaneX(seg.FromLane);
                double xParent = GetLaneX(seg.ToLane);

                // Draw curve from Commit (Mid) to Parent (Bottom)
                // Note: We use _bottomY to represent the direction towards the parent row
                DrawBezierCurve(_dc, pen, xCommit, _midY, xParent, _bottomY);
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
                // This logic creates the "Git Graph" look where lines leave vertically
                // and enter vertically, rather than diagonally.

                // Control point 1: Maintain X of start, move vertically towards end
                // Control point 2: Maintain X of end, move vertically from start

                double height = Math.Abs(y2 - y1);

                // Tension factor: 0.5 is standard smooth, higher makes it turn sharper later
                double tension = 0.5;
                double offset = height * tension;

                var start = new Point(x1, y1);
                var end = new Point(x2, y2);

                // If drawing Bottom -> Mid (Merge): y1 > y2
                // CP1 should go UP (negative Y)
                // If drawing Mid -> Top (Branch): y1 > y2
                // CP1 should go UP (negative Y)

                double dir = y2 > y1 ? 1.0 : -1.0;

                var cp1 = new Point(x1, y1 + offset * dir);
                var cp2 = new Point(x2, y2 - offset * dir);

                // Optimization: Use StreamGeometry for complex paths if heavily used, 
                // but for single curves, DrawGeometry with simple Bezier is fine.
                // However, direct DrawGeometry with explicit points is slightly heavier than needed
                // but simplest to implement without StreamGeometryContext boiler-plate.

                var bezier = new BezierSegment(cp1, cp2, end, true);
                var fig = new PathFigure(start, new[] { bezier }, false);
                var geom = new PathGeometry(new[] { fig });

                dc.DrawGeometry(null, pen, geom);

                //// Create smooth cubic bezier curve
                //// Control points are positioned to create a natural curve
                //double controlOffset = Math.Abs(y2 - y1) * 0.5;

                //var start = new Point(x1, y1);
                //var end = new Point(x2, y2);

                //// Place control points between start and end for smooth S-curve
                //var control1 = new Point(x1, y1 + (y2 > y1 ? controlOffset : -controlOffset));
                //var control2 = new Point(x2, y2 - (y2 > y1 ? controlOffset : -controlOffset));

                //var figure = new PathFigure
                //{
                //    StartPoint = start,
                //    Segments = { new BezierSegment(control1, control2, end, true) }
                //};

                //var geometry = new PathGeometry { Figures = { figure } };
                //dc.DrawGeometry(null, pen, geometry);
            }
        }

        #endregion
    }
}