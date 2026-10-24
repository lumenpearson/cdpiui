using System.Threading.Tasks;
using CDPIUI.Core.Proxy;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUI3Localizer;

namespace CDPIUI.Helper.Basic;

/// <summary>
/// Proxy components listen on the address set in the proxy setup. Anything but a loopback
/// address makes the proxy reachable from the network without authentication (issue #67).
/// </summary>
public static class ProxyAddressWarning
{
    public static async Task<bool> ConfirmAsync(XamlRoot xamlRoot, string ip)
    {
        if (ProxyHelper.IsLoopbackAddress(ip))
            return true;

        ILocalizer localizer = Localizer.Get();
        var dialog = new ContentDialog
        {
            Title = localizer.GetLocalizedString("ProxyNetworkAddressTitle"),
            Content = string.Format(localizer.GetLocalizedString("ProxyNetworkAddressWarning"), ip),
            PrimaryButtonText = localizer.GetLocalizedString("Continue"),
            CloseButtonText = localizer.GetLocalizedString("Cancel"),
            DefaultButton = ContentDialogButton.Close,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            XamlRoot = xamlRoot,
        };
        return await dialog.ShowQueuedAsync() == ContentDialogResult.Primary;
    }
}
