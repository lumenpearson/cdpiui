using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CDPIUI.Core.Basic;

namespace CDPIUI.ViewModels;

/// <summary>
/// A command that runs an asynchronous action. The command cannot run again until the
/// action completes. An exception from the action is written to the log, because nothing
/// awaits the command.
/// </summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<object, Task> _execute;
    private readonly Func<object, bool> _canExecute;
    private readonly string _logSource;
    private bool _isRunning;

    public event EventHandler CanExecuteChanged;

    public AsyncRelayCommand(Func<object, Task> executeAction, Func<object, bool> canExecuteFunc = null, string logSource = nameof(AsyncRelayCommand))
    {
        _execute = executeAction;
        _canExecute = canExecuteFunc;
        _logSource = logSource;
    }

    public AsyncRelayCommand(Func<Task> executeAction, Func<bool> canExecuteFunc = null, string logSource = nameof(AsyncRelayCommand))
        : this(_ => executeAction(), canExecuteFunc == null ? null : _ => canExecuteFunc(), logSource)
    {
    }

    public bool IsRunning => _isRunning;

    public bool CanExecute(object parameter) => !_isRunning && (_canExecute == null || _canExecute(parameter));

    public async void Execute(object parameter)
    {
        // async void: ICommand.Execute is void by contract; the exception is logged below.
        await ExecuteAsync(parameter);
    }

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
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(_logSource, $"Command failed: {ex}");
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
