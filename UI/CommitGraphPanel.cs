using GitGUI.Core;
using GitWave.Services;
using GitWave.UI.Controls;
using Keysight.OpenTap.Wpf;
using Microsoft.Extensions.DependencyInjection;
using OpenTap;
using System.Windows;

namespace GitWave.UI
{
    [Display("Commit Graph", Group: "GitWave")]
    public class CommitGraphPanel : ITapDockPanel
    {
        public double? DesiredWidth => 500;
        public double? DesiredHeight => 450;
        public FrameworkElement CreateElement(ITapDockContext context)
        {
            Bootstrapper.Initialize();
            var contextService = Bootstrapper.Services.GetRequiredService<TapDockContextService>();
            contextService.Initialize(context);
            return new CommitGraphView();
        }
    }
}
