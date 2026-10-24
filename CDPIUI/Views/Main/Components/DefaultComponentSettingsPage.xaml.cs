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

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Views.Main.Components;

public sealed partial class DefaultComponentSettingsPage : TemplatePage
{
    private string ComponentId = string.Empty;
    private ObservableCollection<ConfigSelectorItem> _configItems = new();

    private readonly ObservableCollection<UIElement> _tiles = new();

    private bool _showAnim = true;
    private ILocalizer _localizer = Localizer.Get();
    public DefaultComponentSettingsPage()
    {
        InitializeComponent();

        ConfigChooseCombobox.ItemsSource = _configItems;

        Loaded += PresetPage_Loaded;
        Unloaded += PresetPage_Unloaded;

        StaggeredRepeater.ItemsSource = _tiles;

        DataContext = this;
        ToggleLoading(true);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (Parameter != null)
        {
            ComponentId = Parameter.Get("componentId") ?? string.Empty;
            EmptyPlaceholder.StoreId = ComponentId;
        }

    }

    // Several builds can run at once (Task.Run from different events); only the latest
    // one may fill the list, otherwise the tiles appeared twice.
    private int _tilesGeneration;

    private void AddTileIfCurrent(int generation, UIElement tile)
    {
        if (generation == Volatile.Read(ref _tilesGeneration))
            _tiles.Add(tile);
    }

    private void InitSettingsTiles(ConfigSelectorItem sel)
    {
        int generation = Interlocked.Increment(ref _tilesGeneration);

        DispatcherQueue.TryEnqueue(() =>
        {
            if (generation != Volatile.Read(ref _tilesGeneration))
                return;
            _tiles.Clear();
            HelpfulLinksControl.Visibility = Visibility.Collapsed;
            SetupAsProxyHelpfulLink.IsVisible = ProxyHelper.ProxyLikeComponents.Contains(ComponentId);
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
            DispatcherQueue.TryEnqueue(() =>
            {
                AddTileIfCurrent(generation, CreateSettingTile(variablesItem, HandleSettingTileElementClick));
            });
        }

        List<SiteListItem> list = configHelper.GetSiteListItems(sel.FileName, sel.PackId, ignoreNull: true);
        if (list.Count > 0)
        {
            SettingsTile sitelistTile = tileBuilder.BuildSiteListTile(
                list, sel.PackId, "UsedSiteLists", "UsedSiteListsTip");
            DispatcherQueue.TryEnqueue(() => { AddTileIfCurrent(generation, CreateSettingTile(sitelistTile, HandleSettingTileElementClick)); });
        }
        List<SiteListItem> excludeList = configHelper.GetExcludedSiteListItems(sel.FileName, sel.PackId, ignoreNull: true);
        if (excludeList.Count > 0)
        {
            SettingsTile sitelistTile = tileBuilder.BuildSiteListTile(
                excludeList, sel.PackId, "UsedExcludedSiteLists", "UsedExcludedSiteListsTip");
            DispatcherQueue.TryEnqueue(() => { AddTileIfCurrent(generation, CreateSettingTile(sitelistTile, HandleSettingTileElementClick)); });
        }

        if (commaVariables.Count > 0)
        {
            SettingsTile commaVariablesWidget = tileBuilder.BuildCommaVariablesTile(commaVariables, sel.FileName, sel.PackId);
            DispatcherQueue.TryEnqueue(() =>
            {
                AddTileIfCurrent(generation, CreateSettingTile(commaVariablesWidget, HandleSettingTileElementClick));
            });
        }

        SettingsTile helpTile = tileBuilder.BuildHelpTile();

        DispatcherQueue.TryEnqueue(() =>
        {
            AddTileIfCurrent(generation, CreateSettingTile(helpTile, HandleSettingTileElementClick));
            HelpfulLinksControl.Visibility = Visibility.Visible;
        });
    }

    // Waiting for the task list on the UI thread with .Result could deadlock.
    private async Task RestartComponentIfRunning()
    {
        if (await ComponentTasksManager.Instance.IsTaskRunning(ComponentId))
            await ComponentTasksManager.Instance.RestartTask(ComponentId);
    }

    private ComponentPresetListSubscription _presetListSubscription;
    private bool _updatingConfigList;

    private void PresetPage_Loaded(object sender, RoutedEventArgs e)
    {
        _presetListSubscription?.Dispose();
        _presetListSubscription = new(ComponentId, DispatcherQueue, ApplyPresetItems);
    }

    private void PresetPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _presetListSubscription?.Dispose();
        _presetListSubscription = null;
    }

    private void ApplyPresetItems(IReadOnlyList<ConfigSelectorItem> items)
    {
        _updatingConfigList = true;
        try
        {
            _configItems.Clear();
            foreach (var item in items)
                _configItems.Add(item);
            ToggleVisibility(_configItems.Count > 0);
        }
        finally { _updatingConfigList = false; }
        ApplySavedSelection();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        PresetPage_Unloaded(this, null);
    }

    private async void ConfigChooseCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingConfigList || !IsLoaded)
            return;
        if (ConfigChooseCombobox.SelectedItem is ConfigSelectorItem sel)
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
                    CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(
                        nameof(DefaultComponentSettingsPage),
                        $"Cannot prepare selected config '{sel.PackId}/{sel.FileName}': {ex}");
                }
            }

            if ((oldCfg != sel.FileName || oldId != sel.PackId) && await ComponentTasksManager.Instance.IsTaskRunning(ComponentId))
                await ComponentTasksManager.Instance.RestartTask(ComponentId);

            _ = Task.Run(() => InitSettingsTiles(sel));
        }
        _showAnim = true;

    }

    private void ApplySavedSelection()
    {
        var savedFile = SettingsManager.Instance.GetValue<string>(["CONFIGS", ComponentId], "configFile");
        var savedPackId = SettingsManager.Instance.GetValue<string>(["CONFIGS", ComponentId], "configId");

        if (string.IsNullOrEmpty(savedFile) || string.IsNullOrEmpty(savedPackId))
            return;

        var match = _configItems
            .FirstOrDefault(ci => ci.FileName == savedFile
                               && ci.PackId == savedPackId);
        if (match != null)
            ConfigChooseCombobox.SelectedItem = match;
    }

    private void StaggeredRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (!(args.Element is UIElement element))
            return;

        var visual = ElementCompositionPreview.GetElementVisual(element);
        var compositor = visual.Compositor;

        ElementCompositionPreview.SetIsTranslationEnabled(element, true);

        if (_showAnim)
            visual.Properties.InsertVector3("Translation", new Vector3(0, 50, 0));

        visual.Opacity = 0f;

        element.DispatcherQueue.TryEnqueue(
            DispatcherQueuePriority.Normal,
            () =>
            {
                var translationAnim = compositor.CreateVector3KeyFrameAnimation();
                translationAnim.Target = "Translation";
                translationAnim.InsertKeyFrame(0f, new Vector3(0, 50, 0));
                translationAnim.InsertKeyFrame(1f, Vector3.Zero);
                translationAnim.Duration = TimeSpan.FromMilliseconds(300);

                if (_showAnim)
                    visual.StartAnimation("Translation", translationAnim);


                var fadeAnim = compositor.CreateScalarKeyFrameAnimation();
                fadeAnim.Target = "Opacity";
                if (_showAnim)
                    fadeAnim.InsertKeyFrame(0f, 0f);
                else
                    fadeAnim.InsertKeyFrame(0.5f, 0.5f);
                fadeAnim.InsertKeyFrame(1f, 1f);
                fadeAnim.Duration = TimeSpan.FromMilliseconds(_showAnim ? 300 : 150);

                visual.StartAnimation("Opacity", fadeAnim);


            });
    }

    private void ToggleVisibility(bool visible)
    {
        ToggleLoading(false);
        if (!visible)
        {
            MainPanel.Visibility = Visibility.Collapsed;
            EmptyPageGrid.Visibility = Visibility.Visible;
        }
        else
        {
            MainPanel.Visibility = Visibility.Visible;
            EmptyPageGrid.Visibility = Visibility.Collapsed;
        }
    }

    private void ToggleLoading(bool isLoading)
    {
        LoadingGrid.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ComponentHelpfulLinksUserControl_ConfigEditRequested(object sender, RoutedEventArgs e)
    {
        ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(ComponentId);
        ConfigurationService service = componentHelper.GetConfigHelper();
        if (ConfigChooseCombobox.SelectedItem is ConfigSelectorItem item)
            _ = ConfigMakerWindow.EditConfig(service.GetConfigItem(item.FileName, item.PackId)); // EditConfig catches and logs its own errors.
    }
}
