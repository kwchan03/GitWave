using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GitWave.ViewModels
{
    public class BaseViewModel : INotifyPropertyChanged
    {
        private bool _isBusy;

        /// <summary>
        /// Indicates if the view model is currently performing a long-running operation.
        /// Useful for showing loading spinners in the UI.
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy != value)
                {
                    _isBusy = value;
                    OnPropertyChanged();
                }
            }
        }

        // Event to notify the UI when a property changes
        public event PropertyChangedEventHandler? PropertyChanged;

        // Helper method to raise PropertyChanged event
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Optional helper for setting properties and raising change notification
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
