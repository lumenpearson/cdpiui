using System.Collections.Generic;
using System.Threading.Tasks;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using static CDPIUI.Helper.UIHelper;

namespace CDPIUI.ViewModels.Components;

/// <summary>
/// The setting tiles, dialogs and windows the component settings page shows for its view model.
/// </summary>
public interface IDefaultComponentSettingsView
{
    void ClearTiles();

    /// <summary>Creates the tile control of the model and adds it to the page.</summary>
    void AddTile(SettingsTile tile);

    Task ShowAppliedArgsAsync(string dialogTitle, List<string> args);

    Task ShowErrorAsync(string message, string details);

    /// <summary>Lets the user choose another site list; null when the dialog was not finished.</summary>
    Task<SiteListReplacement> ChooseSiteListAsync(string listTitle, string packId, string fileName, bool isIpSet);

    /// <summary>Asks which application opens the file; true when the user made a choice.</summary>
    Task<bool> AskFileOpenApplicationAsync(string filePath);

    /// <summary>Lets the user edit a list variable; null when the value is kept.</summary>
    Task<string> EditCommaVariableAsync(string name, string value, AvailableVarValues availableValues);

    Task CreateConfigInEditorAsync(string componentId);

    Task EditConfigAsync(ConfigItem item);

    Task OpenGoodCheckAsync(string componentId);
}
