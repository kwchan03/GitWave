using GitGUI.Core;
using GitWave.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace GitWave.UI.Controls
{
    public partial class PullRequestFrame : UserControl
    {
        public PullRequestFrame()
        {
            InitializeComponent();
            this.DataContext = Bootstrapper.Services.GetRequiredService<PullRequestHostViewModel>();
        }
    }
}