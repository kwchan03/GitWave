using GitGUI.Models;
using System.Windows;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using Pen = System.Windows.Media.Pen;

namespace GitGUI.Controls
{
    public sealed class GraphCell : FrameworkElement
    {
        public GraphCell()
        {
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            _railPen.Freeze();
            _dotPen.Freeze();
        }

        // Row to render
        public static readonly DependencyProperty RowProperty =
            DependencyProperty.Register(
                nameof(Row),
                typeof(CommitRow),
                typeof(GraphCell),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public CommitRow? Row
        {
            get => (CommitRow?)GetValue(RowProperty);
            set => SetValue(RowProperty, value);
        }

        // Layout constants (must match your layout)
        private const double LaneWidth = 14.0;
        private const double DotRadius = 3.5;

        // Pens (1px for crisp rails)
        private static readonly Pen _railPen = new(Brushes.SlateGray, 1.0);
        private static readonly Pen _dotPen = new(Brushes.SlateGray, 1.0);

        // Pixel-aligned lane X
        private static double AlignX(double x) => System.Math.Round(x) + 0.5;
        private static double XForLane(int lane) => AlignX((lane + 0.5) * LaneWidth);

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (Row is null) return;
            double h = RenderSize.Height; if (h <= 0) return;


            const double LaneWidth = 14.0, DotRadius = 3.5;
            var pen = new Pen(Brushes.SlateGray, 1.0);

            double AlignX(double x) => Math.Round(x) + 0.5;
            double X(int lane) => AlignX((lane + 0.5) * LaneWidth);

            double top = -1.0, bottom = h + 1.0, mid = h / 2.0;

            // 1) Draw vertical lines for all active lanes (pass-through lanes)
            if (Row.Segments is { Count: > 0 })
            {
                foreach (var s in Row.Segments)
                {
                    if (s.Kind == SegKind.Vertical)
                    {
                        var x = X(s.FromLane);
                        // Draw full height vertical line for pass-through lanes
                        dc.DrawLine(pen, new System.Windows.Point(x, top), new System.Windows.Point(x, bottom));
                    }
                }
            }

            // 2) Draw merge lines (curved connections from current commit to merge parents)
            if (Row.Segments is { Count: > 0 })
            {
                foreach (var s in Row.Segments)
                {
                    if (s.Kind == SegKind.Merge)
                    {
                        var x1 = X(s.FromLane);
                        var x2 = X(s.ToLane);
                        
                        // Draw curved merge line from current commit to merge parent
                        DrawCurvedMergeLine(dc, pen, x1, mid, x2, bottom);
                    }
                }
            }

            // 2.5) Draw branch lines (curved connections from current commit to branch children)
            if (Row.Segments is { Count: > 0 })
            {
                foreach (var s in Row.Segments)
                {
                    if (s.Kind == SegKind.Branch)
                    {
                        var x1 = X(s.FromLane);
                        var x2 = X(s.ToLane);
                        
                        // Draw curved branch line from current commit to branch child
                        DrawCurvedBranchLine(dc, pen, x1, mid, x2, bottom);
                    }
                }
            }

            // 3) If the primary lane continues from above, connect top -> dot
            if (Row.ConnectTop)
            {
                var x = X(Row.PrimaryLane);
                dc.DrawLine(pen, new System.Windows.Point(x, top), new System.Windows.Point(x, mid));
            }

            // 4) Draw the main vertical line to the first parent (if any)
            var parents = Row.Parents ?? Array.Empty<string>();
            if (parents.Count > 0)
            {
                var x = X(Row.PrimaryLane);
                dc.DrawLine(pen, new System.Windows.Point(x, mid), new System.Windows.Point(x, bottom));
            }

            // 5) Draw the commit dot
            var xDot = X(Row.PrimaryLane);
            var dotBrush = Row.Refs?.Count > 0 ? Brushes.Orange : Brushes.White;
            dc.DrawEllipse(dotBrush, pen, new System.Windows.Point(xDot, mid), DotRadius, DotRadius);
        }

        private void DrawCurvedMergeLine(DrawingContext dc, Pen pen, double x1, double y1, double x2, double y2)
        {
            try
            {
                // Create a curved path for merge lines
                var pathGeometry = new PathGeometry();
                var pathFigure = new PathFigure();
                
                pathFigure.StartPoint = new System.Windows.Point(x1, y1);
                
                // Create a smooth curve using Bezier curves
                var controlPoint1 = new System.Windows.Point(x1, y1 + (y2 - y1) * 0.3);
                var controlPoint2 = new System.Windows.Point(x2, y1 + (y2 - y1) * 0.7);
                
                var bezierSegment = new BezierSegment(controlPoint1, controlPoint2, new System.Windows.Point(x2, y2), true);
                pathFigure.Segments.Add(bezierSegment);
                
                pathGeometry.Figures.Add(pathFigure);
                dc.DrawGeometry(null, pen, pathGeometry);
            }
            catch (Exception ex)
            {
                // Fallback to straight line if curve fails
                System.Diagnostics.Debug.WriteLine($"Failed to draw curved merge line: {ex.Message}");
                dc.DrawLine(pen, new System.Windows.Point(x1, y1), new System.Windows.Point(x2, y2));
            }
        }

        private void DrawCurvedBranchLine(DrawingContext dc, Pen pen, double x1, double y1, double x2, double y2)
        {
            try
            {
                // Create a curved path for branch lines
                var pathGeometry = new PathGeometry();
                var pathFigure = new PathFigure();
                
                pathFigure.StartPoint = new System.Windows.Point(x1, y1);
                
                // Create a smooth curve using Bezier curves for branch lines
                var controlPoint1 = new System.Windows.Point(x1, y1 + (y2 - y1) * 0.2);
                var controlPoint2 = new System.Windows.Point(x2, y1 + (y2 - y1) * 0.8);
                
                var bezierSegment = new BezierSegment(controlPoint1, controlPoint2, new System.Windows.Point(x2, y2), true);
                pathFigure.Segments.Add(bezierSegment);
                
                pathGeometry.Figures.Add(pathFigure);
                dc.DrawGeometry(null, pen, pathGeometry);
            }
            catch (Exception ex)
            {
                // Fallback to straight line if curve fails
                System.Diagnostics.Debug.WriteLine($"Failed to draw curved branch line: {ex.Message}");
                dc.DrawLine(pen, new System.Windows.Point(x1, y1), new System.Windows.Point(x2, y2));
            }
        }
    }
}