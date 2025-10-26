using System.Windows.Input;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _executeAsync;
    private readonly Func<object?, bool>? _canExecute;
    private bool _isRunning;

    public AsyncRelayCommand(Func<object?, Task> executeAsync, Func<object?, bool>? canExecute = null)
    {
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? p) =>
        !_isRunning && (_canExecute?.Invoke(p) ?? true);

    public async void Execute(object? p)
    {
        if (!CanExecute(p)) return;

        _isRunning = true;
        CommandManager.InvalidateRequerySuggested(); // disable while running

        try { await _executeAsync(p); }
        finally
        {
            _isRunning = false;
            CommandManager.InvalidateRequerySuggested(); // re-enable after done
        }
    }

    // This makes WPF automatically requery on input/focus changes,
    // and whenever you call CommandManager.InvalidateRequerySuggested()
    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }
}
