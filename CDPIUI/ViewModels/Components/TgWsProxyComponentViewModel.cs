using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CDPIUI.Controls.ComponentSettings;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Helper;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Helper.TgWsProxy;
using CDPIUI.Helper.UserExperience;
using CDPIUI.Shared;
using CDPIUI.Views.Main.Components;
using Microsoft.UI.Dispatching;
using WinUI3Localizer;

namespace CDPIUI.ViewModels.Components;

/// <summary>
/// State of the tg-ws-proxy settings page: the selected configuration, the proxy and
/// performance options and the data centers read from its startup string, and saving
/// them back.
/// </summary>
public sealed partial class TgWsProxyComponentViewModel : ObservableObject
{
    private const string LogSource = "TgWsProxyComponentPage";

    private readonly string _defaultTgWsProxyStartString = TgWsProxyStartupArguments.CreateDefault();
    private readonly ILocalizer _localizer = Localizer.Get();
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly ITgWsProxyComponentView _view;

    private ComponentPageNavigationModel _model;
    private ComponentPresetListSubscription _presetListSubscription;
    private bool _updatingConfigList;

    private ConfigSelectorItem _selectedConfig;
    private bool _isContentVisible;
    private bool _isEmptyPlaceholderVisible = true;
    private bool _isTilesVisible;
    private string _host = string.Empty;
    private string _port = string.Empty;
    private string _secret = string.Empty;
    private string _bufferSize = string.Empty;
    private string _poolSize = string.Empty;
    private string _logSize = string.Empty;
    private bool _isVerbose;

    public TgWsProxyComponentViewModel(DispatcherQueue dispatcherQueue, ITgWsProxyComponentView view)
    {
        _dispatcherQueue = dispatcherQueue;
        _view = view;

        // The asynchronous command methods catch and log their own errors.
        EditDataCenterCommand = new RelayCommand(p => _ = EditDataCenterAsync((string)p));
        RemoveDataCenterCommand = new RelayCommand(p => RemoveDataCenter((string)p));
        AddDataCenterCommand = new RelayCommand(() => _ = AddDataCenterAsync());
        LinkedSettingClickCommand = new RelayCommand(p => _ = LinkedSettingClickedAsync(p));
        CreateFromTemplateCommand = new RelayCommand(p => HandleCreateFromTemplate((AvailableConfigCreationActios)p));
        SaveCommand = new RelayCommand(() => _ = SaveAsync());
        CancelCommand = new RelayCommand(() => _model?.GoBackSignal?.Invoke(_model));
        ResetAddressCommand = new RelayCommand(() =>
        {
            Host = "127.0.0.1";
            Port = "1080";
        });
        ResetSecretCommand = new RelayCommand(() => Secret = $"{SharedUtils.GetRandomHexNumber(32)}");
        ResetPoolSizeCommand = new RelayCommand(() => PoolSize = "4");
        ResetBufferSizeCommand = new RelayCommand(() => BufferSize = "256");
        ResetLogSizeCommand = new RelayCommand(() => LogSize = "5");

        LinkedSettingsHelper.LoadLinksForPage(LinkedSettingsHelper.Pages.TgWsProxyPage, LinkedSettings);
    }

    public ICommand EditDataCenterCommand { get; }

    public ICommand RemoveDataCenterCommand { get; }

    public ICommand AddDataCenterCommand { get; }

    public ICommand LinkedSettingClickCommand { get; }

    public ICommand CreateFromTemplateCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand ResetAddressCommand { get; }

    public ICommand ResetSecretCommand { get; }

    public ICommand ResetPoolSizeCommand { get; }

    public ICommand ResetBufferSizeCommand { get; }

    public ICommand ResetLogSizeCommand { get; }

    public ObservableCollection<ViewTelegramDataCenterModel> DataCenters { get; } = [];

    public ObservableCollection<ConfigSelectorItem> ConfigItems { get; } = [];

    public ObservableCollection<SettingLinkModel> LinkedSettings { get; } = [];

    public string ComponentId { get; private set; } = string.Empty;

    public ConfigSelectorItem SelectedConfig
    {
        get => _selectedConfig;
        private set => SetProperty(ref _selectedConfig, value);
    }

    public bool IsContentVisible
    {
        get => _isContentVisible;
        private set => SetProperty(ref _isContentVisible, value);
    }

    public bool IsEmptyPlaceholderVisible
    {
        get => _isEmptyPlaceholderVisible;
        private set => SetProperty(ref _isEmptyPlaceholderVisible, value);
    }

    public bool IsTilesVisible
    {
        get => _isTilesVisible;
        private set => SetProperty(ref _isTilesVisible, value);
    }

    public string Host
    {
        get => _host;
        set => SetProperty(ref _host, value);
    }

    public string Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    public string Secret
    {
        get => _secret;
        set => SetProperty(ref _secret, value);
    }

    public string BufferSize
    {
        get => _bufferSize;
        set => SetProperty(ref _bufferSize, value);
    }

    public string PoolSize
    {
        get => _poolSize;
        set => SetProperty(ref _poolSize, value);
    }

    public string LogSize
    {
        get => _logSize;
        set => SetProperty(ref _logSize, value);
    }

    public bool IsVerbose
    {
        get => _isVerbose;
        set => SetProperty(ref _isVerbose, value);
    }

    public void SetNavigationModel(ComponentPageNavigationModel model)
    {
        _model = model;
        ComponentId = _model.Id;
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

            if ((oldCfg != sel.FileName || oldId != sel.PackId) && await ComponentTasksManager.Instance.IsTaskRunning(ComponentId))
                await ComponentTasksManager.Instance.RestartTask(ComponentId);

            _ = Task.Run(() => InitSettings(sel));
        }

        IsTilesVisible = _selectedConfig is not null;
    }

    /// <summary>Adds the data centers found in a text, or updates the address of known numbers.</summary>
    public bool PasteDataCenters(string text)
    {
        var dataCenters = TgWsProxyStartupArguments.FindDataCenters(text);
        if (dataCenters.Count == 0)
            return false;

        foreach (var (number, ip) in dataCenters)
        {
            var existItem = DataCenters.FirstOrDefault(x => x.Number == number);
            if (existItem != null)
            {
                existItem.Ip = ip;
            }
            else
            {
                DataCenters.Add(new()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Number = number,
                    Ip = ip
                });
            }
        }
        return true;
    }

    /// <summary>The data centers as "number:address" lines.</summary>
    public string BuildShareText()
    {
        string targetText = string.Empty;
        foreach (var model in DataCenters)
        {
            targetText += $"{model.Number}:{model.Ip}\n";
        }
        return targetText;
    }

    private void ApplyPresetItems(IReadOnlyList<ConfigSelectorItem> items)
    {
        _updatingConfigList = true;
        try
        {
            ConfigItems.Clear();
            foreach (var item in items)
                ConfigItems.Add(item);
            bool hasItems = ConfigItems.Count > 0;
            IsContentVisible = hasItems;
            IsEmptyPlaceholderVisible = !hasItems;
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

        var match = ConfigItems.FirstOrDefault(ci => ci.FileName == savedFile && ci.PackId == savedPackId);
        if (match != null)
            SelectedConfig = match;
    }

    private void InitSettings(ConfigSelectorItem configItem)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            DataCenters.Clear();
        });
        if (configItem == null)
            return;
        ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(ComponentId);

        string startupString = componentHelper.GetConfigHelper().GetStartupParameters(configItem.FileName, configItem.PackId);

        TgWsProxyStartupValues values = TgWsProxyStartupArguments.Parse(startupString);

        _dispatcherQueue.TryEnqueue(() => ApplyStartupValues(values));
    }

    private void ApplyStartupValues(TgWsProxyStartupValues values)
    {
        IsVerbose = values.Verbose;
        if (values.Options.TryGetValue(TgWsProxyStartupArguments.PortOption, out string port))
            Port = port;
        if (values.Options.TryGetValue(TgWsProxyStartupArguments.HostOption, out string host))
            Host = host;
        if (values.Options.TryGetValue(TgWsProxyStartupArguments.SecretOption, out string secret))
            Secret = secret;
        if (values.Options.TryGetValue(TgWsProxyStartupArguments.BufferSizeOption, out string bufferSize))
            BufferSize = bufferSize;
        if (values.Options.TryGetValue(TgWsProxyStartupArguments.PoolSizeOption, out string poolSize))
            PoolSize = poolSize;
        if (values.Options.TryGetValue(TgWsProxyStartupArguments.LogMaxSizeOption, out string logSize))
            LogSize = logSize;
        foreach (TgWsProxyDataCenter dataCenter in values.DataCenters)
        {
            DataCenters.Add(new()
            {
                Guid = dataCenter.Guid,
                Number = dataCenter.Number,
                Ip = dataCenter.Ip,
            });
        }
    }

    private string GetStartupString(string startupString)
    {
        TgWsProxyStartupValues values = new();
        values.Options[TgWsProxyStartupArguments.PortOption] = Port;
        values.Options[TgWsProxyStartupArguments.HostOption] = Host;
        values.Options[TgWsProxyStartupArguments.SecretOption] = Secret;
        values.Options[TgWsProxyStartupArguments.BufferSizeOption] = BufferSize;
        values.Options[TgWsProxyStartupArguments.PoolSizeOption] = PoolSize;
        values.Options[TgWsProxyStartupArguments.LogMaxSizeOption] = LogSize;
        values.Verbose = IsVerbose;

        foreach (var item in DataCenters)
        {
            values.DataCenters.Add(new(item.Guid, item.Number, item.Ip));
        }
        return TgWsProxyStartupArguments.Build(startupString, values);
    }

    private async Task SaveAsync()
    {
        try
        {
            var sel = _selectedConfig;
            ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(ComponentId);

            var item = componentHelper.GetConfigHelper().GetConfigItem(sel.FileName, sel.PackId);

            item.startup_string = GetStartupString(item.startup_string);

            string errorCode = await ConfigurationService.SaveConfigItem(sel.FileName, sel.PackId, item);
            if (!string.IsNullOrEmpty(errorCode))
            {
                await _view.ShowErrorAsync(string.Format(_localizer.GetLocalizedString("SaveConfigException"), errorCode), _localizer.GetLocalizedString("SomethingWentWrong"));
                return;
            }

            componentHelper.ReInitConfigs();
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }
}
