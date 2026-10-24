using System;
using System.Windows.Input;

namespace CDPIUI.ViewModels;

public class RelayCommand : ICommand
{
    private readonly Action<object> _execute;
    private readonly Func<object, bool> _canExecute;

    public event EventHandler CanExecuteChanged;

    public RelayCommand(Action<object> executeAction, Func<object, bool> canExecuteFunc = null)
    {
        _execute = executeAction;
        _canExecute = canExecuteFunc;
    }

    public RelayCommand(Action executeAction, Func<bool> canExecuteFunc = null)
        : this(_ => executeAction(), canExecuteFunc == null ? null : _ => canExecuteFunc())
    {
    }

    public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);

    public void Execute(object parameter) => _execute(parameter);

    /// <summary>Call on the UI thread when the result of the can-execute check changes.</summary>
    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
