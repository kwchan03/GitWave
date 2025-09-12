using GitGUI.ViewModels;
using System.Windows.Controls;

namespace GitGUI.Pages
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
