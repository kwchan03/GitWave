using GitWave.UI.Pages;
using Keysight.OpenTap.Wpf;
using OpenTap;
using System.Windows;

namespace GitWave.UI
{
    [Display("Pull Request", Group: "GitWave")]
    public class PullRequestPanel : ITapDockPanel
    {
        public double? DesiredWidth => 500;

        public double? DesiredHeight => 450;

        public FrameworkElement CreateElement(ITapDockContext context)
        {
            return new SourceControlControl();
        }
    }
}
