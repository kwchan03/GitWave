using GitWave.ViewModels;
using System.Windows.Controls;

namespace GitWave.UI.Pages
{
    /// <summary>
    /// Interaction logic for RepositoryPage.xaml
    /// </summary>
    public partial class RepositoryPage : Page
    {
        public RepositoryPage(RepositoryViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
