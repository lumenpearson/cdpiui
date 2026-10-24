using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CDPIUI.Controls.Dialogs.CreateConfigHelper;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.System;
using CDPIUI.Controls.CreateConfigHelper;
using Microsoft.UI.Dispatching;
using WinUI3Localizer;

namespace CDPIUI.ViewModels.CreateConfig;

/// <summary>
/// The state of the config kit editor: the configs of the kit, renaming and removing them,
/// and saving the kit in place or as a new kit.
/// </summary>
public sealed partial class EditConfigKitViewModel : ObservableObject
{
    private const string LogSource = "EditConfigKitPage";

    private readonly IEditConfigKitView _view;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly ILocalizer _localizer = Localizer.Get();
    private readonly AsyncRelayCommand _saveAsCommand;

    private string _kitId = string.Empty;
    private ConfigurationService _configHelper;
    private bool _isSaveEnabled = true;

    public EditConfigKitViewModel(IEditConfigKitView view)
    {
        _view = view;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        RemoveConfigCommand = new RelayCommand(p => RemoveConfig((Tuple<string>)p));
        RenameConfigCommand = new RelayCommand(p => RenameConfig((Tuple<string, string>)p));
        EditConfigCommand = new RelayCommand(p => EditConfig((Tuple<string>)p));
        OpenDirectoryCommand = new RelayCommand(p => OpenConfigDirectory((Tuple<string, OpenFileDirectoryTypes>)p));
        _saveAsCommand = new AsyncRelayCommand(SaveKitAsAsync, () => IsSaveEnabled);
    }

    public ObservableCollection<ViewConfigInKitModel> Configs { get; } = [];

    public ICommand RemoveConfigCommand { get; }
    public ICommand RenameConfigCommand { get; }
    public ICommand EditConfigCommand { get; }
    public ICommand OpenDirectoryCommand { get; }
    public ICommand SaveAsCommand => _saveAsCommand;

    /// <summary>The save buttons are enabled.</summary>
    public bool IsSaveEnabled
    {
        get => _isSaveEnabled;
        private set
        {
            if (SetProperty(ref _isSaveEnabled, value))
                _saveAsCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Enables or disables the save buttons, as the save confirmation flyout does.</summary>
    public void SetSaveEnabled(bool value) => IsSaveEnabled = value;

    /// <summary>Loads the configs of the kit, unless the kit is already loaded.</summary>
    public async Task LoadAsync(string kitId)
    {
        try
        {
            if (kitId != _kitId)
            {
                _kitId = kitId;
                await Task.Run(() => LoadConfigItems());
            }
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    /// <summary>Forgets the loaded kit.</summary>
    public void Clear()
    {
        Configs.Clear();
        _kitId = string.Empty;
    }

    /// <summary>Takes the config the editor page returned.</summary>
    public void ReplaceConfig(ConfigItem newItem)
    {
        var item = Configs.FirstOrDefault(x => x.FileName == newItem.file_name && x.PackId == newItem.packId);

        if (item != null)
        {
            item.DisplayName = string.IsNullOrEmpty(newItem.name) ? newItem.not_converted_name : newItem.name;
            item.LastEditTime = DateTime.Now.ToString();
            _configHelper.SetConfigItem(newItem);
        }
    }

    private async Task LoadConfigItems()
    {
        ToggleLoadingMode(true, _localizer.GetLocalizedString("LoadingConfigs"));

        _dispatcherQueue.TryEnqueue(() => Configs.Clear());
        _configHelper = new(target: "$ANY");
        _configHelper.Init(_kitId);

        await Task.Delay(100);

        List<ConfigItem> items = _configHelper.GetConfigItems();

        foreach (ConfigItem item in items)
        {
            // target[0] threw on the UI thread for a config without a target.
            if (item.MarkAsRemoved || item.target == null || item.target.Count == 0)
                continue;
            List<string> usedSiteLists =
                [.. (await _configHelper.GetSiteListItemsAsync(item.file_name, item.packId, ignoreNull: true)).Select(x => Path.GetFileName(x.FilePath))];
            List<string> excludedSiteLists =
                [.. (await _configHelper.GetExcludedSiteListItemsAsync(item.file_name, item.packId, ignoreNull: true)).Select(x => Path.GetFileName(x.FilePath))];

            _dispatcherQueue.TryEnqueue(() =>
            {
                Configs.Add(new()
                {
                    Guid = Guid.NewGuid().ToString(),
                    PackId = item.packId,
                    FileName = item.file_name,

                    TargetComponentId = item.target[0],
                    DisplayName = item.name,
                    UsedSiteLists = usedSiteLists,
                    ExcludedSiteLists = excludedSiteLists,
                    LastEditTime = ConfigurationService.GetLastEditTimeFromConfigFile(item.file_name, item.packId),
                });
            });
        }
        ToggleLoadingMode(false);
    }

    private void ToggleLoadingMode(bool loadingMode, string text = "")
    {
        _dispatcherQueue.TryEnqueue(() => IsSaveEnabled = !loadingMode);
        _view.SetStatus(loadingMode, text);
    }

    private void RemoveConfig(Tuple<string> parameter)
    {
        var item = Configs.FirstOrDefault(x => x.Guid == parameter.Item1);

        if (item != null)
        {
            Configs.Remove(item);
            _configHelper.RemoveConfig(item.FileName, item.PackId, false);
        }
    }

    private void RenameConfig(Tuple<string, string> parameter)
    {
        var item = Configs.FirstOrDefault(x => x.Guid == parameter.Item1);

        if (item != null)
        {
            item.DisplayName = parameter.Item2;
            ConfigItem configItem = _configHelper.GetConfigItem(item.FileName, item.PackId);
            if (configItem != null && !configItem.MarkAsRemoved)
            {
                configItem.name = parameter.Item2;
            }
            _configHelper.SetConfigItem(configItem);
        }
    }

    private void EditConfig(Tuple<string> parameter)
    {
        var item = Configs.FirstOrDefault(x => x.Guid == parameter.Item1);

        if (item != null)
        {
            ConfigItem configItem = _configHelper.GetConfigItem(item.FileName, item.PackId);
            var nvc = new NameValueCollection
            {
                { "type", "CFGRETURNEDITED" },
                { "configItem", Core.JSON.JSONConvertor.SerializeObject(configItem) }
            };
            _view.OpenConfigEditor(nvc);
        }
    }

    private void OpenConfigDirectory(Tuple<string, OpenFileDirectoryTypes> parameter)
    {
        var item = Configs.FirstOrDefault(x => x.Guid == parameter.Item1);

        if (item != null)
        {
            ShellHelper.LookupDirectory(ConfigurationService.GetItemFolderFromPackId(_kitId));
        }
    }

    /// <summary>Saves the kit in place and leaves the page.</summary>
    public async Task SaveKitAsync()
    {
        ToggleLoadingMode(true, _localizer.GetLocalizedString("SavingConfigKit"));
        List<ConfigItem> items = [];
        items.AddRange(_configHelper.GetConfigItems());
        for (int i = 0; i < items.Count; i++)
        {
            ConfigItem configItem = items[i];
            var item = Configs.FirstOrDefault(x => x.PackId == configItem.packId && x.FileName == configItem.file_name);
            string errorCode = item == null
                ? _configHelper.RemoveConfig(configItem.file_name, configItem.packId, true)
                : await ConfigurationService.SaveConfigItem(configItem.file_name, configItem.packId, configItem);
            if (!string.IsNullOrEmpty(errorCode))
            {
                // Not awaited: the buttons are enabled while the message is shown. ShowErrorAsync logs its own errors.
                _ = _view.ShowErrorAsync(string.Format(_localizer.GetLocalizedString("SaveConfigException"), errorCode), _localizer.GetLocalizedString("SomethingWentWrong"));
                IsSaveEnabled = true;
                return;
            }
        }

        ToggleLoadingMode(false);

        ComponentItemsLoaderHelper.Instance.Init(forse: true);

        _view.LeaveAfterSave();
    }

    private async Task SaveKitAsAsync()
    {
        ToggleLoadingMode(true, _localizer.GetLocalizedString("SavingConfigKit"));

        var result = await _view.AskSaveAsAsync(
            DatabaseHelper.Instance.GetItemById(_kitId)?.ShortName + $" ({_localizer.GetLocalizedString("Edited")})" ?? string.Empty);

        if (result.Mode == SaveConfigKitAsModes.None)
        {
            ToggleLoadingMode(false);
            return;
        }

        if (result.Mode == SaveConfigKitAsModes.SaveForMe)
        {
            string newPackId = $"{Guid.NewGuid()}_FM";
            string directory = ConfigurationService.GetItemFolderFromPackId(newPackId);

            ConfigPackMakeHelper.ConfigPackInitModel templateModel = new()
            {
                StoreId = newPackId,
                ShortName = result.DisplayName,
                Name = result.DisplayName,
                Developer = SettingsManager.Instance.GetValueOrDefault("CONFIGKIT", "lastUsedDevName", defaultValue: Environment.UserName),
            };

            var operationResult = await Task.Run(() =>
                ConfigPackMakeHelper.CreateConfigPack(templateModel, _configHelper.GetConfigItems(), directory, autoImport: true));

            if (operationResult?.Success != true)
            {
                await _view.ShowFileSaveErrorAsync(
                    string.Format(_localizer.GetLocalizedString("FileSaveErrorMessage"), directory, operationResult?.Error.ErrorCode),
                    operationResult?.Error.FriendlyDescription);
            }
        }
        else
        {
            string filename = _view.PickConfigPackFile($"{result.DisplayName}.cdpiconfigpack");
            if (filename != null)
            {
                ConfigPackMakeHelper.ConfigPackInitModel templateModel = new()
                {
                    StoreId = result.Id,
                    ShortName = result.DisplayName,
                    Name = result.DisplayName,
                    Version = result.Version,
                    Developer = result.Developer,
                    Color = result.HexAccentColor,
                    Icon = result.ImageSourceUri == new Uri("ms-appx:///Assets/Store/empty.png") ? $"$LOADSTATIC(Store/empty.png)" : result.ImageSourceUri.LocalPath
                };

                var operationResult = await Task.Run(() =>
                    ConfigPackMakeHelper.CreateConfigPack(templateModel, _configHelper.GetConfigItems(), filename));

                if (operationResult?.Success != true)
                {
                    await _view.ShowFileSaveErrorAsync(
                        string.Format(_localizer.GetLocalizedString("FileSaveErrorMessage"), filename, operationResult?.Error.ErrorCode),
                        operationResult?.Error.FriendlyDescription);
                }
            }
        }

        await Task.Delay(200);

        ToggleLoadingMode(false);
    }
}
