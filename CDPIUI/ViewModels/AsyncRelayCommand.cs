using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CDPIUI.ViewModels;

/// <summary>
/// A command that runs an asynchronous action. The command cannot run again until the
/// action completes. An exception from the action goes to the application handler for
/// unhandled exceptions, which logs it and shows it to the user, as it does for an
/// async event handler.
/// </summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<object, Task> _execute;
    private readonly Func<object, bool> _canExecute;
    private bool _isRunning;

    public event EventHandler CanExecuteChanged;

    public AsyncRelayCommand(Func<object, Task> executeAction, Func<object, bool> canExecuteFunc = null)
    {
        _execute = executeAction;
        _canExecute = canExecuteFunc;
    }

    public AsyncRelayCommand(Func<Task> executeAction, Func<bool> canExecuteFunc = null)
        : this(_ => executeAction(), canExecuteFunc == null ? null : _ => canExecuteFunc())
    {
    }

    public bool IsRunning => _isRunning;

    public bool CanExecute(object parameter) => !_isRunning && (_canExecute == null || _canExecute(parameter));

    // async void: ICommand.Execute is void by contract. An exception reaches
    // Application.UnhandledException on the UI thread, like one from a Click handler.
    public async void Execute(object parameter) => await ExecuteAsync(parameter);

    /// <summary>Runs the action; for callers that must wait for it.</summary>
    public async Task ExecuteAsync(object parameter)
    {
        if (_isRunning)
            return;

        _isRunning = true;
        NotifyCanExecuteChanged();
        try
        {
            await _execute(parameter);
        }
        finally
        {
            _isRunning = false;
            NotifyCanExecuteChanged();
        }
    }

    /// <summary>Call on the UI thread when the result of the can-execute check changes.</summary>
    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
