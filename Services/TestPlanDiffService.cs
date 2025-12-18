using GitWave.Models;
using OpenTap;
using System.ComponentModel;

namespace GitWave.Services
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
        // Cache to avoid re-allocating per member
        private static readonly ITypeData ResourceType = TypeData.FromType(typeof(IResource));

        // ---------- Public API ----------
        public static List<TestStepDiff> ComparePlans(TestPlan? before, TestPlan? after)
        {
            // Maps by Id for O(1) lookup
            var beforeMap = BuildStepMap(before);
            var afterMap = BuildStepMap(after);

            var diffs = new List<TestStepDiff>();

            // 1) Walk the AFTER plan (keeps UI order). Emit Added/Modified/Unchanged.
            if (after != null)
            {
                foreach (var aStep in Flatten(after.Steps))
                {
                    var id = GetId(aStep);
                    var hasBefore = beforeMap.TryGetValue(id, out var bStep);
                    if (!hasBefore)
                    {
                        // ADDED
                        diffs.Add(new TestStepDiff
                        {
                            Id = id,
                            DisplayName = FriendlyName(aStep),
                            Before = null,
                            After = aStep,
                            Kind = StepChangeKind.Added,
                            PropertyChanges = EnumerateProps(aStep)
                                .Select(p => new PropertyChange { Name = p.Label, Before = null, After = p.Value })
                                .ToList()
                        });
                    }
                    else
                    {
                        // EXISTS IN BOTH → compare properties
                        var changes = DiffStepProperties(bStep!, aStep);
                        diffs.Add(new TestStepDiff
                        {
                            Id = id,
                            DisplayName = FriendlyName(aStep),
                            Before = bStep,
                            After = aStep,
                            Kind = changes.Count == 0 ? StepChangeKind.Unchanged : StepChangeKind.Modified,
                            PropertyChanges = changes
                        });
                    }
                }
            }

            // 2) Any step in BEFORE not present in AFTER → REMOVED (preserve BEFORE order for these)
            if (before != null)
            {
                var afterIds = new HashSet<string>(afterMap.Keys, StringComparer.Ordinal);
                foreach (var bStep in Flatten(before.Steps))
                {
                    var id = GetId(bStep);
                    if (!afterIds.Contains(id))
                    {
                        diffs.Add(new TestStepDiff
                        {
                            Id = id,
                            DisplayName = FriendlyName(bStep),
                            Before = bStep,
                            After = null,
                            Kind = StepChangeKind.Removed,
                            PropertyChanges = EnumerateProps(bStep)
                                .Select(p => new PropertyChange { Name = p.Label, Before = p.Value, After = null })
                                .ToList()
                        });
                    }
                }
            }

            return diffs;
        }

        // ---------- Helpers: traversal & maps ----------
        private static IEnumerable<ITestStep> Flatten(TestStepList roots)
        {
            var stack = new Stack<ITestStep>(Enumerable.Reverse(roots)); // preorder
            while (stack.Count > 0)
            {
                var s = stack.Pop();
                yield return s;

                // push children in reverse to visit in natural order
                for (int i = s.ChildTestSteps.Count - 1; i >= 0; i--)
                    stack.Push(s.ChildTestSteps[i]);
            }
        }

        private static Dictionary<string, ITestStep> BuildStepMap(TestPlan? plan)
        {
            var map = new Dictionary<string, ITestStep>(StringComparer.Ordinal);
            if (plan == null) return map;

            foreach (var s in Flatten(plan.Steps))
                map[GetId(s)] = s;

            return map;
        }

        private static string GetId(ITestStep s)
        {
            if (s.Id == Guid.Empty)
                throw new InvalidOperationException($"Step '{s.Name}' has an empty Id. All steps should have persistent GUIDs.");
            return s.Id.ToString("D");
        }

        private static string FriendlyName(ITestStep s)
        {
            var td = TypeData.GetTypeData(s);
            var prettyType = td.GetDisplayAttribute()?.Name ?? s.GetType().Name;
            return $"{prettyType}: {s.Name}";
        }

        // ---------- Property enumeration & diff ----------
        private sealed record AnnotatedProp(string Key, string Label, string Value);

        private static IEnumerable<AnnotatedProp> EnumerateProps(object obj)
        {
            var ann = AnnotationCollection.Annotate(obj);
            var members = ann?.Get<IMembersAnnotation>()?.Members ?? Array.Empty<AnnotationCollection>();

            foreach (var x in members)
            {
                var m = x.Get<IMemberAnnotation>()?.Member;
                if (m == null) continue;
                if (!IsVisibleEditableLikeTui(x, m)) continue;

                var label = x.Get<DisplayAttribute>()?.Name ?? m.Name;

                string text =
                    x.Get<IStringReadOnlyValueAnnotation>()?.Value
                    ?? x.Get<IStringValueAnnotation>()?.Value
                    ?? x.Get<IObjectValueAnnotation>()?.Value?.ToString()
                    ?? "null";

                var key = $"{m.DeclaringType?.Name}.{m.Name}";
                yield return new AnnotatedProp(key, label, text);
            }
        }

        // Mirrors the TUI selection; also drops [Output] to avoid duplicate "Description"
        private static bool IsVisibleEditableLikeTui(AnnotationCollection node, IMemberData member)
        {
            // Allow Resource.Name
            if (member.DeclaringType.DescendsTo(ResourceType) && member.Name == nameof(Resource.Name))
                return true;

            // Respect access visibility
            if ((node.Get<IAccessAnnotation>()?.IsVisible ?? false) == false)
                return false;

            // Exclude "submit" button-like choices (same as TUI)
            bool isSubmitWithChoices =
                member.GetAttribute<SubmitAttribute>() != null &&
                node.Get<IAvailableValuesAnnotationProxy>() != null;
            if (isSubmitWithChoices) return false;

            // TUI filter
            var browsable = member.GetAttribute<BrowsableAttribute>();
            if (browsable is { Browsable: false }) return false;
            if (member.HasAttribute<OutputAttribute>()) return false; // avoid output Description
            if (member.Attributes.Any(a => a is System.Xml.Serialization.XmlIgnoreAttribute)) return false;

            return member.Writable;
        }

        private static List<PropertyChange> DiffStepProperties(ITestStep before, ITestStep after)
        {
            var left = EnumerateProps(before).ToDictionary(p => p.Key, p => p, StringComparer.Ordinal);
            var right = EnumerateProps(after).ToDictionary(p => p.Key, p => p, StringComparer.Ordinal);

            var keys = new HashSet<string>(left.Keys, StringComparer.Ordinal);
            keys.UnionWith(right.Keys);

            var changes = new List<PropertyChange>();
            foreach (var k in keys.OrderBy(x => x, StringComparer.Ordinal))
            {
                left.TryGetValue(k, out var l);
                right.TryGetValue(k, out var r);

                var label = l?.Label ?? r?.Label ?? k;
                var lv = l?.Value ?? "null";
                var rv = r?.Value ?? "null";

                if (!string.Equals(lv, rv, StringComparison.Ordinal))
                {
                    changes.Add(new PropertyChange
                    {
                        Name = label,
                        Before = lv,
                        After = rv
                    });
                }
            }
            return changes;
        }
    }
}
