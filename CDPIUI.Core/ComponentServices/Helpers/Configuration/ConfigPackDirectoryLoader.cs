using System.Text.RegularExpressions;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers.Configuration.Helpers;
using CDPIUI.Core.JSON;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.Repository.Localization;
using CDPIUI.Shared.Extensions;

namespace CDPIUI.Core.ComponentServices.Helpers.Configuration;

/// <summary>
/// Reads the presets of one installed config pack for one component, with their names
/// localized by the strings of the pack.
/// </summary>
internal static class ConfigPackDirectoryLoader
{
    /// <summary>
    /// Returns the presets of <paramref name="item"/> for <paramref name="target"/>, sorted by
    /// name. The locale strings of the pack are added to <paramref name="localeHelpers"/>.
    /// </summary>
    public static List<ConfigItem> Load(
        DatabaseStoreItem item,
        string target,
        List<Tuple<string, LocaleModel>> localeHelpers)
    {
        string? directory = item.Directory;
        string? id = item.Id;

        if (string.IsNullOrEmpty(id) || directory == null)
            return [];

        List<ConfigItem> configItems = new List<ConfigItem>();

        string initFile = Path.Combine(directory, "init.json");

        if (!File.Exists(initFile))
            return configItems;

        // A damaged pack must not hide the presets of the other packs (issue #61).
        ConfigInitItem? configInitItem;
        try
        {
            configInitItem = JSONConvertor.LoadJson<ConfigInitItem>(initFile);
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(ConfigurationService), $"Cannot read \"{initFile}\": {ex.Message}");
            return configItems;
        }
        if (configInitItem == null)
            return configItems;

        string[] jsonFiles = Directory.GetFiles(directory, "*.json");

        LocaleModel localeModel = new LocaleModel();
        localeHelpers.Add(Tuple.Create(id, localeModel));
        ConfigLocalizationHelper.InitLocaleHelper(
            localeModel,
            configInitItem.localized_strings_directory!,
            directory,
            StoreLocalizationHelper.GetStoreLikeLocale());

        foreach (string jsonFile in jsonFiles)
        {
            // Only the pack description: presets such as "initial-*.json" were skipped too.
            if (Path.GetFileName(jsonFile).Equals("init.json", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                ConfigItem? configItem = PresetFileLoader.LoadPresetForTarget(jsonFile, target, out bool isLegacyZapretConfig);
                if (configItem == null)
                    continue;

                configItem.IsLegacy = isLegacyZapretConfig;

                string localizedName = LocalizePresetName(
                    configItem.name ?? Path.GetFileNameWithoutExtension(jsonFile),
                    configInitItem,
                    directory,
                    localeModel);

                configItem.not_converted_name = configItem.name;
                configItem.name = localizedName;
                configItem.file_name = Path.GetFileName(jsonFile);
                configItem.packId = id;
                configItem.toggle_lists = configInitItem.toggleListAvailable;

                configItems.Add(configItem);
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateWarningLog(nameof(ConfigurationService), $"Error happens: {ex}");
            }
        }
        ConfigItem[] array = [.. configItems];
        Array.Sort(array, new LogicalComparer());

        return [.. array];
    }

    // A preset without a name keeps its file name; Regex.Replace threw and the
    // preset was left out of the list.
    private static string LocalizePresetName(
        string name,
        ConfigInitItem configInitItem,
        string directory,
        LocaleModel localeModel)
    {
        return Regex.Replace(
            name,
            // Only $NAME(key) is a script: a preset named "Fast $5 (beta)" stays as it is.
            @"\$[A-Za-z_]+\((.*?)\)",
            match =>
            {
                var key = match.Groups[1].Value;
                if (configInitItem.localized_strings_directory == null)
                    return key;

                var localized = ConfigLocalizationHelper.GetLocalizedConfigNameString(
                    key,
                    StoreLocalizationHelper.GetStoreLikeLocale(),
                    directory,
                    configInitItem.localized_strings_directory,
                    localeModel);

                return localized;
            }
        );
    }
}
