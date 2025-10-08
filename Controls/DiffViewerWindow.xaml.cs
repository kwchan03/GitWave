using GitGUI.ViewModels;
using System.Windows;

namespace GitGUI.Controls
{
    /// <summary>
    /// Interaction logic for DiffViewerWindow.xaml
    /// </summary>
    public partial class DiffViewerWindow : Window
    {
        public DiffViewerWindow(DiffViewerTreeViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
