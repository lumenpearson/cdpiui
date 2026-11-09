using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using CDPIUI.Core.Security;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.Shared.Exceptions.Catalog;
using CDPIUI.Shared.Security;

namespace CDPIUI.Core.System;

internal class ZipService
{
    public static async Task ExtractZip(
        string zipFilePath,
        string? zipFolderToUnpack,
        string extractTo,
        IEnumerable<string>? filesToSkip = null,
        bool isCatalogCheckRequired = false
    )
    {
        if (!isCatalogCheckRequired)
        {
            await ExtractEntries(zipFilePath, zipFolderToUnpack, extractTo, filesToSkip, mergeTextFiles: true);
            return;
        }

        // A signed archive is checked in a folder of its own and copied into place only when its
        // catalog is valid, so files of a rejected archive never reach the destination.
        string verifyDirectory = extractTo.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            ".verify-" + Guid.NewGuid().ToString("N");
        try
        {
            await ExtractEntries(zipFilePath, zipFolderToUnpack, verifyDirectory, filesToSkip, mergeTextFiles: false);
            await VerifySnapshotAndPromoteAsync(verifyDirectory, extractTo, VerifyCatalog);
        }
        finally
        {
            try
            {
                if (Directory.Exists(verifyDirectory))
                    Directory.Delete(verifyDirectory, recursive: true);
            }
            catch (Exception ex)
            {
                Basic.Logger.Instance.CreateWarningLog(nameof(ZipService), $"Cannot delete the verification folder {verifyDirectory}: {ex.Message}");
            }
        }
    }

    internal static Task VerifyAndPromoteAsync(string sourceDirectory, string destinationDirectory, Func<string, Task> verify) =>
        VerifySnapshotAndPromoteAsync(sourceDirectory, destinationDirectory, _ => verify(sourceDirectory));

    private static async Task VerifySnapshotAndPromoteAsync(string sourceDirectory, string destinationDirectory, Func<ReadOnlyDirectorySnapshot, Task> verify)
    {
        using var snapshot = ReadOnlyDirectorySnapshot.Capture(sourceDirectory);
        await verify(snapshot);
        PromoteSnapshot(snapshot, destinationDirectory);
    }

    private static void PromoteSnapshot(ReadOnlyDirectorySnapshot snapshot, string destinationDirectory)
    {
        ArchivePaths.EnsureDirectoryExists(destinationDirectory);
        foreach (var file in snapshot.Files)
        {
            string destination = ArchivePaths.GetDestinationOrThrow(destinationDirectory, file.RelativePath);
            ArchivePaths.EnsureNoReparsePoints(destinationDirectory, destination);
            ArchivePaths.EnsureDirectoryExists(Path.GetDirectoryName(destination)!);
            ArchivePaths.EnsureNoReparsePoints(destinationDirectory, destination);
            using var output = WritablePathLease.Open(destination);
            output.Stream.SetLength(0);
            file.Stream.Position = 0;
            file.Stream.CopyTo(output.Stream);
        }
        foreach (string relativePath in snapshot.Directories)
        {
            string destination = ArchivePaths.GetDestinationOrThrow(destinationDirectory, relativePath);
            ArchivePaths.EnsureNoReparsePoints(destinationDirectory, destination);
            ArchivePaths.EnsureDirectoryExists(destination);
            ArchivePaths.EnsureNoReparsePoints(destinationDirectory, destination);
        }
    }

    private static async Task ExtractEntries(
        string zipFilePath,
        string? zipFolderToUnpack,
        string extractTo,
        IEnumerable<string>? filesToSkip,
        bool mergeTextFiles
    )
    {
        filesToSkip = filesToSkip ?? Enumerable.Empty<string>();

        if (!Directory.Exists(extractTo))
            ArchivePaths.EnsureDirectoryExists(extractTo);

        using (var archive = ZipFile.OpenRead(zipFilePath))
        {
            var entries = archive.Entries;
            ArchivePaths.EnsureWithinLimits(entries);

            var patternSegments = GetPatternSegments(zipFolderToUnpack);

            foreach (var entry in entries)
            {
                var relativePath = GetRelativePathOrNull(entry, patternSegments);
                if (relativePath == null || ShouldSkip(relativePath, filesToSkip))
                    continue;

                var destinationPath = PrepareDestination(extractTo, relativePath);

                if (mergeTextFiles
                    && relativePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                    && File.Exists(destinationPath))
                {
                    await MergeTextFileAsync(entry, extractTo, destinationPath);
                    continue;
                }

                if (entry.FullName.EndsWith("/"))
                {
                    if (!Directory.Exists(destinationPath))
                    {
                        ArchivePaths.EnsureDirectoryExists(destinationPath);
                        ArchivePaths.EnsureNoReparsePoints(extractTo, destinationPath);
                    }
                }
                else
                {
                    ArchivePaths.ExtractToFileBounded(entry, destinationPath);
                }
            }
        }
    }

    private static string[] GetPatternSegments(string? zipFolderToUnpack)
    {
        if (zipFolderToUnpack == null || zipFolderToUnpack == "/")
            zipFolderToUnpack = string.Empty;
        else if (zipFolderToUnpack.EndsWith("/"))
            zipFolderToUnpack = zipFolderToUnpack.TrimEnd('/');

        return string.IsNullOrEmpty(zipFolderToUnpack)
            ? Array.Empty<string>()
            : zipFolderToUnpack.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// Returns the entry path below the folder pattern, or null when the entry is outside the
    /// pattern or is the pattern folder itself. A "$ANY" pattern segment matches any segment.
    /// </summary>
    private static string? GetRelativePathOrNull(ZipArchiveEntry entry, string[] patternSegments)
    {
        var entryPath = entry.FullName.Replace('\\', '/').TrimStart('/');
        var entrySegments = entryPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

        if (!MatchesPattern(entrySegments, patternSegments))
            return null;

        var relativeSegments = entrySegments.Skip(patternSegments.Length).ToArray();
        var relativePath = string.Join("/", relativeSegments).TrimStart('/');
        return string.IsNullOrEmpty(relativePath) ? null : relativePath;
    }

    private static bool MatchesPattern(string[] entrySegments, string[] patternSegments)
    {
        if (entrySegments.Length < patternSegments.Length)
            return false;

        for (int i = 0; i < patternSegments.Length; i++)
        {
            var pat = patternSegments[i];
            if (pat == "$ANY")
                continue;

            if (!string.Equals(pat, entrySegments[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    // "*.ext" skips by extension; any other value skips paths that contain it.
    private static bool ShouldSkip(string relativePath, IEnumerable<string> filesToSkip) =>
        filesToSkip.Any(skip => skip.StartsWith("*.")
            ? relativePath.EndsWith(skip.Substring(1), StringComparison.OrdinalIgnoreCase)
            : relativePath.Contains(skip, StringComparison.OrdinalIgnoreCase));

    private static string PrepareDestination(string extractTo, string relativePath)
    {
        var destinationPath = ArchivePaths.GetDestinationOrThrow(extractTo, relativePath);
        ArchivePaths.EnsureNoReparsePoints(extractTo, destinationPath);
        var destinationDir = Path.GetDirectoryName(destinationPath);
        if (!Directory.Exists(destinationDir) && !string.IsNullOrEmpty(destinationDir))
            ArchivePaths.EnsureDirectoryExists(destinationDir);
        ArchivePaths.EnsureNoReparsePoints(extractTo, destinationPath);
        return destinationPath;
    }

    private static async Task MergeTextFileAsync(ZipArchiveEntry entry, string extractTo, string destinationPath)
    {
        // FileSystemLinksManager replaces a linked list with a link and keeps
        // the original as .bak; the lines merge into that original file.
        if (File.Exists($"{destinationPath}.bak"))
        {
            destinationPath = $"{destinationPath}.bak";
        }
        ArchivePaths.EnsureNoReparsePoints(extractTo, destinationPath);
        using var lease = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? WritablePathLease.Open(destinationPath) : null;
        using var portable = lease == null ? new FileStream(destinationPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None) : null;
        var existing = lease?.Stream ?? portable!;
        // Whole lines are compared: as a substring, "t.co" was dropped because
        // the list already had "reddit.com".
        var destLines = new HashSet<string>(StringComparer.Ordinal);
        using (var reader = new StreamReader(existing, Encoding.UTF8, true, 1024, leaveOpen: true))
        {
            string? line;
            while ((line = reader.ReadLine()) != null) destLines.Add(line.Trim());
        }
        bool endsWithNewLine = EndsWithNewLine(existing);

        var newLines = new List<string>();
        using (var reader = new StreamReader(entry.Open()))
        {
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (line.Trim().Length > 0 && destLines.Add(line.Trim()))
                    newLines.Add(line);
            }
        }

        if (newLines.Count > 0)
        {
            existing.Seek(0, SeekOrigin.End);
            using var stream = new StreamWriter(existing, new UTF8Encoding(false), 1024, leaveOpen: true);
            if (!endsWithNewLine)
                await stream.WriteLineAsync();
            foreach (string line in newLines)
                await stream.WriteLineAsync(line);
        }
    }

    private static bool EndsWithNewLine(Stream existing)
    {
        if (existing.Length == 0)
            return true;
        existing.Seek(-1, SeekOrigin.End);
        return existing.ReadByte() == '\n';
    }

    private static async Task VerifyCatalog(ReadOnlyDirectorySnapshot snapshot)
    {
        CatalogCheckResult catalogCheckResult = await CertificateCheck.CheckCatalog(Path.Combine(snapshot.RootPath, "catalog.cat"), snapshot);
        switch (catalogCheckResult)
        {
            case CatalogCheckResult.Success:
                return;
            case CatalogCheckResult.FailureNoSignature:
                throw new CatalogNoSignature("Catalog file isn't signed");
            case CatalogCheckResult.FailureNotTrustedSignature:
                throw new CatalogNoSignature("Signature not trusted");
            case CatalogCheckResult.FailureNotValid:
                throw new CatalogInvalid();
            default:
                throw new CatalogInvalid("Unknown");
        }
    }
}
