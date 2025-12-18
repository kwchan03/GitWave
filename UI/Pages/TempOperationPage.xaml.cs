using GitWave.Models;
using System.Windows;
using System.Windows.Controls;

namespace GitWave.UI.Pages
{
    /// <summary>
    /// Interaction logic for TempOperationPage.xaml
    /// </summary>
    public partial class TempOperationPage : Page
    {
        public TempOperationPage()
        {
            InitializeComponent();
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (sender is System.Windows.Controls.TreeView treeView)
            {
                // Get the selected item in the TreeView (which will be a ChangeItem)
                var selectedChange = treeView.SelectedItem as ChangeItem;

                if (selectedChange != null)
                {
                    // You can now use the selectedChange for any logic or pass it to your ViewModel
                    // Example: Update the selected item in the ViewModel
                    //(this.DataContext as OperationViewModel).SelectedChange = selectedChange;
                }
            }
        }
    }
}
