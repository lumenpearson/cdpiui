using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.Controls.ComponentSettings;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Helper.Items;

namespace CDPIUI.ViewModels.ComponentViews;

public sealed partial class ComponentTileViewModel
{
    private int _configLoadVersion;

    /// <summary>Called by the view when the user or the saved selection changes the configuration.</summary>
    public async Task SelectConfigAsync(ConfigSelectorItem sel)
    {
        SelectedConfig = sel;
        if (sel is null)
            return;

        string oldCfg = SettingsManager.Instance.GetValue<string>(["CONFIGS", _storeId], "configFile");
        string oldId = SettingsManager.Instance.GetValue<string>(["CONFIGS", _storeId], "configId");
        SettingsManager.Instance.SetValue<string>(["CONFIGS", _storeId], "configFile", sel.FileName);
        SettingsManager.Instance.SetValue<string>(["CONFIGS", _storeId], "configId", sel.PackId);

        ComponentHelper componentHelper =
            ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(_storeId);
        if (componentHelper != null)
        {
            try
            {
                await Task.Run(() => componentHelper.PrepareSelectedConfig(sel.FileName, sel.PackId));
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateWarningLog(
                    LogSource,
                    $"Cannot prepare selected config '{sel.PackId}/{sel.FileName}': {ex}");
            }
        }

        await InitConfigSettings(_storeId);

        if ((oldCfg != sel.FileName || oldId != sel.PackId) && await ComponentTasksManager.Instance.IsTaskRunning(_storeId))
            await ComponentTasksManager.Instance.RestartTask(_storeId);
    }

    /// <summary>Called by the view when a quick setting switch of the selected configuration is toggled.</summary>
    public async Task SetConfigSettingAsync(string id, bool value)
    {
        var item = ConfigSettings.FirstOrDefault((x) => x.Id == id);
        if (item == null)
            return;

        var sel = _selectedConfig;
        if (sel == null)
            return;

        ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(_storeId);
        componentHelper.GetConfigHelper().ChangeVariableValue(sel.FileName, sel.PackId, id, value);

        if (await ComponentTasksManager.Instance.IsTaskRunning(_storeId))
            _ = ComponentTasksManager.Instance.RestartTask(_storeId);
    }

    private async Task LoadConfigInfo(string storeId)
    {
        _ = LoadConfigItemsAsync(storeId); // LoadConfigItemsAsync catches and logs its own errors.

        ComponentHelper componentHelper =
            ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(storeId);
        if (componentHelper is null)
            return;

        await InitConfigSettings(storeId);

        componentHelper.ConfigListUpdated += LoadConfigItems;
    }

    private Task<ConfigSelectorItem> GetSelectedItemAsync()
    {
        if (_dispatcherQueue.HasThreadAccess)
            return Task.FromResult(_selectedConfig);

        var tcs = new TaskCompletionSource<ConfigSelectorItem>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        bool queued = _dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                tcs.SetResult(_selectedConfig);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        if (!queued)
            tcs.SetException(new InvalidOperationException());

        return tcs.Task;
    }

    private async Task InitConfigSettings(string storeId)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            IsConfigSettingsLoadFailureVisible = true;
            ConfigSettings.Clear();
        });
        var sel = await GetSelectedItemAsync();
        if (sel == null)
            return;

        ComponentHelper componentHelper =
            ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(storeId);
        if (componentHelper is null)
            return;

        List<VariableItem> variables = componentHelper.GetConfigHelper().GetVariables(sel.FileName, sel.PackId);
        List<string> toggleLists = componentHelper.GetConfigHelper().GetToggleLists(sel.FileName, sel.PackId);

        if (variables.Count > 0 || toggleLists.Count > 0)
        {
            foreach (var variable in variables)
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    ConfigSettings.Add(new()
                    {
                        DisplayName = $"{componentHelper.GetConfigHelper().GetLocalizedConfigVarName(variable.name, sel.PackId)}",
                        Id = variable.name,
                        Value = variable.value
                    });
                });
            }
        }
        _dispatcherQueue.TryEnqueue(() =>
        {
            if (ConfigSettings.Count > 0)
                IsConfigSettingsLoadFailureVisible = false;
        });
    }

    private void LoadConfigItems()
    {
        _ = LoadConfigItemsAsync(_storeId); // LoadConfigItemsAsync catches and logs its own errors.
    }

    private async Task LoadConfigItemsAsync(string storeId)
    {
        try
        {
            int loadVersion = Interlocked.Increment(ref _configLoadVersion);

            ComponentItemsLoaderHelper.Instance.Init(forse: false);

            ComponentHelper componentHelper =
                ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(storeId);

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
                Logger.Instance.CreateWarningLog(
                    LogSource,
                    $"Cannot load configs for component '{storeId}': {ex}");
                items = [];
            }

            _dispatcherQueue.TryEnqueue(() =>
                ApplyLoadedConfigItems(storeId, loadVersion, items));
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    private void ApplyLoadedConfigItems(
        string componentId,
        int loadVersion,
        IReadOnlyList<ConfigItem> items)
    {
        if (loadVersion != _configLoadVersion ||
            !string.Equals(componentId, _storeId, StringComparison.Ordinal))
        {
            return;
        }

        ConfigItems.Clear();

        PackNameLookup packNames = new();
        foreach (ConfigItem item in items)
        {
            ConfigItems.Add(new()
            {
                FileName = item.file_name,
                PackId = item.packId,
                DisplayName = item.name,
                PackDisplayName = packNames.GetDisplayName(item.packId),
                IsLegacyConfig = item.IsLegacy
            });
        }

        // The saved selection is applied after the list is filled: the combo box gets the
        // items through its own CollectionChanged handler, which runs after this view
        // model's, and it ignores a selected item that is not in its list yet.
        ApplySavedSelection();

        bool hasItems = ConfigItems.Count > 0;
        IsConfigContentVisible = hasItems;
        IsConfigErrorVisible = !hasItems;
    }
}
