using System.Windows.Controls;

namespace GitGUI.Pages
{
    /// <summary>
    /// Interaction logic for PullRequestReview.xaml
    /// </summary>
    public partial class PullRequestReview : Page
    {
        public PullRequestReview()
        {
            InitializeComponent();
        }

        // Add the missing event handler for FileListBox_SelectionChanged
        private void FileListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Handle the selection change logic here
            // Example: Display the selected file's diff
            if (FileListBox.SelectedItem != null)
            {
                var selectedFile = FileListBox.SelectedItem.ToString();
                // Logic to update OldFileLines and NewFileLines based on selectedFile
            }
        }
    }
}
