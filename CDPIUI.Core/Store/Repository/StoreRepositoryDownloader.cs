using System.Diagnostics;
using System.IO.Compression;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Store.Network;
using CDPIUI.Shared;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.Shared.Secrets;

namespace CDPIUI.Core.Store.Repository;

/// <summary>
/// Downloads the archive of the Store repository from GitHub or GitLab and extracts it.
/// </summary>
internal static class StoreRepositoryDownloader
{
    private static readonly string GitHubApiToken = Secret.GitHubToken;
    private static readonly string GitLabApiToken = Secret.GitLabToken;

    /// <summary>
    /// The API token of a version control, or an empty string when it has none.
    /// </summary>
    public static string GetToken(SupportedVersionControls versionControl)
    {
        return versionControl switch
        {
            SupportedVersionControls.GitHub => GitHubApiToken,
            SupportedVersionControls.GitLab => GitLabApiToken,
            _ => string.Empty,
        };
    }

    private static string GetDownloadToken(SupportedVersionControls versionControl) =>
        versionControl == SupportedVersionControls.GitHub ? GitHubApiToken : GitLabApiToken;

    public static async Task<bool> TryLoadDatabaseForVersionControl(SupportedVersionControls versionControl)
    {
        try
        {
            string zipUrl = GetStoreUrl(versionControl);

            string tempZipPath = Path.Combine(Path.GetTempPath(), $"cdpiui_store_{Guid.NewGuid():N}.tmp");
            using var requestWorker = new RequestWorker();
            await requestWorker.DownloadFileAsync(zipUrl, tempZipPath,
                token: GetDownloadToken(versionControl),
                useStoreUserAgent: true,
                completionOption: HttpCompletionOption.ResponseContentRead);

            File.Delete(tempZipPath);

            return true;
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(RepositoryLoaderService), $"Error loading store database: {ex.Message}");
        }
        return false;
    }

    /// <summary>
    /// Downloads the Store repository and replaces the cache only after the archive was
    /// extracted completely, so a failed download or a damaged archive keeps the old cache.
    /// </summary>
    public static async Task DownloadAndExtractStore(SupportedVersionControls versionControl, string targetFolder)
    {
        string zipUrl = GetStoreUrl(versionControl);
        Debug.WriteLine(zipUrl);

        // A fixed name in the shared temp folder could be prepared by another process.
        string tempZipPath = Path.Combine(Path.GetTempPath(), $"cdpiui_store_{Guid.NewGuid():N}.tmp");
        string newFolder = $"{targetFolder.TrimEnd(Path.DirectorySeparatorChar)}.new-{Guid.NewGuid():N}";
        try
        {
            using (var requestWorker = new RequestWorker())
            {
                await requestWorker.DownloadFileAsync(zipUrl, tempZipPath,
                    token: GetDownloadToken(versionControl),
                    useStoreUserAgent: true,
                    completionOption: HttpCompletionOption.ResponseContentRead);
            }

            Directory.CreateDirectory(newFolder);
            ExtractWithoutRootFolder(tempZipPath, newFolder);

            if (Directory.Exists(targetFolder))
                Directory.Delete(targetFolder, recursive: true);
            Directory.Move(newFolder, targetFolder);
        }
        finally
        {
            DeleteTemporaryFiles(tempZipPath, newFolder);
        }
    }

    // The archive holds one folder named after the branch; its content is extracted.
    private static void ExtractWithoutRootFolder(string zipPath, string destinationFolder)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        ArchivePaths.EnsureWithinLimits(archive.Entries);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith("/"))
                continue;

            var parts = entry.FullName.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 1)
                continue;

            string[] subParts = parts.Skip(1).ToArray();
            string relativePath = Path.Combine(subParts);

            string destinationPath = ArchivePaths.GetDestinationOrThrow(destinationFolder, relativePath);
            ArchivePaths.EnsureNoReparsePoints(destinationFolder, destinationPath);

            string? destDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destDir))
                Directory.CreateDirectory(destDir);

            ArchivePaths.ExtractToFileBounded(entry, destinationPath);
        }
    }

    private static void DeleteTemporaryFiles(string tempZipPath, string newFolder)
    {
        try
        { File.Delete(tempZipPath); }
        catch (IOException ex)
        {
            Basic.Logger.Instance.CreateWarningLog(nameof(RepositoryLoaderService), $"Cannot delete the temporary archive {tempZipPath}: {ex.Message}");
        }
        try
        {
            if (Directory.Exists(newFolder))
                Directory.Delete(newFolder, recursive: true);
        }
        catch (IOException ex)
        {
            Basic.Logger.Instance.CreateWarningLog(nameof(RepositoryLoaderService), $"Cannot delete the temporary folder {newFolder}: {ex.Message}");
        }
    }

    private static string GetStoreUrl(SupportedVersionControls versionControl)
    {
        return string.Format(
                VersionControlData.VersionControlsLinks[versionControl],
                versionControl == SupportedVersionControls.GitHub ? SharedConstants.StoreRepo : SharedConstants.GitLabStoreRepo);
    }
}
