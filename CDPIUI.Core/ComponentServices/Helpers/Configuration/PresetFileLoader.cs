using System.Collections.Concurrent;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.Data;
using CDPIUI.Core.JSON;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;

namespace CDPIUI.Core.ComponentServices.Helpers.Configuration;

/// <summary>
/// Reads single preset files and decides whether a preset is for a given component.
/// </summary>
internal static class PresetFileLoader
{
    private readonly record struct FileStamp(DateTime LastWriteTimeUtc, long Length);

    private readonly record struct CachedPresetTarget(FileStamp Stamp, string? Target);

    // The target of each preset file, shared by all components. An entry is used only while
    // the file keeps the same write time and size.
    private static readonly ConcurrentDictionary<string, CachedPresetTarget> PresetTargetCache =
        new(StringComparer.Ordinal);

    private static FileStamp GetFileStamp(string path)
    {
        FileInfo info = new(path);
        return new FileStamp(info.LastWriteTimeUtc, info.Length);
    }

    /// <summary>
    /// Reads a preset file when it is for <paramref name="target"/>, otherwise returns null.
    /// Every component reads the same packs, so a file already known to be for another
    /// component is skipped without parsing it again. A matching file is always parsed anew,
    /// because callers change the returned item.
    /// </summary>
    public static ConfigItem? LoadPresetForTarget(string jsonFile, string target, out bool isLegacyZapretConfig)
    {
        FileStamp stamp = GetFileStamp(jsonFile);
        if (PresetTargetCache.TryGetValue(jsonFile, out CachedPresetTarget cached) &&
            cached.Stamp == stamp &&
            !MatchesTarget(cached.Target, target, out _))
        {
            isLegacyZapretConfig = false;
            return null;
        }

        ConfigItem? configItem = JSONConvertor.LoadJson<ConfigItem>(jsonFile);
        if (configItem != null)
            PresetTargetCache[jsonFile] = new CachedPresetTarget(stamp, configItem.target?.FirstOrDefault());

        return MatchesTarget(configItem, target, out isLegacyZapretConfig) ? configItem : null;
    }

    public static ConfigItem? LoadConfigItemFromPack(
        string filename,
        string packId,
        string target)
    {
        if (string.IsNullOrWhiteSpace(filename) ||
            string.IsNullOrWhiteSpace(packId) ||
            string.IsNullOrWhiteSpace(target) ||
            !string.Equals(filename, Path.GetFileName(filename), StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            DatabaseStoreItem? pack = DatabaseHelper.Instance.GetItemById(packId);
            if (string.IsNullOrWhiteSpace(pack?.Directory))
                return null;

            string configPath = Path.Combine(pack.Directory, filename);
            if (!File.Exists(configPath))
                return null;

            ConfigItem? configItem = JSONConvertor.LoadJson<ConfigItem>(configPath);
            if (configItem == null ||
                !MatchesTarget(configItem, target, out bool isLegacyZapretConfig))
                return null;

            configItem.file_name = filename;
            configItem.packId = packId;
            configItem.IsLegacy = isLegacyZapretConfig;
            return configItem;
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateWarningLog(
                nameof(ConfigurationService),
                $"Cannot load selected config '{packId}/{filename}': {ex.Message}");
            return null;
        }
    }

    private static bool MatchesTarget(
        ConfigItem? configItem,
        string target,
        out bool isLegacyZapretConfig)
    {
        if (configItem == null)
        {
            isLegacyZapretConfig = false;
            return false;
        }

        return MatchesTarget(configItem.target?.FirstOrDefault(), target, out isLegacyZapretConfig);
    }

    private static bool MatchesTarget(
        string? configTarget,
        string target,
        out bool isLegacyZapretConfig)
    {
        isLegacyZapretConfig =
            target == HardcodedItemIds.ComponentIds[Components.Zapret2] &&
            configTarget == HardcodedItemIds.ComponentIds[Components.Zapret];

        return target == "$ANY" || configTarget == target || isLegacyZapretConfig;
    }
}
