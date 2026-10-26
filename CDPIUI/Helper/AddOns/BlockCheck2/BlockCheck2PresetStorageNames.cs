using System;
using System.IO;
using System.Text.RegularExpressions;
using CDPIUI.Shared;
using Unidecode.NET;

namespace CDPIUI.Helper.AddOns.BlockCheck2;

/// <summary>
/// Creates the ASCII storage id and file names under which a BlockCheck2 preset and its
/// files are stored in the local user item folder.
/// </summary>
internal static class BlockCheck2PresetStorageNames
{
    public const string StorageFolderName = "BlockCheck2";

    public static string GetUniqueDestinationPath(string directory, string fileName)
    {
        string safeName = CreateAsciiFileName(fileName);
        string candidate = Path.Combine(directory, safeName);
        string stem = Path.GetFileNameWithoutExtension(safeName);
        string extension = Path.GetExtension(safeName);
        int suffix = 2;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{stem}_{suffix++}{extension}");
        }
        return candidate;
    }

    public static string CreateStorageId(string presetName, string userStorage)
    {
        string slug = CreateAsciiSlug(presetName, 48);
        if (slug.Length == 0)
        {
            slug = "preset";
        }

        string baseId = $"{slug}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        string candidate = baseId;
        int suffix = 2;
        while (StorageIdExists(userStorage, candidate))
        {
            candidate = $"{baseId}_{suffix++}";
        }
        return candidate;
    }

    private static bool StorageIdExists(string userStorage, string storageId)
    {
        return Directory.Exists(Path.Combine(
                   userStorage,
                   SharedConstants.LocalUserItemSiteListsFolder,
                   StorageFolderName,
                   storageId)) ||
               Directory.Exists(Path.Combine(
                   userStorage,
                   SharedConstants.LocalUserItemBinsFolder,
                   StorageFolderName,
                   storageId)) ||
               File.Exists(Path.Combine(userStorage, $"{storageId}.json"));
    }

    private static string CreateAsciiFileName(string fileName)
    {
        string sourceName = string.IsNullOrWhiteSpace(fileName)
            ? "dependency.bin"
            : Path.GetFileName(fileName);
        string stem = CreateAsciiSlug(Path.GetFileNameWithoutExtension(sourceName), 120);
        if (stem.Length == 0)
        {
            stem = "dependency";
        }

        string extension = Path.GetExtension(sourceName).Unidecode();
        extension = Regex.Replace(extension, @"[^A-Za-z0-9.]", string.Empty);
        return stem + extension;
    }

    private static string CreateAsciiSlug(string value, int maxLength)
    {
        string slug = (value ?? string.Empty).Unidecode();
        slug = Regex.Replace(slug, @"\s+", "_");
        slug = Regex.Replace(slug, @"[^A-Za-z0-9_-]", "_");
        slug = Regex.Replace(slug, @"_+", "_").Trim('_', '-');
        if (slug.Length > maxLength)
        {
            slug = slug[..maxLength].TrimEnd('_', '-');
        }
        return slug;
    }
}
