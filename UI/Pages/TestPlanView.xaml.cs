using OpenTap;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Xml.Serialization;

namespace GitWave.UI.Pages
{
    public partial class TestPlanView : Window
    {
        // Lightweight row for the left list
        private sealed class StepRow
        {
            public string Label { get; }
            public ITestStep Step { get; }
            public StepRow(ITestStep step)
            {
                Step = step;
                var td = TypeData.GetTypeData(step);
                var prettyType = td.GetDisplayAttribute()?.Name ?? step.GetType().Name;
                // e.g. "My Step Name (MyStepType)"
                Label = string.IsNullOrWhiteSpace(step.Name) ? prettyType : $"{step.Name} ({prettyType})";
            }
        }

        private readonly ObservableCollection<StepRow> stepRows = new();
        private AnnotationCollection annotations; // object-level annotations for selected step

        public TestPlanView()
        {
            InitializeComponent();
            DataContext = this;

            // Important: ensure this was called in App.OnStartup before showing any PropGrid:
            // Keysight.Ccl.Wsl.UI.UXManager.Initialize("System");
            // Keysight.Ccl.Wsl.UI.UXManager.ColorScheme = "CaranuLight";

            // Load your plan (replace path)
            var planPath = @"C:\Users\chank\Documents\UM\Y3S2\WIA3002 ACADEMIC PROJECT I\TestGit\TestPlan.TapPlan";
            if (!File.Exists(planPath))
            {
                System.Windows.MessageBox.Show($"Plan not found:\n{planPath}");
                return;
            }

            var plan = TestPlan.Load(planPath);

            // Fill the left pane with all steps (preorder)
            foreach (var s in Flatten(plan.Steps))
                stepRows.Add(new StepRow(s));

            StepList.ItemsSource = stepRows;

            // Optional: select first step
            if (stepRows.Count > 0)
                StepList.SelectedIndex = 0;
        }

        // When a step is picked, annotate it, filter members via getMembers(), and show in PropGrid
        private void StepList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (StepList.SelectedItem is not StepRow row)
            {
                Grid.DataContext = null;
                return;
            }

            // annotate the selected step
            annotations = AnnotationCollection.Annotate(row.Step);

            // get filtered member annotation collections (your function below)
            var members = getMembers();

            // Show all member editors at once by passing the list of member AnnotationCollections
            // (PropGrid can render IEnumerable<AnnotationCollection>)
            Grid.DataContext = row.Step;

            // If you prefer to show the entire object-level annotations instead, use:
            // Grid.Content = annotations;

            Grid.Reload(); // optional
        }

        // Preorder flatten
        private static IEnumerable<ITestStep> Flatten(TestStepList roots)
        {
            var stack = new Stack<ITestStep>(Enumerable.Reverse(roots));
            while (stack.Count > 0)
            {
                var s = stack.Pop();
                yield return s;
                for (int i = s.ChildTestSteps.Count - 1; i >= 0; i--)
                    stack.Push(s.ChildTestSteps[i]);
            }
        }

        // ---------------- your filtering logic (verbatim) ----------------
        static ITypeData resourceTypeData = TypeData.FromType(typeof(IResource));

        public static bool FilterMember(IMemberData member)
        {
            if (member.DeclaringType.DescendsTo(resourceTypeData) && member.Name == nameof(Resource.Name))
                return true;
            if (member.GetAttribute<BrowsableAttribute>() is BrowsableAttribute attr)
                return attr.Browsable;
            if (member.HasAttribute<OutputAttribute>())
                return true;
            return member.Attributes.Any(a => a is XmlIgnoreAttribute) == false && member.Writable;
        }

        // Return AnnotationCollection[] for the selected step's members
        private AnnotationCollection[] getMembers()
        {
            return annotations?.Get<IMembersAnnotation>()?.Members
                .Where(x =>
                {
                    var member = x.Get<IMemberAnnotation>().Member;
                    if (member.DeclaringType.DescendsTo(resourceTypeData) && member.Name == nameof(Resource.Name))
                        return true;
                    return x.Get<IAccessAnnotation>()?.IsVisible ?? false;
                })
                .Where(x =>
                {
                    var member = x.Get<IMemberAnnotation>()?.Member;
                    if (member == null ||
                        (member.GetAttribute<SubmitAttribute>() != null && x.Get<IAvailableValuesAnnotationProxy>() != null))
                        return false;
                    return FilterMember(member);
                })
                .ToArray();
        }
    }
}
