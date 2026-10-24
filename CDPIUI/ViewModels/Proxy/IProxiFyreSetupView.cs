using System.Collections.Generic;
using System.Threading.Tasks;
using CDPIUI.Controls.Dialogs.ProxySetupUtil;

namespace CDPIUI.ViewModels.Proxy;

/// <summary>
/// The dialogs the ProxiFyre setup page shows for its view model.
/// </summary>
public interface IProxiFyreSetupView
{
    /// <summary>Asks to confirm a proxy address that is not a loopback address.</summary>
    Task<bool> ConfirmProxyAddressAsync(string ip);

    /// <summary>Lets the user edit an application list and returns the edited list.</summary>
    Task<List<SelectedApplicationInfo>> EditApplicationsAsync(List<SelectedApplicationInfo> applications, string title);
}
