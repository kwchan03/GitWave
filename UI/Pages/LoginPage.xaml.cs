using GitWave.ViewModels;
using System.Windows.Controls;

namespace GitWave.UI.Pages
{
    public partial class LoginPage : Page
    {
        public LoginPage(LoginViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
