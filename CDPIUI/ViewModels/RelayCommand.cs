using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CDPIUI.ViewModels;

public class RelayCommand : ICommand
{
    private Action<object> _execute;
    private Func<object, bool> _canExecute;
    // CanExecute is not cached by the callers; the command never raises this event.
    public event EventHandler CanExecuteChanged { add { } remove { } }
    public RelayCommand(Action<object> executeAction, Func<object, bool> canExecuteFunc = null)
    {
        _execute = executeAction;
        _canExecute = canExecuteFunc;
    }
    public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);
    public void Execute(object parameter) => _execute(parameter);
}
