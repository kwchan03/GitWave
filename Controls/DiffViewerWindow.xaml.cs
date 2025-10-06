using GitGUI.Models;
using System.Windows;

namespace GitGUI.Controls
{
    /// <summary>
    /// Interaction logic for DiffViewerWindow.xaml
    /// </summary>
    public partial class DiffViewerWindow : Window
    {
        public DiffViewerWindow(DiffViewerViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
