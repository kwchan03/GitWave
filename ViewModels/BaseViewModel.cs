using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GitGUI.ViewModels
{
    public class BaseViewModel : INotifyPropertyChanged
    {
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
