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

            await DownloadTemporaryArchiveAsync(zipUrl, versionControl);
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

        string newFolder = $"{targetFolder.TrimEnd(Path.DirectorySeparatorChar)}.new-{Guid.NewGuid():N}";
        try
        {
            await DownloadTemporaryArchiveAsync(zipUrl, versionControl, archiveStream =>
            {
                Directory.CreateDirectory(newFolder);
                ExtractWithoutRootFolder(archiveStream, newFolder);

                if (Directory.Exists(targetFolder))
                    Directory.Delete(targetFolder, recursive: true);
                Directory.Move(newFolder, targetFolder);
            });
        }
        finally
        {
            DeleteTemporaryFolder(newFolder);
        }
    }

    private static Task DownloadTemporaryArchiveAsync(
        string zipUrl, SupportedVersionControls versionControl, Action<Stream>? consume = null) =>
        WithTemporaryArchiveAsync(Path.GetTempPath(), async (path, openDestination) =>
        {
            using var requestWorker = new RequestWorker();
            await requestWorker.DownloadToStreamAsync(zipUrl, path, openDestination,
                token: GetDownloadToken(versionControl),
                useStoreUserAgent: true,
                completionOption: HttpCompletionOption.ResponseContentRead);
        }, consume);

    internal static async Task WithTemporaryArchiveAsync(
        string temporaryDirectory, Func<string, Func<Stream>, Task> download, Action<Stream>? consume = null)
    {
        string path = Path.Combine(temporaryDirectory, $"cdpiui_store_{Guid.NewGuid():N}.tmp");
        FileStream? archiveStream = null;
        try
        {
            // Keep the same exclusively created file through transfer and extraction.
            await download(path, () => archiveStream ??= new FileStream(
                path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920,
                FileOptions.Asynchronous | FileOptions.DeleteOnClose));

            if (consume is null)
                return;

            if (archiveStream is null)
                throw new InvalidOperationException("The repository download did not open its temporary archive.");

            archiveStream.Position = 0;
            consume(archiveStream);
        }
        finally
        {
            archiveStream?.Dispose();
        }
    }

    // The archive holds one folder named after the branch; its content is extracted.
    private static void ExtractWithoutRootFolder(Stream archiveStream, string destinationFolder)
    {
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: true);
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

    private static void DeleteTemporaryFolder(string newFolder)
    {
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
