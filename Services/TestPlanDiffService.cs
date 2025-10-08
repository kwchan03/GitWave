using GitGUI.Models;
using OpenTap;
using System.ComponentModel;

namespace GitGUI.Services
{
    public sealed class PropertyChange
    {
        public string Name { get; init; } = "";
        public object? Before { get; init; }
        public object? After { get; init; }
    }

    public sealed class TestStepDiff
    {
        public string Id { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public ITestStep? Before { get; init; }
        public ITestStep? After { get; init; }
        public StepChangeKind Kind { get; init; } = StepChangeKind.Unchanged;
        public IReadOnlyList<PropertyChange> PropertyChanges { get; init; } = Array.Empty<PropertyChange>();
    }

    public static class TestPlanDiffService
    {
        // Compare two TestPlans using annotations (semantic diff)
        public static List<TestStepDiff> ComparePlans(TestPlan? before, TestPlan? after)
        {
            var diffs = new List<TestStepDiff>();
            var beforeMap = BuildStepMap(before);
            var afterMap = BuildStepMap(after);

            var allIds = new HashSet<string>(beforeMap.Keys);
            allIds.UnionWith(afterMap.Keys);

            foreach (var id in allIds)
            {
                var hasBefore = beforeMap.TryGetValue(id, out var b);
                var hasAfter = afterMap.TryGetValue(id, out var a);

                if (!hasBefore && hasAfter)
                {
                    // ADDED
                    diffs.Add(new TestStepDiff
                    {
                        Id = id,
                        DisplayName = FriendlyName(a!.Step),
                        Before = null,
                        After = a.Step,
                        Kind = StepChangeKind.Added,
                        PropertyChanges = EnumerateProps(a.Step)
                            .Select(p => new PropertyChange { Name = p.Label, Before = null, After = p.Value })
                            .ToList()
                    });
                }
                else if (hasBefore && !hasAfter)
                {
                    // REMOVED
                    diffs.Add(new TestStepDiff
                    {
                        Id = id,
                        DisplayName = FriendlyName(b!.Step),
                        Before = b.Step,
                        After = null,
                        Kind = StepChangeKind.Removed,
                        PropertyChanges = EnumerateProps(b.Step)
                            .Select(p => new PropertyChange { Name = p.Label, Before = p.Value, After = null })
                            .ToList()
                    });
                }
                else if (hasBefore && hasAfter)
                {
                    // EXIST IN BOTH → check property changes
                    var changes = DiffStepProperties(b!.Step, a!.Step);
                    diffs.Add(new TestStepDiff
                    {
                        Id = id,
                        DisplayName = FriendlyName(a.Step),
                        Before = b.Step,
                        After = a.Step,
                        Kind = changes.Count == 0 ? StepChangeKind.Unchanged : StepChangeKind.Modified,
                        PropertyChanges = changes
                    });
                }
            }

            // Preserve AFTER test plan order (natural traversal)
            var order = BuildOrderMap(after);
            return diffs
                .OrderBy(d => order.TryGetValue(d.Id, out var idx) ? idx : int.MaxValue)
                .ToList();
        }

        // Build map of step GUIDs to structure (flattened traversal)
        private sealed class StepEntry
        {
            public ITestStep Step { get; init; } = default!;
        }

        private static Dictionary<string, StepEntry> BuildStepMap(TestPlan? plan)
        {
            var map = new Dictionary<string, StepEntry>(StringComparer.Ordinal);
            if (plan == null) return map;

            var stack = new Stack<ITestStep>(plan.Steps.Reverse());
            while (stack.Count > 0)
            {
                var step = stack.Pop();
                var id = step.Id != Guid.Empty ? step.Id.ToString("D") : Guid.NewGuid().ToString("D");
                map[id] = new StepEntry { Step = step };

                foreach (var child in step.ChildTestSteps.Reverse())
                    stack.Push(child);
            }
            return map;
        }

        // Record preorder sequence index for after plan
        private static Dictionary<string, int> BuildOrderMap(TestPlan? plan)
        {
            var order = new Dictionary<string, int>(StringComparer.Ordinal);
            if (plan == null) return order;

            int index = 0;
            void Walk(TestStepList steps)
            {
                foreach (var s in steps)
                {
                    var id = s.Id != Guid.Empty ? s.Id.ToString("D") : Guid.NewGuid().ToString("D");
                    order[id] = index++;
                    Walk(s.ChildTestSteps);
                }
            }
            Walk(plan.Steps);
            return order;
        }

        // -----------------------------------------------
        // Property-level diff logic (same as tester class)
        // -----------------------------------------------
        private sealed record AnnotatedProp(string Key, string Label, object? Value);

        private static IEnumerable<AnnotatedProp> EnumerateProps(object obj)
        {
            var ann = AnnotationCollection.Annotate(obj);
            foreach (var x in ann?.Get<IMembersAnnotation>()?.Members ?? Array.Empty<AnnotationCollection>())
            {
                var member = x.Get<IMemberAnnotation>()?.Member;
                if (member == null) continue;
                if (!FilterMemberLikeTui(member)) continue;

                var label = x.Get<DisplayAttribute>()?.Name ?? member.Name;
                object? value =
                    (object?)x.Get<IStringReadOnlyValueAnnotation>()?.Value ??
                    (object?)x.Get<IStringValueAnnotation>()?.Value ??
                    x.Get<IObjectValueAnnotation>()?.Value;

                yield return new AnnotatedProp(member.Name, label, value);
            }
        }

        private static bool FilterMemberLikeTui(IMemberData member)
        {
            var resourceTypeData = TypeData.FromType(typeof(IResource));
            if (member.DeclaringType.DescendsTo(resourceTypeData) && member.Name == nameof(Resource.Name))
                return true;

            var browsable = member.GetAttribute<BrowsableAttribute>();
            if (browsable is { Browsable: false }) return false;

            if (member.HasAttribute<OutputAttribute>()) return false;
            bool xmlIgnored = member.Attributes.Any(a => a is System.Xml.Serialization.XmlIgnoreAttribute);
            if (xmlIgnored) return false;

            return member.Writable;
        }

        private static List<PropertyChange> DiffStepProperties(ITestStep before, ITestStep after)
        {
            var leftList = EnumerateProps(before).ToList();
            var rightList = EnumerateProps(after).ToList();

            var L = leftList.ToDictionary(p => p.Key, p => p, StringComparer.Ordinal);
            var R = rightList.ToDictionary(p => p.Key, p => p, StringComparer.Ordinal);

            var keys = new HashSet<string>(L.Keys, StringComparer.Ordinal);
            keys.UnionWith(R.Keys);

            var changes = new List<PropertyChange>();
            foreach (var k in keys.OrderBy(x => x, StringComparer.Ordinal))
            {
                L.TryGetValue(k, out var l);
                R.TryGetValue(k, out var r);

                var label = l?.Label ?? r?.Label ?? k;
                var lv = l?.Value;
                var rv = r?.Value;
                if (!AreValuesEqual(lv, rv))
                    changes.Add(new PropertyChange { Name = label, Before = lv, After = rv });
            }
            return changes;
        }

        private static bool AreValuesEqual(object? a, object? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            if (Equals(a, b)) return true;
            if (a is string sa && b is string sb) return string.Equals(sa, sb, StringComparison.Ordinal);
            return false;
        }

        private static string FriendlyName(ITestStep s)
        {
            var td = TypeData.GetTypeData(s);
            var prettyType = td.GetDisplayAttribute()?.Name ?? s.GetType().Name;
            return $"{prettyType}: {s.Name}";
        }
    }
}
