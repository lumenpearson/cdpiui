using System;
using System.IO;
using CDPIUI.AddOns.ConfigImport;
using CDPIUI.Core.Store.Database;

namespace CDPIUI.Helper.AddOns.ConfigImport;

/// <summary>
/// Turns installed Store components into the targets a configuration can be imported to.
/// </summary>
internal static class ConfigImportTargetFactory
{
    // The database stores an empty string, not null, for a missing name.
    internal static ConfigImportTarget Create(DatabaseStoreItem item) => new(
        item.Id!,
        !string.IsNullOrEmpty(item.ShortName) ? item.ShortName : !string.IsNullOrEmpty(item.Name) ? item.Name : item.Id!,
        item.Executable ?? string.Empty,
        item.CurrentVersion,
        item.Directory);

    internal static bool IsImportTarget(DatabaseStoreItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Id) ||
            string.IsNullOrWhiteSpace(item.Directory) ||
            string.IsNullOrWhiteSpace(item.Executable))
        {
            return false;
        }

        return Directory.Exists(item.Directory) &&
               File.Exists(Path.Combine(item.Directory, GetExecutableFileName(item.Executable)));
    }

    private static string GetExecutableFileName(string executable) =>
        executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? executable
            : $"{executable}.exe";
}
