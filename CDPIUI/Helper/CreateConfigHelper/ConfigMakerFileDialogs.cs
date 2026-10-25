using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using WinUI3Localizer;

namespace CDPIUI.Helper.CreateConfigHelper;

// The file pickers of the preset editor.
internal static class ConfigMakerFileDialogs
{
    /// <summary>Returns an empty string when the user cancels.</summary>
    public static string ChooseReplacementFile(ILocalizer localizer, string missingPath)
    {
        string extension = Path.GetExtension(missingPath);
        using OpenFileDialog dialog = new()
        {
            Title = localizer.GetLocalizedString("ConfigImportChooseFileTitle"),
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Filter = string.IsNullOrWhiteSpace(extension)
                ? $"{localizer.GetLocalizedString("AllSupported")} (*.*)|*.*"
                : $"{extension.TrimStart('.').ToUpperInvariant()} (*{extension})|*{extension}|{localizer.GetLocalizedString("AllSupported")} (*.*)|*.*",
            FilterIndex = 1,
            RestoreDirectory = true,
        };
        return dialog.ShowDialog() == DialogResult.OK
            ? dialog.FileName
            : string.Empty;
    }

    /// <summary>Returns an empty list when the user cancels.</summary>
    public static IReadOnlyList<string> ChoosePresetFiles(ILocalizer localizer)
    {
        using OpenFileDialog dialog = new()
        {
            Title = localizer.GetLocalizedString("ConfigMakerAddPresetFileDialogTitle"),
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Filter = localizer.GetLocalizedString("ConfigMakerPresetResourceFileFilter"),
            Multiselect = true,
            RestoreDirectory = true,
        };
        return dialog.ShowDialog() == DialogResult.OK
            ? dialog.FileNames
            : [];
    }

    /// <summary>Returns null when the user cancels.</summary>
    public static string ChooseExportPath(ILocalizer localizer, bool allowTextFormat)
    {
        Microsoft.Win32.SaveFileDialog dialog = new()
        {
            OverwritePrompt = true,
            FileName = "config" + ConfigMakerPresetExporter.SharePackageExtension,
            DefaultExt = ConfigMakerPresetExporter.SharePackageExtension,
            Filter = (allowTextFormat ? localizer.GetLocalizedString("ConfigMakerTextFileFilter") + "|" : "") + localizer.GetLocalizedString("ConfigMakerCCFileFilter"),
        };
        dialog.FilterIndex = dialog.Filter.Split('|').Length / 2;
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
