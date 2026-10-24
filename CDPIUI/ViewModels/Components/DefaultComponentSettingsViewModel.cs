using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.Controls.ComponentSettings;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.Proxy;
using CDPIUI.Helper;
using Microsoft.UI.Dispatching;
using WinUI3Localizer;
using static CDPIUI.Helper.UIHelper;

namespace CDPIUI.ViewModels.Components;

/// <summary>
/// State of the default component settings page: the configuration list and selection,
/// the loading and empty states, and the setting tiles of the selected configuration with
/// the actions of their elements.
/// </summary>
public sealed partial class DefaultComponentSettingsViewModel : ObservableObject
{
    private const string LogSource = "DefaultComponentSettingsPage";

    private readonly ILocalizer _localizer = Localizer.Get();
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly IDefaultComponentSettingsView _view;

    private ComponentPresetListSubscription _presetListSubscription;
    private bool _updatingConfigList;

    // Several builds can run at once (Task.Run from different events); only the latest
    // one may fill the list, otherwise the tiles appeared twice.
    private int _tilesGeneration;

    private string _componentId = string.Empty;
    private ConfigSelectorItem _selectedConfig;
    private bool _isLoading = true;
    private bool _isMainPanelVisible;
    private bool _isEmptyPageVisible;
    private bool _isHelpfulLinksVisible;
    private bool _isSetupAsProxyLinkVisible = true;

    public DefaultComponentSettingsViewModel(DispatcherQueue dispatcherQueue, IDefaultComponentSettingsView view)
    {
        _dispatcherQueue = dispatcherQueue;
        _view = view;
    }

    public ObservableCollection<ConfigSelectorItem> ConfigItems { get; } = [];

    public string ComponentId
    {
        get => _componentId;
        set => SetProperty(ref _componentId, value);
    }

    /// <summary>Whether new tiles slide in; false while the tiles of the same configuration are rebuilt.</summary>
    public bool ShowAnimation { get; private set; } = true;

    public ConfigSelectorItem SelectedConfig
    {
        get => _selectedConfig;
        private set => SetProperty(ref _selectedConfig, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public bool IsMainPanelVisible
    {
        get => _isMainPanelVisible;
        private set => SetProperty(ref _isMainPanelVisible, value);
    }

    public bool IsEmptyPageVisible
    {
        get => _isEmptyPageVisible;
        private set => SetProperty(ref _isEmptyPageVisible, value);
    }

    public bool IsHelpfulLinksVisible
    {
        get => _isHelpfulLinksVisible;
        private set => SetProperty(ref _isHelpfulLinksVisible, value);
    }

    public bool IsSetupAsProxyLinkVisible
    {
        get => _isSetupAsProxyLinkVisible;
        private set => SetProperty(ref _isSetupAsProxyLinkVisible, value);
    }

    public void Load()
    {
        _presetListSubscription?.Dispose();
        _presetListSubscription = new(ComponentId, _dispatcherQueue, ApplyPresetItems);
    }

    public void Unload()
    {
        _presetListSubscription?.Dispose();
        _presetListSubscription = null;
    }

    /// <summary>
    /// Called by the view when the selected configuration changes. The choice is applied
    /// only while the page is loaded and the list is not being refilled.
    /// </summary>
    public async Task SelectConfigAsync(ConfigSelectorItem sel, bool isPageLoaded)
    {
        SelectedConfig = sel;
        if (_updatingConfigList || !isPageLoaded)
            return;
        if (sel is not null)
        {
            string oldCfg = SettingsManager.Instance.GetValue<string>(["CONFIGS", ComponentId], "configFile");
            string oldId = SettingsManager.Instance.GetValue<string>(["CONFIGS", ComponentId], "configId");
            SettingsManager.Instance.SetValue<string>(["CONFIGS", ComponentId], "configFile", sel.FileName);
            SettingsManager.Instance.SetValue<string>(["CONFIGS", ComponentId], "configId", sel.PackId);

            ComponentHelper componentHelper =
                ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(ComponentId);
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

            if ((oldCfg != sel.FileName || oldId != sel.PackId) && await ComponentTasksManager.Instance.IsTaskRunning(ComponentId))
                await ComponentTasksManager.Instance.RestartTask(ComponentId);

            _ = Task.Run(() => InitSettingsTiles(sel));
        }
        ShowAnimation = true;
    }

    /// <summary>Called by the helpful links control to edit the selected configuration.</summary>
    public void EditSelectedConfig()
    {
        ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(ComponentId);
        ConfigurationService service = componentHelper.GetConfigHelper();
        if (_selectedConfig is ConfigSelectorItem item)
            _ = _view.EditConfigAsync(service.GetConfigItem(item.FileName, item.PackId)); // EditConfig catches and logs its own errors.
    }

    private void ApplyPresetItems(IReadOnlyList<ConfigSelectorItem> items)
    {
        _updatingConfigList = true;
        try
        {
            ConfigItems.Clear();
            foreach (var item in items)
                ConfigItems.Add(item);
            ShowContent(ConfigItems.Count > 0);
        }
        finally { _updatingConfigList = false; }
        ApplySavedSelection();
    }

    private void ShowContent(bool hasConfigs)
    {
        IsLoading = false;
        IsMainPanelVisible = hasConfigs;
        IsEmptyPageVisible = !hasConfigs;
    }

    private void ApplySavedSelection()
    {
        var savedFile = SettingsManager.Instance.GetValue<string>(["CONFIGS", ComponentId], "configFile");
        var savedPackId = SettingsManager.Instance.GetValue<string>(["CONFIGS", ComponentId], "configId");

        if (string.IsNullOrEmpty(savedFile) || string.IsNullOrEmpty(savedPackId))
            return;

        var match = ConfigItems.FirstOrDefault(ci => ci.FileName == savedFile && ci.PackId == savedPackId);
        if (match != null)
            SelectedConfig = match;
    }

    private void AddTileIfCurrent(int generation, SettingsTile tile)
    {
        if (generation == Volatile.Read(ref _tilesGeneration))
            _view.AddTile(tile);
    }

    private void InitSettingsTiles(ConfigSelectorItem sel)
    {
        int generation = Interlocked.Increment(ref _tilesGeneration);

        _dispatcherQueue.TryEnqueue(() =>
        {
            if (generation != Volatile.Read(ref _tilesGeneration))
                return;
            _view.ClearTiles();
            IsHelpfulLinksVisible = false;
            IsSetupAsProxyLinkVisible = ProxyHelper.ProxyLikeComponents.Contains(ComponentId);
        });

        if (sel == null)
            return;

        ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(ComponentId);
        ConfigurationService configHelper = componentHelper.GetConfigHelper();
        ComponentSettingsTileBuilder tileBuilder = new(configHelper, _localizer);

        List<VariableItem> variables = configHelper.GetVariables(sel.FileName, sel.PackId);
        List<CommaVariableItem> commaVariables = configHelper
            .GetCommaVariables(sel.FileName, sel.PackId);
        List<string> toggleLists = configHelper.GetToggleLists(sel.FileName, sel.PackId);

        if (variables.Count > 0 || toggleLists.Count > 0)
        {
            SettingsTile variablesItem = tileBuilder.BuildVariablesTile(variables, sel.FileName, sel.PackId);
            _dispatcherQueue.TryEnqueue(() => AddTileIfCurrent(generation, variablesItem));
        }

        List<SiteListItem> list = configHelper.GetSiteListItems(sel.FileName, sel.PackId, ignoreNull: true);
        if (list.Count > 0)
        {
            SettingsTile sitelistTile = tileBuilder.BuildSiteListTile(
                list, sel.PackId, "UsedSiteLists", "UsedSiteListsTip");
            _dispatcherQueue.TryEnqueue(() => AddTileIfCurrent(generation, sitelistTile));
        }
        List<SiteListItem> excludeList = configHelper.GetExcludedSiteListItems(sel.FileName, sel.PackId, ignoreNull: true);
        if (excludeList.Count > 0)
        {
            SettingsTile sitelistTile = tileBuilder.BuildSiteListTile(
                excludeList, sel.PackId, "UsedExcludedSiteLists", "UsedExcludedSiteListsTip");
            _dispatcherQueue.TryEnqueue(() => AddTileIfCurrent(generation, sitelistTile));
        }

        if (commaVariables.Count > 0)
        {
            SettingsTile commaVariablesWidget = tileBuilder.BuildCommaVariablesTile(commaVariables, sel.FileName, sel.PackId);
            _dispatcherQueue.TryEnqueue(() => AddTileIfCurrent(generation, commaVariablesWidget));
        }

        SettingsTile helpTile = tileBuilder.BuildHelpTile();

        _dispatcherQueue.TryEnqueue(() =>
        {
            AddTileIfCurrent(generation, helpTile);
            IsHelpfulLinksVisible = true;
        });
    }

    private void RebuildTilesWithoutAnimation()
    {
        ShowAnimation = false;
        var item = _selectedConfig;
        _ = Task.Run(() => InitSettingsTiles(item));
    }

    // Waiting for the task list on the UI thread with .Result could deadlock.
    private async Task RestartComponentIfRunning()
    {
        if (await ComponentTasksManager.Instance.IsTaskRunning(ComponentId))
            await ComponentTasksManager.Instance.RestartTask(ComponentId);
    }
}
