using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Shared;

namespace CDPIUI.ViewModels;

/// <summary>
/// File locations of the site lists BlockCheck2 writes: the user's custom list and the per-run
/// folder of generated per-site lists.
/// </summary>
internal static class BlockCheck2SiteListStorage
{
    public static string CreateGeneratedSiteListRoot() => Path.Combine(
        GetGeneratedSiteListsBase(),
        Guid.NewGuid().ToString("N"));

    public static string CreateFallbackIndividualSiteListRoot() => Path.Combine(
        GetGeneratedSiteListsBase(),
        "Unscoped");

    public static string CreateCustomSiteListPath()
    {
        string directory = Path.Combine(
            Directories.StoreLocalUserItemDirectory,
            SharedConstants.LocalUserItemSiteListsFolder,
            "BlockCheck2",
            "Custom");
        return Path.Combine(directory, $"custom_sites_{Guid.NewGuid():N}.txt");
    }

    public static bool IsLegacyGeneratedSiteList(string filePath)
    {
        string legacyRoot = Path.GetFullPath(Path.Combine(
            Directories.StoreLocalUserItemDirectory,
            SharedConstants.LocalUserItemSiteListsFolder,
            "BlockCheck2",
            "PerSite"));
        string fullPath = Path.GetFullPath(filePath);
        return fullPath.StartsWith(
            legacyRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Joins the non-empty lines of <paramref name="content"/>, without duplicates.
    /// </summary>
    public static string NormalizeCustomContent(string content) => string.Join(
        Environment.NewLine,
        (content ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase));

    public static async Task WriteCustomSiteListAsync(
        string filePath,
        string normalizedContent,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(
            filePath,
            normalizedContent + Environment.NewLine,
            new UTF8Encoding(false),
            cancellationToken);
    }

    /// <summary>
    /// Removes the generated site lists under <paramref name="generatedSiteListRoot"/>. A path
    /// outside the BlockCheck2 storage is never removed.
    /// </summary>
    public static void CleanupGeneratedSiteLists(string generatedSiteListRoot)
    {
        try
        {
            string generatedBase = Path.GetFullPath(GetGeneratedSiteListsBase())
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string target = Path.GetFullPath(generatedSiteListRoot);
            if (!target.StartsWith(generatedBase, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Instance.CreateWarningLog(
                    nameof(BlockCheck2ViewModel),
                    $"Refusing to remove a generated site-list path outside BlockCheck2 storage: {target}");
                return;
            }

            if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Logger.Instance.CreateWarningLog(
                nameof(BlockCheck2ViewModel),
                $"Cannot remove generated BlockCheck2 site lists: {exception.Message}");
        }
    }

    private static string GetGeneratedSiteListsBase() => Path.Combine(
        Directories.DataDirectory,
        "BlockCheck2",
        "GeneratedSiteLists");
}
