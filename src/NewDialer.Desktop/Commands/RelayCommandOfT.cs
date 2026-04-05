using System.Windows.Input;

namespace NewDialer.Desktop.Commands;

public sealed class RelayCommand<T>(Action<T?> execute, Func<T?, bool>? canExecute = null) : ICommand
{
    private readonly Action<T?> _execute = execute;
    private readonly Func<T?, bool>? _canExecute = canExecute;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        if (_canExecute is null)
        {
            return true;
        }

        return parameter is T value
            ? _canExecute(value)
            : _canExecute(default);
    }

    public void Execute(object? parameter)
    {
        if (parameter is T value)
        {
            _execute(value);
            return;
        }

        _execute(default);
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
