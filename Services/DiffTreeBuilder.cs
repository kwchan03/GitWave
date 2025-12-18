// Services/DiffTreeBuilder.cs
using GitWave.Models;
using OpenTap;
using System.Collections.ObjectModel;
using System.Windows.Media;
using static GitWave.Services.TestPlanDiffService;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

namespace GitWave.Services
{
    public static class DiffTreeBuilder
    {
        public static (ObservableCollection<StepNode> beforeRoots, ObservableCollection<StepNode> afterRoots)
            BuildTrees(TestPlan? before, TestPlan? after)
        {
            // ✅ Replace null with empty TestPlan for proper comparison
            before ??= CreateEmptyTestPlan();
            after ??= CreateEmptyTestPlan();

            // ✅ Colors defined once at the top
            Brush added = new SolidColorBrush(Color.FromRgb(220, 255, 220));    // green-ish
            Brush removed = new SolidColorBrush(Color.FromRgb(255, 228, 225));  // red-ish
            Brush modified = new SolidColorBrush(Color.FromRgb(255, 249, 196)); // yellow-ish
            Brush neutral = System.Windows.Media.Brushes.Transparent;

            // ✅ Handle both empty case
            if (before.Steps.Count == 0 && after.Steps.Count == 0)
            {
                return (new ObservableCollection<StepNode>(), new ObservableCollection<StepNode>());
            }

            // ✅ Both are now guaranteed to be non-null with ComparePlans able to enumerate all properties
            var diffs = ComparePlans(before, after);
            var diffById = diffs.ToDictionary(d => d.Id, d => d);

            var beforeRoots = new ObservableCollection<StepNode>(
                before.Steps.Select(s => BuildNode(s, false, diffById))
            );
            var afterRoots = new ObservableCollection<StepNode>(
                after.Steps.Select(s => BuildNode(s, true, diffById))
            );

            return (beforeRoots, afterRoots);

            // ✅ BuildNode with full access to colors and diffs
            StepNode BuildNode(ITestStep s, bool isAfterSide, Dictionary<string, TestStepDiff> diffById)
            {
                diffById.TryGetValue(s.Id.ToString("D"), out var d);

                var kind = d?.Kind ?? StepChangeKind.Unchanged;
                Brush bg = kind switch
                {
                    StepChangeKind.Added => isAfterSide ? added : neutral,       // only on after
                    StepChangeKind.Removed => isAfterSide ? neutral : removed,   // only on before
                    StepChangeKind.Modified => modified,
                    _ => neutral
                };

                var vm = new StepNode(s.Id, s.Name ?? "", s.GetType().Name, kind, bg);

                // ✅ Now d is ALWAYS populated (because ComparePlans always runs)
                if (d is not null)
                {
                    // For Modified steps: show only changed properties
                    if (kind == StepChangeKind.Modified)
                    {
                        foreach (var pc in d.PropertyChanges.OrderBy(x => x.Name, StringComparer.Ordinal))
                        {
                            var val = isAfterSide ? pc.After : pc.Before;
                            vm.Children.Add(new PropertyNode(pc.Name, ToDisplay(val), modified));
                        }
                    }

                    // For Added steps: show ALL properties with added color
                    if (kind == StepChangeKind.Added && isAfterSide)
                    {
                        foreach (var pc in d.PropertyChanges.OrderBy(x => x.Name, StringComparer.Ordinal))
                        {
                            vm.Children.Add(new PropertyNode(pc.Name, ToDisplay(pc.After), added));
                        }
                    }

                    // For Removed steps: show ALL properties with removed color
                    if (kind == StepChangeKind.Removed && !isAfterSide)
                    {
                        foreach (var pc in d.PropertyChanges.OrderBy(x => x.Name, StringComparer.Ordinal))
                        {
                            vm.Children.Add(new PropertyNode(pc.Name, ToDisplay(pc.Before), removed));
                        }
                    }
                }

                // Recurse (keeps plan order)
                foreach (var c in s.ChildTestSteps)
                    vm.Children.Add(BuildNode(c, isAfterSide, diffById));

                return vm;
            }
        }

        // ✅ Helper: Create empty TestPlan for null comparison
        private static TestPlan CreateEmptyTestPlan()
        {
            return new TestPlan();
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