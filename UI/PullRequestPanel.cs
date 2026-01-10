using GitGUI.Core;
using GitWave.Services;
using GitWave.UI.Controls;
using Keysight.OpenTap.Wpf;
using Microsoft.Extensions.DependencyInjection;
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
            Bootstrapper.Initialize();
            var contextService = Bootstrapper.Services.GetRequiredService<TapDockContextService>();
            contextService.Initialize(context);
            return new PullRequestFrame();
        }
    }
}
