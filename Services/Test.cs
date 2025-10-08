using OpenTap;
using System.ComponentModel;
using System.IO;
using System.Xml.Serialization;

namespace GitGUI.Services
{
    /// <summary>
    /// TUI-like inspector: loads a .TapPlan and dumps step properties using OpenTAP Annotations.
    /// </summary>
    public static class TestPlanInspector
    {
        /// <summary>
        /// Load and inspect a .TapPlan file, writing a structured dump next to it (MyPlan.dump.txt).
        /// </summary>
        public static void Run(string testPlanPath)
        {
            if (string.IsNullOrWhiteSpace(testPlanPath) || !File.Exists(testPlanPath))
            {
                Console.WriteLine("❌ Invalid TestPlan path.");
                return;
            }

            var logPath = Path.ChangeExtension(testPlanPath, ".dump.txt");
            using var writer = new StreamWriter(logPath, false);

            try
            {
                writer.WriteLine($"=== Dump of {Path.GetFileName(testPlanPath)} ===");
                writer.WriteLine($"Generated at {DateTime.Now}");
                writer.WriteLine();

                // Load the plan (equivalent to TapSerializer for full plan files)
                var plan = TestPlan.Load(testPlanPath);
                if (plan is null)
                {
                    writer.WriteLine("Failed to load TestPlan.");
                    return;
                }

                writer.WriteLine($"Test Plan: {plan.Name}");
                writer.WriteLine($"Root Steps: {plan.Steps.Count}");
                writer.WriteLine();

                foreach (var (step, depth, pathKey) in EnumerateAllStepsWithPath(plan.Steps))
                {
                    DumpStepWithAnnotations(step, depth, pathKey, writer);
                    writer.WriteLine();
                }

                writer.WriteLine("=== Done ===");
                Console.WriteLine($"✅ Dump created: {logPath}");
            }
            catch (Exception ex)
            {
                writer.WriteLine("FATAL ERROR:");
                writer.WriteLine(ex.ToString());
                Console.WriteLine("❌ Failed to dump test plan. See log for details.");
            }
        }

        // ---------- Step traversal with a stable hierarchical path ----------
        private static IEnumerable<(ITestStep step, int depth, string pathKey)> EnumerateAllStepsWithPath(IList<ITestStep> roots)
        {
            var stack = new Stack<(ITestStep step, int depth, string parentKey, Dictionary<string, int> siblingCounts)>();
            var rootCounts = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int i = roots.Count - 1; i >= 0; i--)
                stack.Push((roots[i], 0, "Root", rootCounts));

            while (stack.Count > 0)
            {
                var (s, d, parentKey, counters) = stack.Pop();

                string typePart = s.GetType().FullName ?? s.GetType().Name;
                string namePart = s.Name ?? "(unnamed)";
                string baseId = $"{typePart}:{namePart}";

                counters.TryGetValue(baseId, out int n);
                counters[baseId] = ++n;

                string thisKey = $"{parentKey}/{baseId}/#{n}";
                yield return (s, d, thisKey);

                var childCounts = new Dictionary<string, int>(StringComparer.Ordinal);
                var children = s.ChildTestSteps;
                for (int i = children.Count - 1; i >= 0; i--)
                    stack.Push((children[i], d + 1, thisKey, childCounts));
            }
        }

        // ---------- Dump one step using Annotations (same path TUI takes) ----------
        private static void DumpStepWithAnnotations(ITestStep step, int depth, string pathKey, StreamWriter w)
        {
            string indent = new string(' ', depth * 2);
            var typeName = step.GetType().FullName ?? step.GetType().Name;

            w.WriteLine($"{indent}- Step: {step.Name}  [{typeName}]");
            w.WriteLine($"{indent}  Id: {pathKey}");

            try
            {
                var ann = AnnotationCollection.Annotate(step);

                // 🔽 Use the same filtering pipeline as TUI.PropertiesView.getMembers()
                var members = GetVisibleEditableMembersLikeTui(ann);

                foreach (var x in members)
                {
                    var member = x.Get<IMemberAnnotation>()!.Member;
                    var label = x.Get<DisplayAttribute>()?.Name ?? member.Name;

                    object? value =
                        (object?)x.Get<IStringReadOnlyValueAnnotation>()?.Value ??
                        (object?)x.Get<IStringValueAnnotation>()?.Value ??
                        x.Get<IObjectValueAnnotation>()?.Value;

                    w.WriteLine($"{indent}  {label} = {Pretty(value)}");
                }

                // (Optional) If you want to also show the step help (like TUI description pane), print it once:
                // var help = step.GetType().GetCustomAttribute<DisplayAttribute>()?.Description;
                // if (!string.IsNullOrWhiteSpace(help))
                //     w.WriteLine($"{indent}  [Help] {help}");
            }
            catch (Exception ex)
            {
                w.WriteLine($"{indent}  <annotation enumeration failed: {ex.GetType().Name}>");
            }
        }

        /// <summary>
        /// Mirrors OpenTAP TUI's PropertiesView.getMembers() selection:
        /// - Visible (IAccessAnnotation.IsVisible)
        /// - Keep Resource.Name
        /// - Exclude Submit members that render as buttons
        /// - Apply FilterMemberLikeTui (Browsable, XmlIgnore, Writable, Outputs policy)
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

        // ---------- Visibility rules: mirrors TUI PropertiesView.FilterMember ----------
        private static bool FilterMemberLikeTui(IMemberData member)
        {
            // Keep Resource.Name visible
            var resourceTypeData = TypeData.FromType(typeof(IResource));
            if (member.DeclaringType.DescendsTo(resourceTypeData) && member.Name == nameof(Resource.Name))
                return true;
            if (member.GetAttribute<BrowsableAttribute>() is BrowsableAttribute attr)
                return attr.Browsable;
            if (member.HasAttribute<OutputAttribute>())
                return true;
            return member.Attributes.Any(a => a is XmlIgnoreAttribute) == false && member.Writable;
        }

        // ---------- Friendly value printing (lightweight; annotations already made it human-friendly) ----------
        private static string Pretty(object? v)
        {
            if (v is null) return "null";
            if (v is string s) return $"\"{s}\"";

            var t = v.GetType();
            if (t.IsEnum || t.IsPrimitive) return v.ToString()!;
            if (t == typeof(decimal) || t == typeof(double) || t == typeof(float)) return Convert.ToString(v)!;

            // If object has a public Name, print Type(Name)
            var nameProp = t.GetProperty("Name", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (nameProp?.CanRead == true)
            {
                try { return $"{t.Name}({nameProp.GetValue(v) ?? "null"})"; }
                catch { /* ignore */ }
            }

            return t.Name;
        }
    }
}
