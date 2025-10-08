// ViewModels/DiffViewerTreeViewModel.cs
using GitGUI.Models;
using OpenTap;
using System.Collections.ObjectModel;

namespace GitGUI.ViewModels
{
    public class DiffViewerTreeViewModel
    {
        public ObservableCollection<StepNode> BeforeRoots { get; } = new();
        public ObservableCollection<StepNode> AfterRoots { get; } = new();

        public void Load(TestPlan before, TestPlan after)
        {
            BeforeRoots.Clear(); AfterRoots.Clear();
            var (b, a) = GitGUI.Services.DiffTreeBuilder.BuildTrees(before, after);
            foreach (var n in b) BeforeRoots.Add(n);
            foreach (var n in a) AfterRoots.Add(n);
            System.Diagnostics.Debug.WriteLine($"BuildTrees: before={BeforeRoots.Count}, after={AfterRoots.Count}");
        }
    }
}
