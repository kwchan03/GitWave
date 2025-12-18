using GitWave.Core;
using GitWave.ViewModels;
using System.Windows.Controls;

namespace GitWave.UI.Pages
{
    /// <summary>
    /// Interaction logic for PRListPage.xaml
    /// </summary>
    public partial class PullRequestPage : Page
    {
        public PullRequestPage(IGitService gitService)
        {
            InitializeComponent();
            DataContext = new PullRequestPageViewModel(gitService);
        }

        // Event handler for the SelectionChanged event of the ListBox
        private void ChangedFile_Selected(object sender, SelectionChangedEventArgs e)
        {
            // Add your logic here to handle the file selection change
            // Example: Update the diff viewer based on the selected file
        }
    }
}
