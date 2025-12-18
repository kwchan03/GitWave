using GitWave.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GitWave.UI.Controls
{
    /// <summary>
    /// Interaction logic for PullRequestDetailView.xaml
    /// </summary>
    public partial class PullRequestDetailView : System.Windows.Controls.UserControl
    {
        public PullRequestDetailView()
        {
            InitializeComponent();
        }

        private void OnChangedFileClick(object sender, MouseButtonEventArgs e)
        {
            // Get the FileChangeViewModel from the clicked element
            if (sender is FrameworkElement fe && fe.DataContext is FileChangeViewModel file)
            {
                // Find the parent TreeViewItem containing the CommitNodeViewModel
                var treeViewItem = FindParent<TreeViewItem>(fe);
                if (treeViewItem == null)
                    return;

                // The TreeViewItem's DataContext should be CommitNodeViewModel
                if (treeViewItem.DataContext is CommitNodeViewModel commitVM)
                {
                    if (commitVM.ViewFileDiffCommand != null && commitVM.ViewFileDiffCommand.CanExecute(file))
                    {
                        commitVM.ViewFileDiffCommand.Execute(file);
                        e.Handled = true;
                    }
                }
            }
        }

        /// <summary>
        /// Helper method to find parent of specific type in visual tree
        /// </summary>
        private T FindParent<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T parent)
                    return parent;

                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
