using System;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.Controls.ComponentSettings;
using CDPIUI.Controls.Dialogs.ComponentSettings;
using CDPIUI.Controls.Universal;
using CDPIUI.Core;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Extensions;
using CDPIUI.Helper;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Helper.TgWsProxy;
using CDPIUI.Shared;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Windows.UI.Popups;
using WinRT.Interop;
using Application = Microsoft.UI.Xaml.Application;

namespace CDPIUI.Views.Main.Components;

public sealed partial class TgWsProxyComponentPage
{
    private void HandleCreatePlCommand(AvailableConfigCreationActios action)
    {
        if (action == AvailableConfigCreationActios.CreateFromTemplate)
            _ = CreatePlConfig(); // CreatePlConfig catches and logs its own errors.
    }

    private async Task CreatePlConfig()
    {
        try
        {
            var item = CreateConfigPageHelper.CreateConfigItem(
                SharedConstants.LocalUserItemsId, _localizer.GetLocalizedString("DefaultConfigPlaceholder"), ComponentId, [], [], [], [], _defaultTgWsProxyStartString
                );
            string fileName = $"{Guid.NewGuid()}.json";
            string errorCode = await ConfigurationService.SaveConfigItem(fileName, SharedConstants.LocalUserItemsId, item);

            if (!string.IsNullOrEmpty(errorCode))
            {
                await ShowErrorDialog(string.Format(_localizer.GetLocalizedString("SaveConfigException"), errorCode), _localizer.GetLocalizedString("SomethingWentWrong"));
                return;
            }

            SettingsManager.Instance.SetValue<string>(["CONFIGS", ComponentId], "configFile", fileName);
            SettingsManager.Instance.SetValue<string>(["CONFIGS", ComponentId], "configId", SharedConstants.LocalUserItemsId);

            ComponentHelper componentHelper =
                ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(ComponentId);
            componentHelper.ReInitConfigs();
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(TgWsProxyComponentPage), exception.ToString());
        }
    }

    private async Task CreateConfig()
    {
        try
        {
            var dialog = new CreateNewConfigForComponentContentDialog()
            {
                XamlRoot = this.XamlRoot,
            };

            await dialog.ShowQueuedAsync();

            if (dialog.Result == CreateNewConfigForComponentContentDialog.ConfigCreationVariants.FromTemplate)
            {
                await CreatePlConfig();
            }
            else if (dialog.Result == CreateNewConfigForComponentContentDialog.ConfigCreationVariants.Manual)
            {
                await ConfigMakerWindow.CreateForComponentAsync(ComponentId);
            }
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(TgWsProxyComponentPage), exception.ToString());
        }
    }

    private void CreateConfigButton_Click(object sender, RoutedEventArgs e)
    {
        _ = CreateConfig(); // CreateConfig catches and logs its own errors.
    }

    private string GetStartupString(string startupString)
    {
        TgWsProxyStartupValues values = new();
        values.Options[TgWsProxyStartupArguments.PortOption] = PortValue.Text;
        values.Options[TgWsProxyStartupArguments.HostOption] = IPValue.Text;
        values.Options[TgWsProxyStartupArguments.SecretOption] = KeyValue.Text;
        values.Options[TgWsProxyStartupArguments.BufferSizeOption] = BufferSizeTextBox.Text;
        values.Options[TgWsProxyStartupArguments.PoolSizeOption] = WebSocketSizeTextBox.Text;
        values.Options[TgWsProxyStartupArguments.LogMaxSizeOption] = LogSizeTextBox.Text;
        values.Verbose = AdvancedLoggingToggleSwitch.IsOn;

        foreach (var item in _telegramDataCenterModels)
        {
            values.DataCenters.Add(new(item.Guid, item.Number, item.Ip));
        }
        return TgWsProxyStartupArguments.Build(startupString, values);
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var sel = ConfigChooseCombobox.SelectedItem as ConfigSelectorItem;
        ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(ComponentId);

        var item = componentHelper.GetConfigHelper().GetConfigItem(sel.FileName, sel.PackId);

        item.startup_string = GetStartupString(item.startup_string);

        string errorCode = await ConfigurationService.SaveConfigItem(sel.FileName, sel.PackId, item);
        if (!string.IsNullOrEmpty(errorCode))
        {
            await ShowErrorDialog(string.Format(_localizer.GetLocalizedString("SaveConfigException"), errorCode), _localizer.GetLocalizedString("SomethingWentWrong"));
            return;
        }

        componentHelper.ReInitConfigs();
    }

    private async Task ShowErrorDialog(string message, string title)
    {
        var dlg = new MessageDialog(message, title);
        InitializeWithWindow.Initialize(
            dlg, WindowNative.GetWindowHandle(await ((App)Application.Current).SafeCreateNewWindow<ModernMainWindow>())
            );
        await dlg.ShowAsync();
    }

    private async Task LinkedSettingClicked(object param)
    {
        try
        {
            if (param is LinkedActions action)
            {
                switch (action)
                {
                    case LinkedActions.EditCurrentConfig:
                        ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(ComponentId);
                        var item = (ConfigSelectorItem)ConfigChooseCombobox.SelectedItem;

                        if (componentHelper != null && item != null)
                            await ConfigMakerWindow.EditConfig(componentHelper.GetConfigHelper().GetConfigItems().FirstOrDefault(x => x.packId == item.PackId && x.file_name == item.FileName));
                        break;
                    case LinkedActions.CreateNewConfigForComponent:
                        await CreateConfig();
                        break;
                    case LinkedActions.OpenProxyInTelegram:
                        ConnectTelegramProxyContentDialog dialog = new()
                        {
                            XamlRoot = this.XamlRoot,
                        };
                        await dialog.ShowQueuedAsync();
                        break;
                }
            }
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(TgWsProxyComponentPage), exception.ToString());
        }
    }
}
