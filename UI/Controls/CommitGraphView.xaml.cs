using GitGUI.Core;
using GitWave.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GitWave.UI.Controls
{
    public partial class CommitGraphView : System.Windows.Controls.UserControl
    {
        public CommitGraphView()
        {
            InitializeComponent();
            this.DataContext = Bootstrapper.Services.GetRequiredService<CommitGraphViewModel>();
        }
    }
}
