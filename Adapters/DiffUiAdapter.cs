// Adapters/DiffUiAdapter.cs
using GitGUI.Models;
using GitGUI.Services; // where TestStepDiff/TestPlanDiffService are
using System.Windows.Media;

namespace YourApp.Adapters
{
    public static class DiffUiAdapter
    {
        // Theme colors
        private static readonly System.Windows.Media.Brush Added = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 255, 220));  // light green
        private static readonly System.Windows.Media.Brush Removed = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 228, 225));  // misty rose
        private static readonly System.Windows.Media.Brush Modified = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 249, 196));  // light yellow
        private static readonly System.Windows.Media.Brush Neutral = System.Windows.Media.Brushes.Transparent;

        /// <summary>
        /// Converts semantic diffs into rows for the DiffViewer UserControl.
        /// </summary>
        /// <param name="diffs">Result of TestPlanDiffService.ComparePlans</param>
        /// <param name="onlyChangedProps">When true, only show changed properties. False shows all props (if provided).</param>
        public static List<DiffRow> BuildRows(IReadOnlyList<TestStepDiff> diffs, bool onlyChangedProps = true)
        {
            var rows = new List<DiffRow>();

            // Show only interesting steps first; group by path
            foreach (var d in diffs.OrderBy(x => x.Path, StringComparer.Ordinal))
            {
                switch (d.Kind)
                {
                    case StepChangeKind.Added:
                        // Step header
                        rows.Add(Header($"Step ADDED  —  {d.Path}", Added));
                        // Properties present only on "After"
                        foreach (var pc in d.PropertyChanges.OrderBy(x => x.Name, StringComparer.Ordinal))
                        {
                            rows.Add(new DiffRow
                            {
                                Label = pc.Name,
                                Before = null,
                                After = ToDisplay(pc.After),
                                BeforeBrush = Neutral,
                                AfterBrush = Added
                            });
                        }
                        break;

                    case StepChangeKind.Removed:
                        rows.Add(Header($"Step REMOVED  —  {d.Path}", Removed));
                        foreach (var pc in d.PropertyChanges.OrderBy(x => x.Name, StringComparer.Ordinal))
                        {
                            rows.Add(new DiffRow
                            {
                                Label = pc.Name,
                                Before = ToDisplay(pc.Before),
                                After = null,
                                BeforeBrush = Removed,
                                AfterBrush = Neutral
                            });
                        }
                        break;

                    case StepChangeKind.Modified:
                        rows.Add(Header($"Step MODIFIED  —  {d.Path}", Modified));

                        // Only changed properties provided by the diff service
                        foreach (var pc in d.PropertyChanges.OrderBy(x => x.Name, StringComparer.Ordinal))
                        {
                            rows.Add(new DiffRow
                            {
                                Label = pc.Name,
                                Before = ToDisplay(pc.Before),
                                After = ToDisplay(pc.After),
                                BeforeBrush = Modified,
                                AfterBrush = Modified
                            });
                        }
                        break;

                    case StepChangeKind.Unchanged:
                        if (!onlyChangedProps)
                        {
                            // Optional: show unchanged header and nothing else
                            rows.Add(Header($"Step  —  {d.Path}", Neutral));
                        }
                        break;
                }
            }

            if (rows.Count == 0)
            {
                rows.Add(new DiffRow
                {
                    Label = "No differences found.",
                    Before = "—",
                    After = "—",
                    BeforeBrush = Neutral,
                    AfterBrush = Neutral
                });
            }

            return rows;
        }

        private static DiffRow Header(string title, System.Windows.Media.Brush shade) => new DiffRow
        {
            Label = title,
            Before = "",
            After = "",
            BeforeBrush = shade,
            AfterBrush = shade
        };

        private static string? ToDisplay(object? v)
        {
            if (v is null) return null;
            if (v is string s) return s;
            var t = v.GetType();

            // common displayables
            if (t.IsEnum) return v.ToString();
            if (t == typeof(bool) || t == typeof(int) || t == typeof(double) ||
                t == typeof(decimal) || t == typeof(float) || t == typeof(long))
                return Convert.ToString(v);

            // try Name property as a friendly label
            var nameProp = t.GetProperty("Name", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (nameProp?.CanRead == true)
            {
                try
                {
                    var nv = nameProp.GetValue(v);
                    return $"{t.Name}({nv})";
                }
                catch { /*ignore*/ }
            }
            return v.ToString();
        }
    }
}
