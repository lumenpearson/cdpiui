using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Input;
using CDPIUI.Controls.ComponentSettings;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Dialogs.ComponentSettings;
using CDPIUI.Controls.MainPage;
using CDPIUI.Controls.Universal;
using CDPIUI.Core;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.JSON;
using CDPIUI.Core.Store.Database;
using CDPIUI.Extensions;
using CDPIUI.Helper;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Helper.TgWsProxy;
using CDPIUI.Helper.UserExperience;
using CDPIUI.Shared;
using CDPIUI.ViewModels;
using CDPIUI.Views.CreateConfigHelper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
using WinRT.Interop;
using WinUI3Localizer;
using static System.Net.Mime.MediaTypeNames;
using Application = Microsoft.UI.Xaml.Application;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Views.Main.Components;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class TgWsProxyComponentPage : TemplatePage
{
    private readonly string _defaultTgWsProxyStartString = TgWsProxyStartupArguments.CreateDefault();


    private readonly ObservableCollection<ViewTelegramDataCenterModel> _telegramDataCenterModels = [];
    private readonly ObservableCollection<ConfigSelectorItem> _configItems = [];

    public ICommand TgWsProxyEditCommand { get; }
    public ICommand TgWsProxyRemoveCommand { get; }

    public ICommand LinkedSettingClickCommand { get; }

    private ILocalizer _localizer = Localizer.Get();

    public string ComponentId { get; private set; } = string.Empty;
    private ComponentPageNavigationModel _model;

    public readonly ObservableCollection<SettingLinkModel> LinkedSettings = [];

    public ICommand CreateFromTemplateCommand { get; }

    public TgWsProxyComponentPage()
    {
        InitializeComponent();
        this.DataContext = this;

        ConfigChooseCombobox.ItemsSource = _configItems;
        Loaded += PresetPage_Loaded;
        Unloaded += PresetPage_Unloaded;

        // EditElement and LinkedSettingClicked catch and log their own errors.
        TgWsProxyEditCommand = new RelayCommand(p => _ = EditElement((string)p));
        TgWsProxyRemoveCommand = new RelayCommand(p => RemoveElement((string)p));
        LinkedSettingClickCommand = new RelayCommand(p => _ = LinkedSettingClicked(p));

        DataCentersListView.ItemsSource = _telegramDataCenterModels;
        LinkedSettingsUserControl.SettingModelsList = LinkedSettings;

        CreateFromTemplateCommand = new RelayCommand(p => HandleCreatePlCommand((AvailableConfigCreationActios)p));

        HideInfoBar();
        LoadLinks();
    }

    private void LoadLinks()
    {
        LinkedSettingsHelper.LoadLinksForPage(LinkedSettingsHelper.Pages.TgWsProxyPage, LinkedSettings);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter != null && e.Parameter is ComponentPageNavigationModel model)
        {

            _model = model;
            ComponentId = _model.Id;
            EmptyPlaceholder.StoreId = ComponentId;
        }

    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        PresetPage_Unloaded(this, null);
    }

    private void ToggleVisibility(bool visible)
    {
        if (!visible)
        {
            ContentStackPanel.Visibility = Visibility.Collapsed;
            EmptyPlaceholder.Visibility = Visibility.Visible;
        }
        else
        {
            ContentStackPanel.Visibility = Visibility.Visible;
            EmptyPlaceholder.Visibility = Visibility.Collapsed;
        }
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

    private void InitSettings(ConfigSelectorItem configItem)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _telegramDataCenterModels.Clear();
        });
        if (configItem == null)
            return;
        ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(ComponentId);

        string startupString = componentHelper.GetConfigHelper().GetStartupParameters(configItem.FileName, configItem.PackId);

        TgWsProxyStartupValues values = TgWsProxyStartupArguments.Parse(startupString);

        DispatcherQueue.TryEnqueue(() => ApplyStartupValues(values));
    }

    private void ApplyStartupValues(TgWsProxyStartupValues values)
    {
        AdvancedLoggingToggleSwitch.IsOn = values.Verbose;
        if (values.Options.TryGetValue(TgWsProxyStartupArguments.PortOption, out string port))
            PortValue.Text = port;
        if (values.Options.TryGetValue(TgWsProxyStartupArguments.HostOption, out string host))
            IPValue.Text = host;
        if (values.Options.TryGetValue(TgWsProxyStartupArguments.SecretOption, out string secret))
            KeyValue.Text = secret;
        if (values.Options.TryGetValue(TgWsProxyStartupArguments.BufferSizeOption, out string bufferSize))
            BufferSizeTextBox.Text = bufferSize;
        if (values.Options.TryGetValue(TgWsProxyStartupArguments.PoolSizeOption, out string poolSize))
            WebSocketSizeTextBox.Text = poolSize;
        if (values.Options.TryGetValue(TgWsProxyStartupArguments.LogMaxSizeOption, out string logSize))
            LogSizeTextBox.Text = logSize;
        foreach (TgWsProxyDataCenter dataCenter in values.DataCenters)
        {
            _telegramDataCenterModels.Add(new()
            {
                Guid = dataCenter.Guid,
                Number = dataCenter.Number,
                Ip = dataCenter.Ip,
            });
        }
    }

    private void ShowInfoBar(string title, InfoBarSeverity severity)
    {
        HideInfoBar();
        ActionInfoBar.IsOpen = true;
        ActionInfoBar.Title = title;
        ActionInfoBar.Severity = severity;
        HideInfoBarAnimation.Stop();
        ShowInfoBarAnimation.Begin();
    }

    private void HideInfoBar()
    {
        ShowInfoBarAnimation.Stop();
        HideInfoBarAnimation.Begin();
    }

    private void ActionInfoBar_Closing(InfoBar sender, InfoBarClosingEventArgs args)
    {
        HideInfoBar();
        args.Cancel = true;
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        IPValue.Text = "127.0.0.1";
        PortValue.Text = "1080";
    }

    private void ResetKeyButton_Click(object sender, RoutedEventArgs e)
    {
        KeyValue.Text = $"{SharedUtils.GetRandomHexNumber(32)}";
    }

    private void ResetWebSocketSizeButton_Click(object sender, RoutedEventArgs e)
    {
        WebSocketSizeTextBox.Text = "4";
    }

    private void ResetBufferButton_Click(object sender, RoutedEventArgs e)
    {
        BufferSizeTextBox.Text = "256";
    }

    private void ResetLogSizeButton_Click(object sender, RoutedEventArgs e)
    {
        LogSizeTextBox.Text = "5";
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

            if ((oldCfg != sel.FileName || oldId != sel.PackId) && await ComponentTasksManager.Instance.IsTaskRunning(ComponentId))
                await ComponentTasksManager.Instance.RestartTask(ComponentId);

            _ = Task.Run(() => InitSettings(sel));
        }

        if (ConfigChooseCombobox.SelectedItem is null)
        {
            TilesStackPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            TilesStackPanel.Visibility = Visibility.Visible;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _model?.GoBackSignal?.Invoke(_model);
    }
}
