using System.Threading.Tasks;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;

namespace CDPIUI.ViewModels.ComponentViews;

/// <summary>
/// The dialogs and windows the tg-ws-proxy settings page opens for its view model.
/// </summary>
public interface ITgWsProxyComponentView
{
    /// <summary>
    /// Shows the data center dialog, filled with <paramref name="current"/> when it is not
    /// null; returns null when the user cancels it.
    /// </summary>
    Task<TgWsDataCenterEdit> EditDataCenterAsync(string title, TgWsDataCenterEdit current);

    Task ShowErrorAsync(string message, string title);

    Task<ConfigCreationChoice> AskConfigCreationAsync();

    Task CreateConfigInEditorAsync(string componentId);

    Task EditConfigAsync(ConfigItem item);

    Task ShowConnectTelegramProxyAsync();
}
