using System.Windows;
using System.Windows.Controls;

namespace GitGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
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