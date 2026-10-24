using CDPIUI.Core.Store.Database;

namespace CDPIUI.AddOns.ConfigImport;

/// <summary>
/// Creates import targets from Store components and resolves the version a Config targets.
/// </summary>
internal static class ConfigImportTargets
{
    // The database stores an empty string, not null, for a missing name.
    public static ConfigImportTarget Create(DatabaseStoreItem item) => new(
        item.Id!,
        !string.IsNullOrEmpty(item.ShortName) ? item.ShortName : !string.IsNullOrEmpty(item.Name) ? item.Name : item.Id!,
        item.Executable ?? string.Empty,
        item.CurrentVersion,
        item.Directory);

    public static string ResolveVersion(ConfigImportTarget target) =>
        string.IsNullOrWhiteSpace(target.Version) ? ConfigImportService.CurrentVersionPlaceholder : target.Version;
}
