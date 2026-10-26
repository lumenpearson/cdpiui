#nullable enable

using System;
using System.IO;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.ViewModels;

namespace CDPIUI.Helper.CreateConfigHelper;

internal static class ConfigMakerPresetPaths
{
    public static bool IsPathInside(string path, string root)
    {
        string fullPath = Path.GetFullPath(path);
        string fullRoot = Path.GetFullPath(root).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        return string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase) ||
               fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public static string? ResolveResourcePath(
        string sourcePath,
        string componentDirectory,
        string presetDirectory,
        ConfigItem sourceConfig)
    {
        try
        {
            string candidate = sourceConfig.ResolveFilePath(
                sourcePath,
                componentDirectory,
                presetDirectory);
            return File.Exists(candidate) ? candidate : null;
        }
        catch
        {
            return null;
        }
    }

    public static string GetSourcePresetDirectory(
        ConfigMakerPresetDocument document,
        string fallbackDirectory)
    {
        if (string.IsNullOrWhiteSpace(document.PackId))
        {
            return fallbackDirectory;
        }
        string directory = ConfigurationService.GetItemFolderFromPackId(document.PackId);
        return Directory.Exists(directory) ? directory : fallbackDirectory;
    }
}
