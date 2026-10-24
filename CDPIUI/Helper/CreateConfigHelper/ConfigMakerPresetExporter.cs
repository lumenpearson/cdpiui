using System;
using System.IO;
using System.Threading.Tasks;
using CDPIUI.AddOns.ConfigShare;
using CDPIUI.Core;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.ViewModels;

namespace CDPIUI.Helper.CreateConfigHelper;

// Writes the edited preset to a file: a ".cdpiconfig" share package or the plain command text.
internal static class ConfigMakerPresetExporter
{
    public const string SharePackageExtension = ".cdpiconfig";

    public static async Task ExportAsync(ConfigMakerPresetDocument document, string filePath)
    {
        // The text export also checks that the preset can be resolved, for both formats.
        string exportText = ConfigMakerPresetStorageService
            .CreateResolvedCommandForTextExport(document);

        if (string.Equals(Path.GetExtension(filePath), SharePackageExtension, StringComparison.OrdinalIgnoreCase))
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            using var package = await new ConfigShareService()
                .ExportAsync(
                document.ToConfigItem(Guid.NewGuid().ToString(), fileName),
                fileName,
                SettingsManager.Instance.GetValueOrDefault("CONFIGKIT", "lastUsedDevName", defaultValue: Environment.UserName));
            await FileSystemService.CopyFileAsync(package.ArchivePath, filePath);
        }
        else
        {
            await File.WriteAllTextAsync(filePath, exportText);
        }
    }
}
