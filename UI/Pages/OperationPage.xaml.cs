using GitWave.ViewModels;
using System.Windows.Controls;

namespace GitWave.UI.Pages
{
    /// <summary>  
    /// Interaction logic for Operation.xaml  
    /// </summary>  
    public partial class OperationPage : Page
    {
        public OperationPage(OperationViewModel vm)
        {
            try
            {
                InitializeComponent();
                DataContext = vm;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error: {ex}");
                throw;
            }
        }
    }
}
