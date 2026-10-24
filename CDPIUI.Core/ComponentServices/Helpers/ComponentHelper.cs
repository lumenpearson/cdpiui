using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.ComponentServices.Helpers.Configuration.Converters;
using CDPIUI.Core.Data;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;

namespace CDPIUI.Core.ComponentServices.Helpers;

public class ComponentHelper
{
    public readonly string Id;

    private string? _executablePath;
    private string? Directory;
    private DatabaseStoreItem? _databaseStoreItem;

    private readonly Lazy<ConfigurationService> _configHelper;

    public Action? ConfigListUpdated;

    public ComponentHelper(string id)
        : this(DatabaseHelper.Instance.GetItemById(id), id)
    {
    }

    /// <summary>
    /// Uses a row that was already read, so a list of components does not query each one again.
    /// </summary>
    public ComponentHelper(DatabaseStoreItem item)
        : this(item, item.Id!)
    {
    }

    private ComponentHelper(DatabaseStoreItem? item, string id)
    {
        Id = id;

        _databaseStoreItem = item;

        Directory = _databaseStoreItem.Directory;
        _executablePath = Path.Combine(
            _databaseStoreItem.Directory!,
            _databaseStoreItem.Executable + ".exe");

        _configHelper = new Lazy<ConfigurationService>(
            CreateConfigHelper,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private ConfigurationService CreateConfigHelper()
    {
        EnsureConvertedZapretStorageRegistered();

        ConfigurationService configHelper = new(Id);
        configHelper.Init();
        return configHelper;
    }

    private void EnsureConvertedZapretStorageRegistered()
    {
        if (Id != HardcodedItemIds.ComponentIds[Components.Zapret2])
            return;

        try
        {
            Zapret2LegacyConfigService.EnsureStorageRegistered();
        }
        catch (Exception ex)
        {
            Basic.Logger.Instance.CreateWarningLog(
                nameof(ComponentHelper),
                $"Cannot initialize the converted Zapret config storage: {ex}");
        }
    }

    public void ReInitConfigs()
    {
        if (_configHelper.IsValueCreated)
            _configHelper.Value.Init();
        else
            _ = _configHelper.Value;

        ConfigListUpdated?.Invoke();
    }

    /// <summary>The executable name from the row this helper was built from.</summary>
    public string? ExecutableName => _databaseStoreItem?.Executable;

    public ConfigurationService GetConfigHelper()
    {
        return _configHelper.Value;
    }

    public string? GetExecutablePath()
    {
        if (File.Exists(_executablePath))
            return _executablePath;

        return TryGetNewPath();
    }

    public string? GetDirectory()
    {
        // A folder: File.Exists is always false for it.
        if (global::System.IO.Directory.Exists(Directory))
            return Directory;

        return TryGetNewDirectory();
    }

    public string GetStartupParams()
    {
        string configId =
            SettingsManager.Instance.GetValue<string>(["CONFIGS", Id], "configId");
        string configFile =
            SettingsManager.Instance.GetValue<string>(["CONFIGS", Id], "configFile");

        ConfigItem? config = _configHelper.IsValueCreated
            ? _configHelper.Value.GetConfigItem(configFile!, configId!)
            : null;

        config ??= ConfigurationService.LoadConfigItemFromPack(
            configFile!,
            configId!,
            Id);
        if (config == null)
        {
            return string.Empty;
        }

        if (config.IsLegacy)
            EnsureConvertedZapretStorageRegistered();

        string startupString = ConfigurationService.GetStartupParametersByConfigItem(config);
        return startupString;
    }

    public void PrepareSelectedConfig(string configFile, string configId)
    {
        if (Id != HardcodedItemIds.ComponentIds[Components.Zapret2])
        {
            return;
        }

        ConfigItem? config = _configHelper.Value.GetConfigItem(configFile, configId);
        if (config == null || !config.IsLegacy)
        {
            return;
        }

        string startupString = ConfigurationService
            .GetStartupParametersByConfigItem(config, forceRebuild: true);
    }

    private string? TryGetNewPath()
    {
        string localItemsFolder = Directories.StoreItemsDirectory;
        _executablePath = Path.Combine(
            localItemsFolder,
            Id,
            _databaseStoreItem.Executable + ".exe");

        return File.Exists(_executablePath) ? _executablePath : null;
    }
    private string? TryGetNewDirectory()
    {
        string localItemsFolder = Directories.StoreItemsDirectory;
        Directory = Path.Combine(localItemsFolder, Id);
        return Path.Exists(Directory) ? Directory : null;
    }
}
