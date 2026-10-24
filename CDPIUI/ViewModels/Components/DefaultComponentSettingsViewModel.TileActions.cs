using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.Commands;
using CDPIUI.Controls.Dialogs.ComponentSettings;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.Features;
using CDPIUI.Core.System;
using CDPIUI.Shared.Models;
using static CDPIUI.Helper.UIHelper;

namespace CDPIUI.ViewModels.Components;

public sealed partial class DefaultComponentSettingsViewModel
{
    /// <summary>Runs the action of an element of a setting tile.</summary>
    public void HandleTileAction(ActionIds actionId, List<string> arguments, SettingTileContentDefinition contentDefinition)
    {
        switch (actionId)
        {
            case ActionIds.ViewButtonClicked:
                _ = _view.ShowAppliedArgsAsync(arguments[0], contentDefinition.PrettyViewParams);
                break;
            case ActionIds.OpenFolderClicked:
                ShellHelper.LookupFileInDirectory(contentDefinition.EditFilePath);
                break;
            case ActionIds.ChangeSiteListClicked:
                if (contentDefinition.IsFileHardLinked)
                {
                    // RevertFileLink catches and logs its own errors.
                    _ = RevertFileLink(contentDefinition.PackId, contentDefinition.EditFilePath, contentDefinition.HardLinkTargetFile);
                }
                else
                {
                    // ChangeSiteList catches and logs its own errors.
                    _ = ChangeSiteList(arguments[0], contentDefinition.PackId, contentDefinition.EditFilePath, contentDefinition.IsIPSet);
                }
                break;
            case ActionIds.EditButtonClicked:
                if (!SettingsManager.Instance.GetValue<bool>("FILEOPENACTIONS", "isDialogShown") || !SettingsManager.Instance.GetValueOrDefault<bool>("FILEOPENACTIONS", "doNotRemindAgain", defaultValue: true))
                {
                    _ = AskFileOpenApplication(contentDefinition.EditFilePath); // AskFileOpenApplication catches and logs its own errors.
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

                RebuildTilesWithoutAnimation();
                _ = RestartComponentIfRunning();
                break;
            case ActionIds.FullButtonElementClicked:
                _ = ButtonClick(contentDefinition); // ButtonClick catches and logs its own errors.
                break;
        }
    }

    private async Task EditCommaVariable(SettingTileContentDefinition contentDefinition)
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
        string newValue = await _view.EditCommaVariableAsync(variable.name, variable.value, availableValues);
        if (newValue == null || string.Equals(newValue, variable.value, StringComparison.Ordinal))
            return;

        componentHelper.GetConfigHelper().ChangeCommaVariableValue(
            contentDefinition.FileName,
            contentDefinition.PackId,
            contentDefinition.VariableName,
            newValue);

        RebuildTilesWithoutAnimation();
        if (await ComponentTasksManager.Instance.IsTaskRunning(ComponentId))
            _ = ComponentTasksManager.Instance.RestartTask(ComponentId);
    }

    private async Task RevertFileLink(string packId, string fileName, string linkName)
    {
        try
        {
            var result = await FileSystemLinksManager.RemoveLinkForItemId(packId, linkName, fileName);

            Debug.WriteLine(result.Success);

            if (!result.Success)
            {
                await _view.ShowErrorAsync(
                    string.Format(_localizer.GetLocalizedString("ReplaceSiteListException"),
                    Path.GetFileName(linkName),
                    Path.GetFileName(fileName),
                    result.Error.ErrorCode),
                    result.Error.FriendlyDescription);
            }
            else
            {
                RebuildTilesWithoutAnimation();
            }
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    private async Task ChangeSiteList(string file, string packId, string fileName, bool isIPSet)
    {
        try
        {
            SiteListReplacement replacement = await _view.ChooseSiteListAsync(file, packId, fileName, isIPSet);

            if (replacement != null)
            {
                OperationResultModel<EmptyResult> result;
                if (replacement.SelectionType == FileSelectionType.FromTheStore)
                {
                    result = await FileSystemLinksManager.CreateHardLinkForItemId(packId, replacement.NewFileName, fileName);
                }
                else
                {
                    result = await FileSystemLinksManager.CreateSymbolicLinkForItemId(packId, replacement.NewFileName, fileName);
                }

                if (!result.Success)
                {
                    await _view.ShowErrorAsync(
                        string.Format(_localizer.GetLocalizedString("ReplaceSiteListException"), Path.GetFileName(fileName), Path.GetFileName(replacement.NewFileName), result.Error.ErrorCode),
                        result.Error.FriendlyDescription);
                }
                else
                {
                    RebuildTilesWithoutAnimation();
                }
            }
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    private async Task AskFileOpenApplication(string file)
    {
        try
        {
            if (await _view.AskFileOpenApplicationAsync(file))
                SettingsManager.Instance.SetValue("FILEOPENACTIONS", "isDialogShown", true);
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    private async Task ButtonClick(SettingTileContentDefinition contentDefinition)
    {
        try
        {
            switch (contentDefinition.ClickId)
            {
                case "CFGCREATE":
                    await _view.CreateConfigInEditorAsync(ComponentId);
                    break;
                case "CFGEDIT":
                    ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(ComponentId);
                    var item = _selectedConfig;

                    if (componentHelper != null && item != null)
                        await _view.EditConfigAsync(componentHelper.GetConfigHelper().GetConfigItems().FirstOrDefault(x => x.packId == item.PackId && x.file_name == item.FileName));
                    break;
                case "CFGGOODCHECK":
                    await _view.OpenGoodCheckAsync(ComponentId);
                    break;
                case "HELPOFFLINE":
                    CommandsHandler.HandleCommand("cdpiui://Help/");
                    break;
                case "HELPOFFLINECONFIGCHOOISE":
                    CommandsHandler.HandleCommand(
                        "cdpiui://Help/Autoselection/BestConfigSelection/");
                    break;
                case "EDITCOMMAVAR":
                    await EditCommaVariable(contentDefinition);
                    break;
            }
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }
}
