using OpenTap;
using System.ComponentModel;

namespace GitGUI.Services
{
    // -----------------------------
    // Result models
    // -----------------------------
    public enum StepChangeKind { Unchanged, Added, Removed, Modified }

    public sealed class PropertyChange
    {
        public string Name { get; init; } = "";
        public object? Before { get; init; }
        public object? After { get; init; }
    }

    public sealed class TestStepDiff
    {
        public string Path { get; init; } = "";                // e.g. Root/Group/Type:Name/#1
        public ITestStep? Before { get; init; }
        public ITestStep? After { get; init; }
        public StepChangeKind Kind { get; init; } = StepChangeKind.Unchanged;
        public IReadOnlyList<PropertyChange> PropertyChanges { get; init; } = Array.Empty<PropertyChange>();
    }

    // -----------------------------
    // Diff service
    // -----------------------------
    public static class TestPlanDiffService
    {
        private static void LogPlanSummary(string tag, TestPlan? p)
        {
            if (p == null) { Console.WriteLine($"{tag}: <NULL>"); return; }
            Console.WriteLine($"{tag}: steps={p.Steps.Count}");
            foreach (var s in p.Steps.Take(5))
                Console.WriteLine($"{tag}:  - {s.GetType().FullName} :: {s.Name}");
        }
        /// <summary>
        /// Compare two TestPlans (semantic, using OpenTAP annotations for properties).
        /// </summary>
        public static List<TestStepDiff> ComparePlans(TestPlan? before, TestPlan? after)
        {
            var diffs = new List<TestStepDiff>();
            LogPlanSummary("BEFORE", before);
            LogPlanSummary("AFTER ", after);

            var leftMap = BuildStepMap(before);
            var rightMap = BuildStepMap(after);

            var allKeys = new HashSet<string>(leftMap.Keys, StringComparer.Ordinal);
            allKeys.UnionWith(rightMap.Keys);

            foreach (var key in allKeys.OrderBy(k => k, StringComparer.Ordinal))
            {
                leftMap.TryGetValue(key, out var l);
                rightMap.TryGetValue(key, out var r);

                if (l == null && r != null)
                {
                    diffs.Add(new TestStepDiff
                    {
                        Path = key,
                        Before = null,
                        After = r.Step,
                        Kind = StepChangeKind.Added,
                        PropertyChanges = EnumerateDisplayPropsLikeTester(r.Step)
                            .Select(p => new PropertyChange { Name = p.Label, Before = null, After = p.Value })
                            .ToList()
                    });
                }
                else if (l != null && r == null)
                {
                    diffs.Add(new TestStepDiff
                    {
                        Path = key,
                        Before = l.Step,
                        After = null,
                        Kind = StepChangeKind.Removed,
                        PropertyChanges = EnumerateDisplayPropsLikeTester(l.Step)
                            .Select(p => new PropertyChange { Name = p.Label, Before = p.Value, After = null })
                            .ToList()
                    });
                }
                else if (l != null && r != null)
                {
                    var propDiffs = DiffStepProperties(l.Step, r.Step);
                    diffs.Add(new TestStepDiff
                    {
                        Path = key,
                        Before = l.Step,
                        After = r.Step,
                        Kind = propDiffs.Count == 0 ? StepChangeKind.Unchanged : StepChangeKind.Modified,
                        PropertyChanges = propDiffs
                    });
                }
            }

            return diffs;
        }

        // -----------------------------
        // Step identity & traversal
        // -----------------------------

        private sealed class StepEntry
        {
            public string Key { get; init; } = "";
            public ITestStep Step { get; init; } = default!;
        }

        /// <summary>
        /// Build a stable map of "path keys" → step. Keys are built as a hierarchy of "Type:Name" and
        /// disambiguated with occurrence counters to make matching robust against sibling renames/reorders.
        /// </summary>
        private static Dictionary<string, StepEntry> BuildStepMap(TestPlan? plan)
        {
            var map = new Dictionary<string, StepEntry>(StringComparer.Ordinal);
            if (plan == null) return map;

            var stack = new Stack<(ITestStep step, string parentKey)>();
            for (int i = plan.Steps.Count - 1; i >= 0; i--)
                stack.Push((plan.Steps[i], "Root"));

            while (stack.Count > 0)
            {
                var (step, parentKey) = stack.Pop();

                string typePart = step.GetType().FullName ?? step.GetType().Name;

                // ✅ Use the built-in persistent Id from OpenTAP (same one in XML)
                string idPart = step.Id != Guid.Empty ? step.Id.ToString() : Guid.NewGuid().ToString();

                // Path is now stable even if Name changes
                string thisKey = $"{parentKey}/{typePart}:{idPart}";

                map[thisKey] = new StepEntry { Key = thisKey, Step = step };

                // push children
                for (int i = step.ChildTestSteps.Count - 1; i >= 0; i--)
                    stack.Push((step.ChildTestSteps[i], thisKey));
            }

            return map;
        }

        // -----------------------------
        // Property enumeration (OpenTAP annotations — TUI-like)
        // -----------------------------

        // For Added/Removed (display-only)
        private sealed record DisplayProp(string Label, object? Value);

        private static IEnumerable<DisplayProp> EnumerateDisplayPropsLikeTester(object obj)
        {
            var ann = AnnotationCollection.Annotate(obj);
            var members = GetVisibleEditableMembersLikeTui(ann);

            foreach (var x in members)
            {
                var member = x.Get<IMemberAnnotation>()!.Member;
                var label = x.Get<DisplayAttribute>()?.Name ?? member.Name;

                object? value =
                    (object?)x.Get<IStringReadOnlyValueAnnotation>()?.Value ??
                    (object?)x.Get<IStringValueAnnotation>()?.Value ??
                    x.Get<IObjectValueAnnotation>()?.Value;

                yield return new DisplayProp(label, value);
            }
        }

        // ---------- Property diff for Modified ----------
        private static List<PropertyChange> DiffStepProperties(ITestStep? before, ITestStep? after)
        {
            var leftList = before != null ? EnumerateAnnotatedPropsTuiFiltered(before).ToList() : new List<AnnotatedProp>();
            var rightList = after != null ? EnumerateAnnotatedPropsTuiFiltered(after).ToList() : new List<AnnotatedProp>();

            var left = leftList.ToDictionary(p => p.Key, p => p, StringComparer.Ordinal);
            var right = rightList.ToDictionary(p => p.Key, p => p, StringComparer.Ordinal);

            var keys = new HashSet<string>(left.Keys, StringComparer.Ordinal);
            keys.UnionWith(right.Keys);

            var changes = new List<PropertyChange>();
            foreach (var k in keys.OrderBy(x => x, StringComparer.Ordinal))
            {
                left.TryGetValue(k, out var l);
                right.TryGetValue(k, out var r);

                var label = l?.Label ?? r?.Label ?? k;
                var lv = l?.Value;
                var rv = r?.Value;

                if (!AreValuesEqual(lv, rv))
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

        // Enumerate properties like TUI (no duplicate Description) with unique keys
        private sealed record AnnotatedProp(string Key, string Label, object? Value, bool Writable);

        private static IEnumerable<AnnotatedProp> EnumerateAnnotatedPropsTuiFiltered(object obj)
        {
            var ann = AnnotationCollection.Annotate(obj);
            foreach (var x in GetVisibleEditableMembersLikeTui(ann))
            {
                var member = x.Get<IMemberAnnotation>()!.Member;

                // ITypeData has no FullName; use Name for technical key
                var declaring = member.DeclaringType?.Name ?? "";
                var key = string.IsNullOrEmpty(declaring) ? member.Name : $"{declaring}.{member.Name}";
                var label = x.Get<DisplayAttribute>()?.Name ?? member.Name;

                object? value =
                    (object?)x.Get<IStringReadOnlyValueAnnotation>()?.Value ??
                    (object?)x.Get<IStringValueAnnotation>()?.Value ??
                    x.Get<IObjectValueAnnotation>()?.Value;

                yield return new AnnotatedProp(key, label, value, member.Writable);
            }
        }

        /// <summary>
        /// Mirrors OpenTAP TUI's PropertiesView.getMembers() selection:
        /// - Visible (IAccessAnnotation.IsVisible) OR Resource.Name
        /// - Exclude Submit button-like members
        /// - Apply FilterMemberLikeTui (Browsable, XmlIgnore, Writable)
        /// - NOTE: We EXCLUDE [Output] here to match grid-like behavior (one 'Description')
        /// </summary>
        private static AnnotationCollection[] GetVisibleEditableMembersLikeTui(AnnotationCollection annotations)
        {
            var resourceTypeData = TypeData.FromType(typeof(IResource));

            return annotations?.Get<IMembersAnnotation>()?.Members
                // 1) Visibility gate (and allow Resource.Name)
                .Where(x =>
                {
                    var member = x.Get<IMemberAnnotation>()!.Member;
                    if (member.DeclaringType.DescendsTo(resourceTypeData) && member.Name == nameof(Resource.Name))
                        return true;

                    return x.Get<IAccessAnnotation>()?.IsVisible ?? false;
                })
                // 2) Exclude Submit “button-like” members
                .Where(x =>
                {
                    var member = x.Get<IMemberAnnotation>()?.Member;
                    if (member == null) return false;

                    bool isSubmitWithChoices =
                        member.GetAttribute<SubmitAttribute>() != null &&
                        x.Get<IAvailableValuesAnnotationProxy>() != null;

                    if (isSubmitWithChoices) return false;

                    // 3) Final TUI-style filter
                    return FilterMemberLikeTui(member);
                })
                .ToArray() ?? Array.Empty<AnnotationCollection>();
        }

        // Same semantics as the TUI grid (we EXCLUDE outputs to avoid the second "Description")
        private static bool FilterMemberLikeTui(IMemberData member)
        {
            var resourceTypeData = TypeData.FromType(typeof(IResource));
            if (member.DeclaringType.DescendsTo(resourceTypeData) && member.Name == nameof(Resource.Name))
                return true;

            // Respect [Browsable(false)]
            var browsable = member.GetAttribute<BrowsableAttribute>();
            if (browsable is { Browsable: false })
                return false;

            // Exclude outputs to match grid/tester behavior (prevents duplicate "Description")
            if (member.HasAttribute<OutputAttribute>())
                return false;

            // Hide [XmlIgnore]
            bool xmlIgnored = member.Attributes.Any(a => a is System.Xml.Serialization.XmlIgnoreAttribute);
            if (xmlIgnored) return false;

            // Only writable members
            return member.Writable;
        }

        // ---------- Equality helper ----------
        private static bool AreValuesEqual(object? a, object? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            if (Equals(a, b)) return true;

            if (a is string sa && b is string sb)
                return string.Equals(sa, sb, StringComparison.Ordinal);

            return false;
        }
    }
}
