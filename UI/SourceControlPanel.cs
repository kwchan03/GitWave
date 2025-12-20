using GitGUI.Core;
using GitWave.UI.Pages;
using Keysight.OpenTap.Wpf;
using Microsoft.Extensions.DependencyInjection;
using OpenTap;
using System.Windows;

namespace GitWave.UI
{
    [Display("Source Control", Group: "GitWave")]
    public class SourceControlPanel : ITapDockPanel
    {
        public double? DesiredWidth => 500;

        public double? DesiredHeight => 450;

        public FrameworkElement CreateElement(ITapDockContext context)
        {
            Bootstrapper.Initialize(); // Ensure services exist

            return Bootstrapper.Services.GetRequiredService<OperationPage>();
        }
    }
}
