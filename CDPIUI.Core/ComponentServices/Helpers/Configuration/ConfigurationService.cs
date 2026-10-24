using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.ComponentServices.Helpers.Configuration.Converters;
using CDPIUI.Core.ComponentServices.Helpers.Configuration.Helpers;
using CDPIUI.Core.Data;
using CDPIUI.Core.JSON;
using CDPIUI.Core.Store.Database;

namespace CDPIUI.Core.ComponentServices.Configuration;

/// <summary>
/// The presets of the installed config packs for one component, and the operations on them.
/// </summary>
public partial class ConfigurationService
{
    private readonly List<ConfigItem> _items = [];

    private string _target;

    private List<Tuple<string, LocaleModel>> _configLocaleHelpers = [];

    private readonly object _lock = new object();
    public ConfigurationService(string target)
    {
        _target = target;
    }

    /// <inheritdoc cref="PresetFileLoader.LoadPresetForTarget"/>
    internal static ConfigItem? LoadPresetForTarget(string jsonFile, string target, out bool isLegacyZapretConfig) =>
        PresetFileLoader.LoadPresetForTarget(jsonFile, target, out isLegacyZapretConfig);

    public static ConfigItem? LoadConfigItemFromPack(
        string filename,
        string packId,
        string target) =>
        PresetFileLoader.LoadConfigItemFromPack(filename, packId, target);

    public void Init(string itemId = "")
    {
        lock (_lock)
        {
            _items.Clear();
            _configLocaleHelpers.Clear();
            List<DatabaseStoreItem> configItems =
                string.IsNullOrEmpty(itemId) ?
                DatabaseHelper.Instance.GetItemsByType("configlist") :
                DatabaseHelper.Instance.GetItemById(itemId) is { } configItem ? [configItem] : [];

            List<DatabaseStoreItem> itemsToCheck = new List<DatabaseStoreItem>();

            foreach (DatabaseStoreItem item in configItems)
            {
                if (!Path.Exists(item?.Directory))
                    continue;

                itemsToCheck.Add(item);
            }

            foreach (DatabaseStoreItem item in itemsToCheck)
            {
                _items.AddRange(ConfigPackDirectoryLoader.Load(item, _target, _configLocaleHelpers));
            }
        }
    }

    private ConfigItem? FindItem(string? filename, string? packId) =>
        _items.FirstOrDefault(
            x => string.Equals(x.packId, packId, StringComparison.Ordinal) &&
                 string.Equals(x.file_name, filename, StringComparison.Ordinal));

    public List<ConfigItem> GetConfigItems()
    {
        lock (_lock)
        {
            return [.. _items];
        }
    }

    public ConfigItem? GetConfigItem(string filename, string packId)
    {
        lock (_lock)
        {
            return FindItem(filename, packId);
        }
    }

    public void SetConfigItem(ConfigItem newConfigItem)
    {
        lock (_lock)
        {
            var configItem = FindItem(newConfigItem.file_name, newConfigItem.packId);
            if (configItem != null)
                _items.Remove(configItem);

            _items.Add(newConfigItem);
        }
    }

    public void ChangeVariableValue(string filename, string packId, string key, bool value)
    {
        var configItem = FindItem(filename, packId);

        if (configItem == null)
            return;

        configItem.jparams[key] = value;

        _ = SaveConfigItem(filename, packId, configItem);
    }

    public void ChangeCommaVariableValue(string filename, string packId, string key, string value)
    {
        var configItem = FindItem(filename, packId);

        if (configItem?.commaVars == null)
            return;

        string? actualKey = configItem.commaVars.Keys.FirstOrDefault(
            candidate => string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase));
        AvailableVarValues? availableValues = configItem.availableCommaVarsValues?.FirstOrDefault(
            item => string.Equals(item.VarName, key, StringComparison.OrdinalIgnoreCase));
        if (actualKey == null)
            return;

        configItem.commaVars[actualKey] = value;
        if (availableValues != null)
        {
            availableValues.CurrentValueIndex = availableValues.Values?.FindIndex(
                candidate => string.Equals(candidate, value, StringComparison.Ordinal)) ?? -1;
        }
        _ = SaveConfigItem(filename, packId, configItem);
    }

    public static async Task<string> SaveConfigItem(string filename, string packId, ConfigItem item)
    {
        string folder = GetItemFolderFromPackId(packId);
        string fileName = Path.Combine(folder, filename);

        // The file keeps the name as written by its author ($LOADDYNAMIC... keys), while the cached
        // item keeps showing the localized name.
        string? displayName = item.name;
        string jsonString;
        try
        {
            item.name = item.not_converted_name ?? item.name;
            jsonString = JSONConvertor.SerializeObject(item);
        }
        finally
        {
            item.name = displayName;
        }

        Logger.Instance.CreateDebugLog(nameof(ConfigurationService), jsonString);
        try
        {
            CDPIUI.Shared.Basic.Filesystem.FileSystemService.WriteAllTextAtomic(fileName, jsonString);
        }
        catch (Exception ex)
        {
            return ErrorsHelper.Convertor.GetPrettyErrorCode("SAVE_CFG", ex);
        }

        await Task.CompletedTask;
        return string.Empty;
    }

    public static string GetDefaultLocalePath(string packId) =>
        ConfigPackDirectoryLoader.GetDefaultLocalePath(GetItemFolderFromPackId(packId));

    public string GetLocalizedConfigVarName(string name, string packId)
    {
        try
        {
            foreach (Tuple<string, LocaleModel> localeHelperTuple in _configLocaleHelpers)
            {
                if (localeHelperTuple.Item1 == packId)
                {
                    if (!localeHelperTuple.Item2.keyValuePairs.ContainsKey(name))
                        return $"Toggle \"{name}\"";
                    return localeHelperTuple.Item2.keyValuePairs[name];
                }
            }
        }
        catch (Exception ex)
        {
            // A missing translation is not an error for the user; the default name is shown.
            Basic.Logger.Instance.CreateDebugLog(nameof(ConfigurationService), $"Cannot get the localized name of {name}: {ex.Message}");
        }

        return $"Toggle \"{name}\"";
    }

    internal const int MaxVariablePasses = ConfigVariableExpander.MaxVariablePasses;
    internal const int MaxExpandedLength = ConfigVariableExpander.MaxExpandedLength;

    public static Dictionary<string, string>? GetReadyToUseVariables(
        string? id,
        List<string>? variables,
        Dictionary<string, bool>? jparams) =>
        ConfigVariableExpander.GetReadyToUseVariables(id, variables, jparams);

    public static string ReplaceVariables(string input, IDictionary<string, string>? readyToUseVars) =>
        ConfigVariableExpander.ReplaceVariables(input, readyToUseVars);

    public static string ReplaceCommaVariables(string startupString, Dictionary<string, string>? commaVars) =>
        ConfigVariableExpander.ReplaceCommaVariables(startupString, commaVars);

    /// <summary>
    /// Gets startup sting of <see cref="ConfigItem"/> after
    /// variables and paths check.
    /// </summary>
    /// <param name="item">Target item</param>
    /// <param name="forceRebuild">
    /// Force rebuild config if component required hidden conversion.<br></br>
    /// If <see cref="false"/>, rebuild called only if user sets
    /// "ZAPRET2/validateLegacyCacheHashes" as <see cref="true"/><br></br>
    /// If <paramref name="item"/> target not required conversion this property
    /// ignores.
    /// </param>
    /// <returns>Component startup string for item</returns>
    public static string GetStartupParametersByConfigItem(ConfigItem item, bool forceRebuild = false)
    {
        Dictionary<string, bool>? jparams = item.jparams;
        List<string>? variables = item.variables;
        string? startupString = item.startup_string;

        if (jparams == null || variables == null || startupString == null)
            return string.Empty;

        startupString = ConfigVariableExpander.ExpandStartupString(
            startupString, item.packId, variables, jparams, item.commaVars);

        if (!item.IsLegacy)
        {
            return startupString;
        }

        bool validateHashes = SettingsManager.Instance.GetValueOrDefault(
            Zapret2LegacyConfigService.HashValidationSettingsGroup,
            Zapret2LegacyConfigService.HashValidationSettingsKey,
            defaultValue: Zapret2LegacyConfigService.DefaultHashValidationValue);

        return Zapret2LegacyConfigService.GetStartupString(
            item,
            startupString,
            validateHashes,
            forceRebuild);
    }

    public string GetStartupParameters(string filename, string packId)
    {
        ConfigItem? item = FindItem(filename, packId);
        if (item?.startup_string == null)
            return string.Empty;

        return ConfigVariableExpander.ExpandStartupString(
            item.startup_string, packId, item.variables, item.jparams, item.commaVars);
    }

    public static List<VariableItem> GetVariables(ConfigItem configItem) =>
        ToVariableItems(configItem.jparams!);

    public List<VariableItem> GetVariables(string filename, string packId)
    {
        var configItem = FindItem(filename, packId);

        if (configItem?.jparams == null)
            return [];

        return ToVariableItems(configItem.jparams);
    }

    private static List<VariableItem> ToVariableItems(Dictionary<string, bool> jparams)
    {
        List<VariableItem> variables = [];

        foreach (var variable in jparams)
        {
            VariableItem variableItem = new()
            {
                name = variable.Key,
                value = variable.Value,
            };
            variables.Add(variableItem);
        }

        return variables;
    }

    public List<CommaVariableItem> GetCommaVariables(string filename, string packId)
    {
        var configItem = FindItem(filename, packId);
        if (configItem?.commaVars == null)
            return [];

        var result = new List<CommaVariableItem>();
        foreach (var commaVariable in configItem.commaVars)
        {
            AvailableVarValues? availableValues = configItem.availableCommaVarsValues?.FirstOrDefault(
                item => string.Equals(item.VarName, commaVariable.Key, StringComparison.OrdinalIgnoreCase));
            List<string> values = (availableValues?.Values ?? [])
                .Distinct(StringComparer.Ordinal)
                .ToList();

            result.Add(new CommaVariableItem
            {
                name = commaVariable.Key,
                comment = availableValues?.Comment,
                value = commaVariable.Value,
                values = values,
            });
        }

        return result;
    }

    public List<string> GetToggleLists(string filename, string packId)
    {
        var configItem = FindItem(filename, packId);

        // The callers read Count: a preset without toggle lists gives an empty list.
        return configItem?.toggle_lists ?? [];
    }

    public async Task<List<SiteListItem>> GetExcludedSiteListItemsAsync(
        string filename,
        string packId,
        bool unique = true,
        bool ignoreNull = false)
    {
        await Task.CompletedTask;
        return GetExcludedSiteListItems(filename, packId, unique, ignoreNull);
    }

    public List<SiteListItem> GetExcludedSiteListItems(
        string filename,
        string packId,
        bool unique = true,
        bool ignoreNull = false)
    {
        GetAllWindowsInConfig(filename, packId, out var localItemFolder, out var windows);
        return SiteListParser.GetExcludedLists(windows, localItemFolder, unique, ignoreNull);
    }

    public async Task<List<SiteListItem>> GetSiteListItemsAsync(
        string filename,
        string packId,
        bool unique = true,
        bool ignoreNull = false)
    {
        await Task.CompletedTask;
        return GetSiteListItems(filename, packId, unique, ignoreNull);
    }

    public List<SiteListItem> GetSiteListItems(
        string filename,
        string packId,
        bool unique = true,
        bool ignoreNull = false)
    {
        GetAllWindowsInConfig(filename, packId, out var localItemFolder, out var windows);
        return SiteListParser.GetIncludedLists(windows, localItemFolder, unique, ignoreNull);
    }

    private void GetAllWindowsInConfig(
        string filename,
        string packId,
        out string localItemFolder,
        out List<string> windows)
    {
        localItemFolder = GetItemFolderFromPackId(packId);
        string startupString = GetStartupParameters(filename, packId);
        Logger.Instance.CreateDebugLog(nameof(ConfigurationService), $"{startupString}");

        windows = SiteListParser.SplitProfiles(startupString);
    }

    public static string GetItemFolderFromPackId(string packId)
    {
        string localItemFolder = Path.Combine(Directories.StoreItemsDirectory, packId);
        return localItemFolder;
    }

    // The item is null when a .json file holds "null".
    public static Tuple<ConfigItem?, bool>? LoadConfigFromFile(string filepath) =>
        ConfigFileImporter.Load(filepath);

    public static List<string> GetUsedFilesFromConfigItem(ConfigItem configItem) =>
        configItem.UsedFiles
            .Select(file => file.ExpandedPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static ConfigItem ReplaceFilesPath(ConfigItem configItem, Dictionary<string, string> files) =>
        ConfigFilePathReplacer.ReplaceFilesPath(configItem, files);

    public string RemoveConfig(string filename, string packId, bool removeFile)
    {
        string folder = GetItemFolderFromPackId(packId);
        string packPath = Path.Combine(folder, filename);

        var item = FindItem(filename, packId);
        if (item != null)
        {
            if (removeFile)
            {
                _items.Remove(item);
            }
            else
            {
                item.MarkAsRemoved = true;
            }
        }

        if (removeFile)
        {
            try
            {
                File.Delete(packPath);
            }
            catch (Exception ex)
            {
                return ErrorsHelper.Convertor.GetPrettyErrorCode("SAVE_CFG", ex);
            }
        }
        return string.Empty;
    }

    public static string GetLastEditTimeFromConfigFile(string filename, string packId)
    {
        string filePath = Path.Combine(GetItemFolderFromPackId(packId), filename);

        try
        {
            DateTime lastEditTime = File.GetLastWriteTime(filePath);
            return lastEditTime.ToString();
        }
        catch
        {
            return "01.12.1970";
        }
    }
}
