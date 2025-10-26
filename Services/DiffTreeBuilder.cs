// Services/DiffTreeBuilder.cs
using GitGUI.Models;
using OpenTap;
using System.Collections.ObjectModel;
using System.Windows.Media;
using static GitGUI.Services.TestPlanDiffService;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

namespace GitGUI.Services
{
    public static class DiffTreeBuilder
    {
        public static (ObservableCollection<StepNode> beforeRoots, ObservableCollection<StepNode> afterRoots) BuildTrees(TestPlan before, TestPlan after)
        {
            // ComparePlans returns List<StepDiff>
            var diffs = ComparePlans(before, after);

            // Index by Id for quick lookup while building both trees
            var diffById = diffs.ToDictionary(d => d.Id, d => d);

            // Colors
            Brush added = new SolidColorBrush(Color.FromRgb(220, 255, 220)); // green-ish
            Brush removed = new SolidColorBrush(Color.FromRgb(255, 228, 225)); // red-ish
            Brush modified = new SolidColorBrush(Color.FromRgb(255, 249, 196)); // yellow-ish
            Brush neutral = System.Windows.Media.Brushes.Transparent;

            StepNode BuildNode(ITestStep s, bool isAfterSide)
            {
                diffById.TryGetValue(s.Id.ToString("D"), out var d);

                var kind = d?.Kind ?? StepChangeKind.Unchanged;
                Brush bg = kind switch
                {
                    StepChangeKind.Added => isAfterSide ? added : neutral, // only on after
                    StepChangeKind.Removed => isAfterSide ? neutral : removed, // only on before
                    StepChangeKind.Modified => modified,
                    _ => neutral
                };

                var vm = new StepNode(s.Id, s.Name ?? "", s.GetType().Name, kind, bg);

                // For Modified steps, show per-property rows on both sides
                if (kind == StepChangeKind.Modified && d is not null)
                {
                    foreach (var pc in d.PropertyChanges.OrderBy(x => x.Name, StringComparer.Ordinal))
                    {
                        var val = isAfterSide ? pc.After : pc.Before;
                        vm.Children.Add(new PropertyNode(pc.Name, ToDisplay(val), bg));
                    }
                }

                // If you want props for Added/Removed too, uncomment:
                if (kind == StepChangeKind.Added && isAfterSide && d is not null)
                {
                    foreach (var pc in d.PropertyChanges.OrderBy(x => x.Name, StringComparer.Ordinal))
                        vm.Children.Add(new PropertyNode(pc.Name, ToDisplay(pc.After), added));
                }
                if (kind == StepChangeKind.Removed && !isAfterSide && d is not null)
                {
                    foreach (var pc in d.PropertyChanges.OrderBy(x => x.Name, StringComparer.Ordinal))
                        vm.Children.Add(new PropertyNode(pc.Name, ToDisplay(pc.Before), removed));
                }

                // Recurse (keeps plan order)
                foreach (var c in s.ChildTestSteps)
                    vm.Children.Add(BuildNode(c, isAfterSide));

                return vm;
            }

            var beforeRoots = new ObservableCollection<StepNode>(before.Steps.Select(s => BuildNode(s, false)));
            var afterRoots = new ObservableCollection<StepNode>(after.Steps.Select(s => BuildNode(s, true)));

            return (beforeRoots, afterRoots);
        }

        private static string? ToDisplay(object? v)
        {
            if (v is null) return null;
            if (v is string s) return s;

            var t = v.GetType();
            if (t.IsEnum) return v.ToString();
            if (t == typeof(bool) || t == typeof(int) || t == typeof(double) ||
                t == typeof(decimal) || t == typeof(float) || t == typeof(long))
                return Convert.ToString(v);

            // Try a friendly "Name" property
            var nameProp = t.GetProperty("Name", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (nameProp?.CanRead == true)
            {
                try
                {
                    var nv = nameProp.GetValue(v);
                    return $"{t.Name}({nv})";
                }
                catch { /* ignore */ }
            }
            return v.ToString();
        }
    }
}