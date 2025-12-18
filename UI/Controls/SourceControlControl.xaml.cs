using System.Windows.Controls;

namespace GitWave.UI.Pages
{
    /// <summary>
    /// Interaction logic for GitWaveControl.xaml
    /// </summary>
    public partial class SourceControlControl : System.Windows.Controls.UserControl
    {
        public SourceControlControl()
        {
            InitializeComponent();
        }

        // Navigate to a page in the main frame
        public void NavigateTo(Page page)
        {
            MainFrame.Navigate(page);
        }
    }
}
