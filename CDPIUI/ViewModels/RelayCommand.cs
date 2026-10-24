using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CDPIUI.ViewModels;

public class RelayCommand : ICommand
{
    private Action<object> execute;
    private Func<object, bool> canExecute;
    // CanExecute is not cached by the callers; the command never raises this event.
    public event EventHandler CanExecuteChanged { add { } remove { } }
    public RelayCommand(Action<object> executeAction, Func<object, bool> canExecuteFunc = null)
    {
        execute = executeAction;
        canExecute = canExecuteFunc;
    }
    public bool CanExecute(object parameter) => canExecute == null || canExecute(parameter);
    public void Execute(object parameter) => execute(parameter);
}
