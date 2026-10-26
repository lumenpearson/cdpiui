using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.Controls.Dialogs.CreateConfigHelper;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Shared;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace CDPIUI.Views.CreateConfigHelper;

// Building the config from the page and saving it.
public sealed partial class CreateNewConfigPage
{
    private async void SaveConfigButton_Click(object sender, RoutedEventArgs e)
    {
        ConvertDesignerLikeSettingsToString();
        TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
        int secondsSinceEpoch = (int)t.TotalSeconds;

        ConfigItem configItem = CreateConfig(secondsSinceEpoch);

        if (configItem == null)
        {
            return;
        }

        if (PageOpenMode == PageOpenModes.EditConfigNoSave)
        {
            var window = ((App)Application.Current).GetCurrentWindowFromType<CreateConfigHelperWindow>();
            if (window != null)
                window.IsOperationExitAskAvailable = false;
            window?.NavigateBackWithParameter(configItem);
            return;
        }

        string src;
        if (_configItem == null || _configItem.packId != SharedConstants.LocalUserItemsId)
            src = DisplayNameTextBox.Text + $"_{secondsSinceEpoch}.json";
        else
            src = _configItem.file_name;

        string transl = ConfigMakerPresetStorageNaming.ToAsciiSlug(src);

        string errorCode = await ConfigurationService.SaveConfigItem(transl, SharedConstants.LocalUserItemsId, configItem);
        if (!string.IsNullOrEmpty(errorCode))
        {
            await ShowErrorDialog(
                string.Format(_localizer.GetLocalizedString("CannotSaveConfigMessage"), errorCode),
                _localizer.GetLocalizedString("SomethingWentWrong")
                );
            return;
        }

        ComponentHelper componentHelper =
                ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(
                    (ComponentChooseComboBox.SelectedItem as ComponentModel).Id);
        componentHelper?.ReInitConfigs();

        await ShowDialog();
    }

    private ConfigItem CreateConfig(int secondsSinceEpoch, bool saveLocalization = true)
    {
        string componentId = (ComponentChooseComboBox.SelectedItem as ComponentModel).Id;
        var (jparams, vars) = CreateVariables(secondsSinceEpoch, saveLocalization);

        if (jparams == null || vars == null)
            return null;

        var configItem = CreateConfigPageHelper.CreateConfigItem(
            SharedConstants.LocalUserItemsId,
            DisplayNameTextBox.Text,
            componentId,
            jparams,
            vars,
            // A preset can define the same variable twice (as X= and as a comma variable);
            // the last value wins instead of an exception on save.
            Variables.GroupBy(v => v.Name).ToDictionary(g => g.Key, g => g.Last().Value),
            Variables.Where(v => v.AvailableValues != null).Select(v => v.AvailableValues).ToList(),
            CreateConfigPageHelper.GetNormalText(StartupStringTextBox.Text, _listDirectory, _binDirectory)
            );

        return configItem;
    }

    private Tuple<Dictionary<string, bool>, List<string>> CreateVariables(int secondsSinceEpoch, bool saveLocalization)
    {
        try
        {
            return CreateConfigConditionBuilder.Build(
                Conditions,
                secondsSinceEpoch,
                EditItemId,
                saveLocalization);
        }
        catch (Exception ex)
        {
            // ShowErrorDialog catches and logs its own errors.
            _ = ShowErrorDialog(
                string.Format(_localizer.GetLocalizedString("CannotSaveConfigMessage"), ErrorsHelper.Convertor.GetPrettyErrorCode("CREATE_CONFIG", ex)),
                _localizer.GetLocalizedString("SomethingWentWrong")
                );
            Logger.Instance.CreateWarningLog(nameof(CreateNewConfigPage), $"{ex}");
            return new(null, null);
        }
    }

    private async Task ShowDialog()
    {
        CreateCompleteDialog dialog = new()
        {
            XamlRoot = this.XamlRoot
        };
        CreateConfigHelperWindow.Instanse.IsOperationExitAskAvailable = false;
        if (await dialog.ShowQueuedAsync() == ContentDialogResult.Primary)
            CreateConfigHelperWindow.Instanse.Close();
        else
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
            else
                Frame.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
        }
    }
}
