using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.Controls.Dialogs.CreateConfigHelper;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.JSON;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Helper.LScript;
using CDPIUI.Shared;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;

namespace CDPIUI.Views.CreateConfigHelper;

// Opening the page for one of the navigation requests (CFGSTRING, CFGIMPORT, CFGEDIT,
// CFGCREATEBYID, CFGRETURNEDITED) and loading the config into the editor.
public sealed partial class CreateNewConfigPage
{
    private void RunOnNavigatedToActions(object args)
    {
        if (args is NameValueCollection collection)
        {
            string operationType = collection.Get("type");
            _configItem = JSONConvertor.DeserializeObject<ConfigItem>(collection.Get("configItem"));
            Debug.WriteLine(operationType);
            switch (operationType)
            {
                case CreateConfigNavigationParameters.FromStringType:
                    OpenFromStartupString(collection);
                    break;
                case CreateConfigNavigationParameters.ImportType:
                    OpenImportedConfig(collection);
                    break;
                case CreateConfigNavigationParameters.EditType:
                    OpenConfigForEdit();
                    break;
                case CreateConfigNavigationParameters.CreateByIdType:
                    SelectComponent(collection.Get("componentId"));
                    break;
                case CreateConfigNavigationParameters.ReturnEditedType:
                    OpenReturnedEditedConfig();
                    break;
            }
        }
        CheckViewType();
    }

    private void OpenFromStartupString(NameValueCollection collection)
    {
        string startupString = collection.Get("startupString");
        string componentId = collection.Get("componentId");

        SelectComponent(componentId);

        PageOpenMode = PageOpenModes.CreateConfigFromString;
        AddWrap(startupString);

        _configItem = new()
        {
            startup_string = startupString,
        };
        // AskAutoFillFiles catches and logs its own errors.
        _ = AskAutoFillFiles(
            _configItem,
            LScriptLangHelper.ExecuteScript(
                "$GETCURRENTDIR()",
            callItemId: SharedConstants.LocalUserItemsId),
            AskAutoFillMode.Quiet);
    }

    private void OpenImportedConfig(NameValueCollection collection)
    {
        bool errorHappens = bool.Parse(collection.Get("errorHappens"));
        string filePath = collection.Get("filePath");

        PageTitleTextBlock.Text = _localizer.GetLocalizedString("ImportConfigFromFile");
        SelectTargetComponent();

        AddWrap();

        LoadVars(_configItem);

        _ = AskAutoFillFiles(_configItem, filePath); // AskAutoFillFiles catches and logs its own errors.
    }

    private void OpenConfigForEdit()
    {
        PageTitleTextBlock.Text = _localizer.GetLocalizedString("EditConfig");
        SelectTargetComponent();
        ComponentChooseComboBox.IsEnabled = false;

        PageOpenMode = PageOpenModes.EditConfig;
        if (_configItem.packId != SharedConstants.LocalUserItemsId)
        {
            DisplayNameTextBox.Text = $"{_configItem.name} ({_localizer.GetLocalizedString("Edited")})";
            SaveButtonText.Text = _localizer.GetLocalizedString("SaveAsACopy");
        }
        else
        {
            DisplayNameTextBox.Text = $"{_configItem.name}";
        }

        if (_configItem.packId != SharedConstants.LocalUserItemsId)
        {
            // AskAutoFillFiles catches and logs its own errors.
            _ = AskAutoFillFiles(
                _configItem,
                LScriptLangHelper.ExecuteScript("$GETCURRENTDIR()",
                callItemId: _configItem.packId), AskAutoFillMode.Quiet);
        }

        LoadVars(_configItem);
        LoadConditions(_configItem);

        AddWrap();
    }

    private void OpenReturnedEditedConfig()
    {
        PageTitleTextBlock.Text = _localizer.GetLocalizedString("EditConfig");
        SelectTargetComponent();
        ComponentChooseComboBox.IsEnabled = false;

        PageOpenMode = PageOpenModes.EditConfigNoSave;
        EditItemId = _configItem.packId;

        DisplayNameTextBox.Text = _configItem.name;
        SaveButtonText.Text = _localizer.GetLocalizedString("Save");

        LoadVars(_configItem);
        LoadConditions(_configItem);

        AddWrap();
    }

    private void SelectTargetComponent()
    {
        if (_configItem.target?.Count > 0)
        {
            SelectComponent(_configItem.target[0]);
        }
    }

    private void SelectComponent(string componentId)
    {
        ComponentChooseComboBox.SelectedItem = ComponentChooseComboBox.Items
            .Cast<ComponentModel>()
            .FirstOrDefault(c => c.Id == componentId);
    }

    private async Task AskAutoFillFiles(ConfigItem configItem, string dir, AskAutoFillMode askAutoFillMode = AskAutoFillMode.Ask)
    {
        try
        {
            List<string> usedFiles = ConfigurationService.GetUsedFilesFromConfigItem(configItem);

            SelectUsedFilesForConfigContentDialog dialog = new(usedFiles, configItem.name, dir, configItem, askAutoFillMode)
            {
                XamlRoot = this.XamlRoot
            };
            var result = await dialog.ShowQueuedAsync();
            if (dialog.Result == CreateConfigResult.Selected)
            {
                _configItem = ConfigurationService.ReplaceFilesPath(_configItem, dialog.Files);
                AddWrap();

                LoadVars(configItem);
                AuditSaveAvailable();
            }
            else
            {
                CreateConfigHelperWindow.Instanse.IsOperationExitAskAvailable = false;
                Frame.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
            }
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(nameof(CreateNewConfigPage), exception.ToString());
        }
    }

    private void LoadConditions(ConfigItem configItem)
    {
        Conditions.Clear();
        if (configItem.variables == null || configItem.variables.Count == 0)
        { return; }

        List<VariableItem> variables = ConfigurationService.GetVariables(configItem);
        ComponentHelper componentHelper =
            ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId((ComponentChooseComboBox.SelectedItem as ComponentModel).Id);

        foreach (var variable in configItem.variables)
        {
            var t = LScriptLangHelper.GetNameOnOffValuesFromConditionString(variable);
            if (t == null)
                continue;

            var (_var, conditionVarName, onValue, offValue) = t;
            Conditions.Add(
                new()
                {
                    Name = conditionVarName,
                    OnValue = onValue,
                    OffValue = offValue,
                    Description = componentHelper?.GetConfigHelper()?.GetLocalizedConfigVarName(_var, configItem.packId) ?? string.Empty,
                });
        }
    }

    private void LoadVars(ConfigItem configItem)
    {
        Variables.Clear();

        if (configItem.variables != null)
        {
            foreach (var variable in configItem.variables)
            {
                var t = LScriptLangHelper.GetNameOnOffValuesFromConditionString(variable);

                if (t == null)
                {
                    // Only the first '=' separates the name; values may contain '=' themselves.
                    int separator = variable.IndexOf('=');
                    if (separator < 0)
                        continue;
                    Variables.Add(new()
                    {
                        Name = variable[..separator].Replace("%", ""),
                        Value = variable[(separator + 1)..]
                    });
                }
            }
        }

        if (configItem.commaVars != null)
        {
            foreach (var commaVar in configItem.commaVars)
            {
                try
                {
                    Variables.Add(new VariableModel
                    {
                        Name = commaVar.Key,
                        Value = commaVar.Value,
                        AvailableValues = configItem.availableCommaVarsValues?.FirstOrDefault(varItem => varItem.VarName == commaVar.Key, null)
                    });
                }
                catch (Exception ex)
                {
                    Logger.Instance.CreateWarningLog(nameof(CreateNewConfigPage), $"{ex}");
                }
            }
        }

        if (Variables.Count == 0)
        {
            VariablesStackPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            VariablesStackPanel.Visibility = Visibility.Visible;
        }
    }

    private void AddWrap()
    {
        if (_configItem != null)
        {
            string text = CreateConfigPageHelper.ApplyWrappingToString(_configItem.startup_string);
            StartupStringTextBox.Text = GetPrettyLookText(text);
            StartupStringTextBox.SetCursorPosition(0, 0);
        }
    }

    private void AddWrap(string startupString)
    {
        string text = CreateConfigPageHelper.ApplyWrappingToString(startupString);
        StartupStringTextBox.Text = GetPrettyLookText(text);
        StartupStringTextBox.SetCursorPosition(0, 0);
    }

    private string GetPrettyLookText(string text)
    {
        var result = CreateConfigPageHelper.ApplyPrettyFilesReplacement(text);

        _listDirectory = result.ListDirectory;
        _binDirectory = result.BinDirectory;

        CheckFolderOpenButtonsVisibility();

        return result.ResultText;
    }
}
