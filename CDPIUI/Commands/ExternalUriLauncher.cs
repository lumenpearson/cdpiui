using System.Windows.Input;
using CDPIUI.Helper;
using CDPIUI.ViewModels;

namespace CDPIUI.Commands;

internal class ExternalUriLauncher
{
    #region UI link handle
    private static ExternalUriLauncher _instance;
    private static readonly object SyncRoot = new();
    public static ExternalUriLauncher Instance
    {
        get
        {
            lock (SyncRoot)
            {
                _instance ??= new ExternalUriLauncher();
                return _instance;
            }
        }
    }

    public ICommand Command { get; }

    private ExternalUriLauncher()
    {
        Command = new RelayCommand(ExecuteCommand);
    }

    private void ExecuteCommand(object parameter)
    {
        if (parameter is string uri)
        {
            _ = UrlOpenHelper.LaunchUrl(uri);
        }
    }
    #endregion
}
