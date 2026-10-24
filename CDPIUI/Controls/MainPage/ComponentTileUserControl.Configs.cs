using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CDPIUI.Controls.ComponentSettings;
using CDPIUI.Controls.Dialogs.ComponentSettings;
using CDPIUI.Core;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper;
using CDPIUI.Helper.Items;
using CDPIUI.Shared;
using CDPIUI.Shared.Extensions;
using CDPIUI.ViewModels;
using CDPIUI.Views.CreateConfigUtil;
using CDPIUI.Views.Main.Components;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;
using static CDPIUI.Helper.UIHelper;
using ToolTip = Microsoft.UI.Xaml.Controls.ToolTip;
using ToolTipService = Microsoft.UI.Xaml.Controls.ToolTipService;
namespace CDPIUI.Controls.MainPage;

public sealed partial class ComponentTileUserControl
{
    private async Task LoadConfigInfo(string storeId)
    {
        _ = LoadConfigItems(storeId); // LoadConfigItems catches and logs its own errors.

        ComponentHelper componentHelper =
            ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(
                storeId);
        if (componentHelper is null)
            return;

        await InitConfigSettings(storeId);
        await Task.CompletedTask;

        componentHelper.ConfigListUpdated += LoadConfigItems;
    }

    private Task<ConfigSelectorItem> GetSelectedItemAsync()
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            return Task.FromResult(
                ConfigChooseCombobox.SelectedItem as ConfigSelectorItem);
        }

        var tcs = new TaskCompletionSource<ConfigSelectorItem?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        bool queued = DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                tcs.SetResult(
                    ConfigChooseCombobox.SelectedItem as ConfigSelectorItem);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        if (!queued)
        {
            tcs.SetException(
                new InvalidOperationException());
        }

        return tcs.Task;
    }

    private async Task InitConfigSettings(string storeId)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ConfigSettingsLoadFailureTextBlock.Visibility = Visibility.Visible;
            _configSettingsList.Clear();
        });
        var sel = await GetSelectedItemAsync();
        if (sel == null)
            return;

        ComponentHelper componentHelper =
                ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(
                    storeId);
        if (componentHelper is null)
            return;

        List<VariableItem> variables = componentHelper.GetConfigHelper().GetVariables(sel.FileName, sel.PackId);
        List<string> toggleLists = componentHelper.GetConfigHelper().GetToggleLists(sel.FileName, sel.PackId);

        if (variables.Count > 0 || toggleLists.Count > 0)
        {
            foreach (var variable in variables)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    _configSettingsList.Add(new()
                    {
                        DisplayName = $"{componentHelper.GetConfigHelper().GetLocalizedConfigVarName(variable.name, sel.PackId)}",
                        Id = variable.name,
                        Value = variable.value
                    });
                });
            }
        }
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_configSettingsList.Count > 0)
            {
                ConfigSettingsLoadFailureTextBlock.Visibility = Visibility.Collapsed;
            }
        });
    }

    private void LoadConfigItems()
    {
        _ = LoadConfigItems(StoreId); // LoadConfigItems catches and logs its own errors.
    }

    private async Task LoadConfigItems(string storeId)
    {
        try
        {
            int loadVersion = Interlocked.Increment(ref _configLoadVersion);

            ComponentItemsLoaderHelper.Instance.Init(forse: false);

            ComponentHelper componentHelper =
                ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(
                    storeId);

            if (componentHelper is null)
                return;

            List<ConfigItem> items;
            try
            {
                items = await Task.Run(() =>
                    componentHelper.GetConfigHelper().GetConfigItems().ToList());
            }
            catch (Exception ex)
            {
                CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(
                    nameof(ComponentTileUserControl),
                    $"Cannot load configs for component '{storeId}': {ex}");
                items = [];
            }

            DispatcherQueue.TryEnqueue(() =>
                ApplyLoadedConfigItems(storeId, loadVersion, items));
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(ComponentTileUserControl), exception.ToString());
        }
    }

    private void ApplyLoadedConfigItems(
        string componentId,
        int loadVersion,
        IReadOnlyList<ConfigItem> items)
    {
        if (loadVersion != _configLoadVersion ||
            !string.Equals(componentId, StoreId, StringComparison.Ordinal))
        {
            return;
        }

        _configItems.Clear();

        PackNameLookup packNames = new();
        foreach (ConfigItem item in items)
        {
            ConfigSelectorItem configItem = new()
            {
                FileName = item.file_name,
                PackId = item.packId,
                DisplayName = item.name,
                PackDisplayName = packNames.GetDisplayName(item.packId),
                IsLegacyConfig = item.IsLegacy
            };

            _configItems.Add(configItem);
        }

        if (_configItems.Count == 0)
        {
            ToggleVisibility(false);
        }
        else
        {
            ToggleVisibility(true);
        }
    }

    private void ToggleVisibility(bool visible)
    {
        if (!visible)
        {
            ContentStackPanel.Visibility = Visibility.Collapsed;
            ErrorStackPanel.Visibility = Visibility.Visible;
        }
        else
        {
            ContentStackPanel.Visibility = Visibility.Visible;
            ErrorStackPanel.Visibility = Visibility.Collapsed;
        }
    }

    private async void ConfigChooseCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ConfigChooseCombobox.SelectedItem is ConfigSelectorItem sel)
        {
            string oldCfg = SettingsManager.Instance.GetValue<string>(["CONFIGS", StoreId], "configFile");
            string oldId = SettingsManager.Instance.GetValue<string>(["CONFIGS", StoreId], "configId");
            SettingsManager.Instance.SetValue<string>(["CONFIGS", StoreId], "configFile", sel.FileName);
            SettingsManager.Instance.SetValue<string>(["CONFIGS", StoreId], "configId", sel.PackId);

            ComponentHelper componentHelper =
                ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(StoreId);
            if (componentHelper != null)
            {
                try
                {
                    await Task.Run(() => componentHelper.PrepareSelectedConfig(sel.FileName, sel.PackId));
                }
                catch (Exception ex)
                {
                    CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(
                        nameof(ComponentTileUserControl),
                        $"Cannot prepare selected config '{sel.PackId}/{sel.FileName}': {ex}");
                }
            }

            await InitConfigSettings(StoreId);

            if ((oldCfg != sel.FileName || oldId != sel.PackId) && await ComponentTasksManager.Instance.IsTaskRunning(StoreId))
                await ComponentTasksManager.Instance.RestartTask(StoreId);
        }
    }

    private void ApplySavedSelection()
    {
        var savedFile = SettingsManager.Instance.GetValue<string>(["CONFIGS", StoreId], "configFile");
        var savedPackId = SettingsManager.Instance.GetValue<string>(["CONFIGS", StoreId], "configId");

        if (string.IsNullOrEmpty(savedFile) || string.IsNullOrEmpty(savedPackId))
            return;

        var match = _configItems
            .FirstOrDefault(ci => ci.FileName == savedFile
                               && ci.PackId == savedPackId);
        if (match != null)
            ConfigChooseCombobox.SelectedItem = match;
    }

    private void ConfigItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        ApplySavedSelection();
    }

    private async void ConfigSettingToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        bool value = (sender as ToggleSwitch).IsOn;
        string id = (string)(sender as ToggleSwitch).Tag;

        var item = _configSettingsList.FirstOrDefault((x) => x.Id == id);
        if (item == null)
            return;

        var sel = ConfigChooseCombobox.SelectedItem as ConfigSelectorItem;
        if (sel == null)
            return;

        ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(StoreId);
        componentHelper.GetConfigHelper().ChangeVariableValue(sel.FileName, sel.PackId, id, value);

        if (await ComponentTasksManager.Instance.IsTaskRunning(StoreId))
            _ = ComponentTasksManager.Instance.RestartTask(StoreId);
    }
}
