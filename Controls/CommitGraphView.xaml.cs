using GitGUI.Models;
using System.Collections;
using System.Windows;

namespace GitGUI.Controls
{
    public partial class CommitGraphView : System.Windows.Controls.UserControl
    {
        public CommitGraphView() => InitializeComponent();

        // ItemsSource
        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ItemsSource),
                typeof(IEnumerable),
                typeof(CommitGraphView));

        // SelectedItem
        public CommitRow? SelectedItem
        {
            get => (CommitRow?)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }
        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(
                nameof(SelectedItem),
                typeof(CommitRow),
                typeof(CommitGraphView),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        // RowHeight  — remeasure when it changes
        public double RowHeight
        {
            get => (double)GetValue(RowHeightProperty);
            set => SetValue(RowHeightProperty, value);
        }
        public static readonly DependencyProperty RowHeightProperty =
            DependencyProperty.Register(
                nameof(RowHeight),
                typeof(double),
                typeof(CommitGraphView),
                new FrameworkPropertyMetadata(28.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        // GraphWidth — remeasure when it changes + optional min clamp
        public double GraphWidth
        {
            get => (double)GetValue(GraphWidthProperty);
            set => SetValue(GraphWidthProperty, value);
        }
        public static readonly DependencyProperty GraphWidthProperty =
            DependencyProperty.Register(
                nameof(GraphWidth),
                typeof(double),
                typeof(CommitGraphView),
                new FrameworkPropertyMetadata(
                    64.0,
                    FrameworkPropertyMetadataOptions.AffectsMeasure,
                    propertyChangedCallback: null,
                    coerceValueCallback: CoerceGraphWidth));

        private static object CoerceGraphWidth(DependencyObject d, object baseValue)
        {
            var w = (double)baseValue;
            return w < 48.0 ? 48.0 : w; // prevent accidental collapse
        }

        // (Optional) LaneWidth DP — remove if you don't use it
        public double LaneWidth
        {
            get => (double)GetValue(LaneWidthProperty);
            set => SetValue(LaneWidthProperty, value);
        }
        public static readonly DependencyProperty LaneWidthProperty =
            DependencyProperty.Register(
                nameof(LaneWidth),
                typeof(double),
                typeof(CommitGraphView),
                new FrameworkPropertyMetadata(14.0));
    }
}
