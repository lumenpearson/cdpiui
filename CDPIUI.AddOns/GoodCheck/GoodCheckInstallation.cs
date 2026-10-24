using CDPIUI.Core.Data;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;

namespace CDPIUI.AddOns.GoodCheck;

/// <summary>
/// Where the GoodCheck add-on is installed, as recorded in the Store database.
/// </summary>
internal static class GoodCheckInstallation
{
    public static readonly string AddonId = HardcodedItemIds.AddOnsIds[Core.Store.Data.AddOns.GoodCheck];

    /// <summary>
    /// The folder GoodCheck is installed in, as recorded in the Store database.
    /// </summary>
    public static string GetDirectory()
    {
        if (DatabaseHelper.Instance.IsItemInstalled(AddonId))
        {
            string? directory = DatabaseHelper.Instance.GetItemById(AddonId)?.Directory;
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                return directory;
        }
        return Path.Combine(Directories.StoreItemsDirectory, AddonId);
    }

    public static bool IsInstalled() => DatabaseHelper.Instance.IsItemInstalled(AddonId);

    /// <summary>
    /// The path of the executable the Store database names for an installed add-on.
    /// </summary>
    public static string GetExecutablePath(string directory)
    {
        DatabaseStoreItem item = DatabaseHelper.Instance.GetItemById(AddonId);

        return Path.Combine(
            directory,
            item.Executable + ".exe");
    }
}
