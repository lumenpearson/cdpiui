using System.IO;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.Basic.Settings;

namespace CDPIUI.Helper.CreateConfigHelper;

/// <summary>
/// Writes the folders and executable names of the installed components to the GoodCheck config.ini.
/// </summary>
internal static class GoodCheckComponentPathsWriter
{
    internal static void Write(IniSettingsHelper iniSettingsHelper, string itemsFolder)
    {
        if (DatabaseHelper.Instance.IsItemInstalled(HardcodedItemIds.ComponentIds[Components.Zapret]))
        {
            iniSettingsHelper.SetValue<string>("Zapret", "ZapretFolder", GetComponentFolder(Components.Zapret, itemsFolder));
            iniSettingsHelper.SetValue<string>("Zapret", "ZapretExecutableName", DatabaseHelper.Instance.GetItemById(HardcodedItemIds.ComponentIds[Components.Zapret])?.Executable + ".exe");
        }
        if (DatabaseHelper.Instance.IsItemInstalled(HardcodedItemIds.ComponentIds[Components.GoodbyeDPI]))
        {
            iniSettingsHelper.SetValue<string>("GoodbyeDPI", "GoodbyeDPIFolder", GetComponentFolder(Components.GoodbyeDPI, itemsFolder));
            iniSettingsHelper.SetValue<string>("GoodbyeDPI", "GoodbyeDPIExecutableName", DatabaseHelper.Instance.GetItemById(HardcodedItemIds.ComponentIds[Components.GoodbyeDPI])?.Executable + ".exe");
        }
        if (DatabaseHelper.Instance.IsItemInstalled(HardcodedItemIds.ComponentIds[Components.ByeDPI]))
        {
            iniSettingsHelper.SetValue<string>("ByeDPI", "ByeDPIFolder", GetComponentFolder(Components.ByeDPI, itemsFolder));
            iniSettingsHelper.SetValue<string>("ByeDPI", "ByeDPIExecutableName", DatabaseHelper.Instance.GetItemById(HardcodedItemIds.ComponentIds[Components.ByeDPI])?.Executable + ".exe");
        }
    }

    // The folder recorded in the Store database; the default folder is only the fallback.
    private static string GetComponentFolder(Components component, string itemsFolder)
    {
        string id = HardcodedItemIds.ComponentIds[component];
        string directory = DatabaseHelper.Instance.GetItemById(id)?.Directory;
        return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)
            ? directory
            : Path.Combine(itemsFolder, id);
    }
}
