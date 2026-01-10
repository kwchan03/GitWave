using GitGUI.Core;
using GitWave.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace GitWave.UI.Controls
{
    public partial class SourceControlFrame : UserControl
    {
        public SourceControlFrame()
        {
            InitializeComponent();
            this.DataContext = Bootstrapper.Services.GetRequiredService<SourceControlHostViewModel>();
        }
    }
}