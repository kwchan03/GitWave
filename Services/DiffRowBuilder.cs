//using GitGUI.Models;
//using System.Text;

//namespace GitGUI.Services
//{
//    public static class DiffRowBuilder
//    {
//        public static IEnumerable<DiffRow> FromTestStepDiffs(IEnumerable<TestStepDiff> diffs)
//        {
//            foreach (var d in diffs)
//            {
//                // Decide colors + text based on change type
//                if (d.Before == null && d.After != null)
//                {
//                    // Added
//                    yield return new DiffRow
//                    {
//                        Label = d.Path,
//                        Before = "(not present)",
//                        After = RenderStepSummary(d.After, d.ChangedProperties),
//                        BeforeBrush = System.Windows.Media.Brushes.LightCoral, // red-ish (left side)
//                        AfterBrush = System.Windows.Media.Brushes.LightGreen  // green (right side)
//                    };
//                }
//                else if (d.Before != null && d.After == null)
//                {
//                    // Deleted
//                    yield return new DiffRow
//                    {
//                        Label = d.Path,
//                        Before = RenderStepSummary(d.Before, d.ChangedProperties),
//                        After = "(not present)",
//                        BeforeBrush = System.Windows.Media.Brushes.LightCoral,
//                        AfterBrush = System.Windows.Media.Brushes.LightGreen
//                    };
//                }
//                else
//                {
//                    // Modified
//                    yield return new DiffRow
//                    {
//                        Label = d.Path,
//                        Before = RenderStepSummary(d.Before!, d.ChangedProperties),
//                        After = RenderStepSummary(d.After!, d.ChangedProperties),
//                        BeforeBrush = System.Windows.Media.Brushes.LightYellow,
//                        AfterBrush = System.Windows.Media.Brushes.LightYellow
//                    };
//                }
//            }
//        }

//        // Keep it simple: show StepName [Type] and the property names that changed.
//        private static string RenderStepSummary(OpenTap.ITestStep step, IList<string> changedProps)
//        {
//            var sb = new StringBuilder();
//            sb.Append($"{step.Name} [{step.GetType().Name}]");
//            if (changedProps != null && changedProps.Count > 0)
//                sb.AppendLine().Append("Changed: ").Append(string.Join(", ", changedProps));
//            return sb.ToString();
//        }
//    }
//}
