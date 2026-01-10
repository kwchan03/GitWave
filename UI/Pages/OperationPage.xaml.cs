using System.Windows.Controls;

namespace GitWave.UI.Pages
{
    /// <summary>  
    /// Interaction logic for Operation.xaml  
    /// </summary>  
    public partial class OperationPage : UserControl
    {
        public OperationPage()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error: {ex}");
                throw;
            }
        }
    }
}
