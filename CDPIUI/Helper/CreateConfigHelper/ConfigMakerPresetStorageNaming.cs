#nullable enable

using System;
using System.IO;
using System.Text.RegularExpressions;
using CDPIUI.Shared;
using Unidecode.NET;

namespace CDPIUI.Helper.CreateConfigHelper;

internal static class ConfigMakerPresetStorageNaming
{
    public const string StorageFolderName = "ConfigMaker";

    // Converts a display name to an ASCII file name part: spaces become "_", other characters are dropped.
    public static string ToAsciiSlug(string value)
    {
        string slug = value.Unidecode();
        slug = Regex.Replace(slug, @"\s+", "_");
        slug = Regex.Replace(slug, @"[^A-Za-z0-9_.-]", string.Empty);
        return Regex.Replace(slug, "_+", "_").Trim('_');
    }

    public static string CreateStorageId(string presetName, string userStorage)
    {
        string slug = ToAsciiSlug(presetName);
        if (slug.Length == 0)
        {
            slug = "preset";
        }
        if (slug.Length > 48)
        {
            slug = slug[..48].TrimEnd('_', '.', '-');
        }
        string baseId = $"{slug}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        string candidate = baseId;
        int suffix = 2;
        while (File.Exists(Path.Combine(userStorage, $"{candidate}.json")) ||
               Directory.Exists(Path.Combine(
                   userStorage,
                   SharedConstants.LocalUserItemSiteListsFolder,
                   StorageFolderName,
                   candidate)) ||
               Directory.Exists(Path.Combine(
                   userStorage,
                   SharedConstants.LocalUserItemBinsFolder,
                   StorageFolderName,
                   candidate)))
        {
            candidate = $"{baseId}_{suffix++}";
        }
        return candidate;
    }

    public static string CreateOverwriteStorageId(string configFileName)
    {
        string storageId = ToAsciiSlug(Path.GetFileNameWithoutExtension(configFileName))
            .Trim('_', '.', '-');
        return storageId.Length == 0 ? "preset" : storageId;
    }

    public static string GetUniqueDestinationPath(string directory, string sourceFileName)
    {
        string asciiName = sourceFileName.Unidecode();
        asciiName = Regex.Replace(asciiName, @"[^A-Za-z0-9_.-]", "_");
        asciiName = Regex.Replace(asciiName, "_+", "_").Trim('_');
        if (asciiName.Length == 0)
        {
            asciiName = "resource.bin";
        }
        string stem = Path.GetFileNameWithoutExtension(asciiName);
        string extension = Path.GetExtension(asciiName);
        string candidate = Path.Combine(directory, asciiName);
        int suffix = 2;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{stem}_{suffix++}{extension}");
        }
        return candidate;
    }

    public static string NormalizeDisplayName(string? value) =>
        Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();
}
