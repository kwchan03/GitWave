using GitWave.ViewModels;
using System.Windows;

namespace GitWave.Controls
{
    /// <summary>
    /// Interaction logic for DiffViewerWindow.xaml
    /// </summary>
    public partial class DiffViewerWindow : Window, IDisposable
    {
        private DiffViewerTreeViewModel? _viewModel;
        private bool _disposed = false;

        public DiffViewerWindow(DiffViewerTreeViewModel vm)
        {
            InitializeComponent();
            _viewModel = vm;
            DataContext = vm;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return; // Already disposed, prevent double-disposal

            if (disposing)
            {
                // Dispose managed resources
                // Clear references to prevent memory leaks
                _viewModel = null;
                DataContext = null;

                // Close the window if it's still open
                try
                {
                    if (this.IsLoaded)
                    {
                        this.Close();
                    }
                }
                catch
                {
                    // Window might already be closed
                }
            }

            _disposed = true;
        }
    }
}
