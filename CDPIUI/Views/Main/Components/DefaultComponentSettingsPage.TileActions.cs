using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.Controls.ComponentSettings;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Dialogs;
using CDPIUI.Controls.Dialogs.ComponentSettings;
using CDPIUI.Controls.Dialogs.Universal;
using CDPIUI.Core;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.Features;
using CDPIUI.Core.Proxy;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.System;
using CDPIUI.Helper;
using CDPIUI.Shared.Extensions;
using CDPIUI.Shared.Models;
using CDPIUI.Views.CreateConfigUtil;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Navigation;
using WinUI3Localizer;
using static CDPIUI.Helper.UIHelper;

namespace CDPIUI.Views.Main.Components;

public sealed partial class DefaultComponentSettingsPage
{
    private void HandleSettingTileElementClick(ActionIds actionId, List<string> arguments, SettingTileContentDefinition contentDefinition)
    {
        switch (actionId)
        {
            case ActionIds.ViewButtonClicked:
                Controls.Dialogs.ViewApplyArgsContentDialog dialog = new()
                {
                    Title = _localizer.GetLocalizedString("AppliedFlagsDialogTitle"),
                    DialogTitle = arguments[0],
                    Args = contentDefinition.PrettyViewParams,
                    XamlRoot = this.XamlRoot,
                };
                _ = dialog.ShowQueuedAsync();
                break;
            case ActionIds.OpenFolderClicked:
                ShellHelper.LookupFileInDirectory(contentDefinition.EditFilePath);
                break;
            case ActionIds.ChangeSiteListClicked:
                if (contentDefinition.IsFileHardLinked)
                {
                    // RevertFileLink catches and logs its own errors.
                    _ = RevertFileLink(arguments[0], contentDefinition.PackId, contentDefinition.EditFilePath, contentDefinition.HardLinkTargetFile);
                }
                else
                {
                    // ShowChangeListDialog catches and logs its own errors.
                    _ = ShowChangeListDialog(arguments[0], contentDefinition.PackId, contentDefinition.EditFilePath, contentDefinition.IsIPSet);
                }
                break;
            case ActionIds.EditButtonClicked:
                if (!SettingsManager.Instance.GetValue<bool>("FILEOPENACTIONS", "isDialogShown") || !SettingsManager.Instance.GetValueOrDefault<bool>("FILEOPENACTIONS", "doNotRemindAgain", defaultValue: true))
                {
                    _ = ShowEditAskDialog(contentDefinition.EditFilePath); // ShowEditAskDialog catches and logs its own errors.
                }
                else
                {
                    ShellHelper.OpenFile(contentDefinition.EditFilePath);
                }
                break;
            case ActionIds.SwitchToggled:
                ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(ComponentId);
                bool.TryParse(arguments[0], out bool result);
                componentHelper.GetConfigHelper().ChangeVariableValue(contentDefinition.FileName, contentDefinition.PackId, contentDefinition.VariableName, result);

                _showAnim = false;
                var item = ConfigChooseCombobox.SelectedItem as ConfigSelectorItem;
                Task.Run(() => InitSettingsTiles(item));
                _ = RestartComponentIfRunning();
                break;
            case ActionIds.FullButtonElementClicked:
                _ = ButtonClick(contentDefinition); // ButtonClick catches and logs its own errors.
                break;

        }
    }

    private async Task ShowEditCommaVariableDialog(SettingTileContentDefinition contentDefinition)
    {
        ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(ComponentId);
        CommaVariableItem variable = componentHelper.GetConfigHelper()
            .GetCommaVariables(contentDefinition.FileName, contentDefinition.PackId)
            .FirstOrDefault(item => string.Equals(
                item.name,
                contentDefinition.VariableName,
                StringComparison.OrdinalIgnoreCase));
        if (variable == null)
            return;

        var availableValues = new AvailableVarValues
        {
            Comment = variable.comment ?? string.Empty,
            VarName = variable.name,
            CurrentValueIndex = variable.values.FindIndex(value =>
                string.Equals(value, variable.value, StringComparison.Ordinal)),
            Values = variable.values,
        };
        var dialog = new EditConfigVarValueDialog(variable.name, variable.value, availableValues)
        {
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowQueuedAsync() != ContentDialogResult.Primary ||
            string.Equals(dialog.VarValue, variable.value, StringComparison.Ordinal))
        {
            return;
        }

        componentHelper.GetConfigHelper().ChangeCommaVariableValue(
            contentDefinition.FileName,
            contentDefinition.PackId,
            contentDefinition.VariableName,
            dialog.VarValue);

        _showAnim = false;
        var selectedConfig = ConfigChooseCombobox.SelectedItem as ConfigSelectorItem;
        _ = Task.Run(() => InitSettingsTiles(selectedConfig));
        if (await ComponentTasksManager.Instance.IsTaskRunning(ComponentId))
            _ = ComponentTasksManager.Instance.RestartTask(ComponentId);
    }

    private async Task RevertFileLink(string file, string packId, string fileName, string linkName)
    {
        try
        {
            var result = await FileSystemLinksManager.RemoveLinkForItemId(packId, linkName, fileName);

            Debug.WriteLine(result.Success);

            if (!result.Success)
            {
                ErrorContentDialog dialog = new();
                await dialog.ShowErrorDialogAsync(
                    string.Format(_localizer.GetLocalizedString("ReplaceSiteListException"),
                    Path.GetFileName(linkName),
                    Path.GetFileName(fileName),
                    result.Error.ErrorCode),
                    result.Error.FriendlyDescription,
                    this.XamlRoot
                    );
            }
            else
            {

                _showAnim = false;
                var item = ConfigChooseCombobox.SelectedItem as ConfigSelectorItem;
                _ = Task.Run(() => InitSettingsTiles(item));
            }
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(DefaultComponentSettingsPage), exception.ToString());
        }
    }

    private async Task ShowChangeListDialog(string file, string packId, string fileName, bool isIPSet)
    {
        try
        {
            ChangeSiteListContentDialog changeListContentDialog = new()
            {
                XamlRoot = this.XamlRoot,
                ListTitle = file,
                PackId = packId,
                FileName = fileName,
                IsIpSet = isIPSet,
            };
            await changeListContentDialog.ShowQueuedAsync();

            if (changeListContentDialog.IsDialogFinishedSuccessfully)
            {
                OperationResultModel<EmptyResult> result;
                if (changeListContentDialog.SelectionType == FileSelectionType.FromTheStore)
                {
                    result = await FileSystemLinksManager.CreateHardLinkForItemId(packId, changeListContentDialog.NewFileName, fileName);
                }
                else
                {
                    result = await FileSystemLinksManager.CreateSymbolicLinkForItemId(packId, changeListContentDialog.NewFileName, fileName);
                }

                if (!result.Success)
                {
                    ErrorContentDialog dialog = new();
                    await dialog.ShowErrorDialogAsync(
                        string.Format(_localizer.GetLocalizedString("ReplaceSiteListException"), Path.GetFileName(fileName), Path.GetFileName(changeListContentDialog.NewFileName), result.Error.ErrorCode),
                        result.Error.FriendlyDescription,
                        this.XamlRoot
                        );
                }
                else
                {
                    _showAnim = false;
                    var item = ConfigChooseCombobox.SelectedItem as ConfigSelectorItem;
                    _ = Task.Run(() => InitSettingsTiles(item));
                }


            }
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(DefaultComponentSettingsPage), exception.ToString());
        }
    }
    private async Task ShowEditAskDialog(string file)
    {
        try
        {
            EditSitelistAskApplicationContentDialog editSitelistAskApplicationContentDialog = new()
            {
                XamlRoot = this.XamlRoot,
                FilePath = file
            };
            await editSitelistAskApplicationContentDialog.ShowQueuedAsync();
            if (editSitelistAskApplicationContentDialog.IsSuccess)
                SettingsManager.Instance.SetValue("FILEOPENACTIONS", "isDialogShown", true);
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(DefaultComponentSettingsPage), exception.ToString());
        }
    }

    private async Task ButtonClick(SettingTileContentDefinition contentDefinition)
    {
        try
        {
            switch (contentDefinition.ClickId)
            {
                case "CFGCREATE":
                    await ConfigMakerWindow.CreateForComponentAsync(ComponentId);
                    break;
                case "CFGEDIT":
                    ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(ComponentId);
                    var item = (ConfigSelectorItem)ConfigChooseCombobox.SelectedItem;

                    if (componentHelper != null && item != null)
                        await ConfigMakerWindow.EditConfig(componentHelper.GetConfigHelper().GetConfigItems().FirstOrDefault(x => x.packId == item.PackId && x.file_name == item.FileName));
                    break;
                case "CFGGOODCHECK":
                    CreateConfigUtilWindow gwindow = await ((App)Application.Current).SafeCreateNewWindow<CreateConfigUtilWindow>();
                    gwindow.NavigateToPage<CreateViaGoodCheck>(new NameValueCollection() { { "componentId", ComponentId } });
                    break;
                case "HELPOFFLINE":
                    Commands.CommandsHandler.HandleCommand("cdpiui://Help/");
                    break;
                case "HELPOFFLINECONFIGCHOOISE":
                    Commands.CommandsHandler.HandleCommand(
                        "cdpiui://Help/Autoselection/BestConfigSelection/");
                    break;
                case "EDITCOMMAVAR":
                    await ShowEditCommaVariableDialog(contentDefinition);
                    break;
            }
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(DefaultComponentSettingsPage), exception.ToString());
        }
    }
}
