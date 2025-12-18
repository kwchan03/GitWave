using GitWave.ViewModels;
using System.Windows.Input;

namespace GitWave.Controls
{
    /// <summary>
    /// Interaction logic for CommitRowView.xaml
    /// </summary>
    public partial class CommitRowView : System.Windows.Controls.UserControl
    {
        private CommitRowViewModel? _viewModel;

        public CommitRowView()
        {
            InitializeComponent();
            this.DataContextChanged += (s, e) =>
            {
                _viewModel = this.DataContext as CommitRowViewModel;
            };
        }
        private void OnClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is CommitRowViewModel row)
                row.IsExpanded = !row.IsExpanded;
        }

        private void OnChangedFileClick(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel == null) return;
            if (sender is System.Windows.Controls.ListView listView)
            {
                var selectedFile = listView?.SelectedItems[0] as FileChangeViewModel;

                // Set the selected file and execute the view diff command
                _viewModel.SelectedChange = selectedFile;
                _viewModel.ViewFileCommand?.Execute(selectedFile);
                e.Handled = true;
            }
        }
    }
}
