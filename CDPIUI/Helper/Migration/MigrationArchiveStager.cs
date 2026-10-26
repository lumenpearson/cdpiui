using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using CDPIUI.Core.Data;
using CDPIUI.Shared.Migration;
using static CDPIUI.Helper.Migration.MigrationArchiveGuard;

namespace CDPIUI.Helper.Migration;

/// <summary>
/// Copies the incoming migration archive into the protected incoming folder and checks
/// its SHA-256 hash, so the archive cannot change after it was verified.
/// </summary>
internal static class MigrationArchiveStager
{
    private const long MaximumArchiveSize = 1024L * 1024L * 1024L;

    public static string StageArchive(
        GoodbyeDpiMigrationActivationRequest request,
        CancellationToken cancellationToken)
    {
        string incomingDirectory = Path.Combine(
            Directories.DataDirectory, "Migration", "GoodbyeDPIUI", "Incoming");
        Directory.CreateDirectory(incomingDirectory);
        string stagedPath = Path.Combine(
            incomingDirectory,
            $"{request.MigrationId:N}-{request.ArchiveSha256[..12]}.zip");

        if (File.Exists(stagedPath))
        {
            EnsureStagedArchiveUnchanged(stagedPath, request, cancellationToken);
            return stagedPath;
        }

        if (!File.Exists(request.ArchivePath))
            throw new FileNotFoundException("The migration archive was not found.", request.ArchivePath);
        FileInfo sourceInfo = new(request.ArchivePath);
        if (sourceInfo.Length <= 0 || sourceInfo.Length > MaximumArchiveSize ||
            (sourceInfo.Attributes & FileAttributes.ReparsePoint) != 0)
            throw Invalid("The migration archive has an unsafe type or size.");

        string temporaryPath = Path.Combine(incomingDirectory, $"incoming-{Guid.NewGuid():N}.tmp");
        try
        {
            string actualHash = CopyAndHash(request.ArchivePath, temporaryPath, cancellationToken);

            if (!actualHash.Equals(request.ArchiveSha256, StringComparison.Ordinal))
                throw Invalid("The migration archive SHA-256 hash is invalid.");

            try
            {
                File.Move(temporaryPath, stagedPath);
            }
            catch (IOException) when (File.Exists(stagedPath))
            {
                EnsureStagedArchiveUnchanged(stagedPath, request, cancellationToken);
            }
            return stagedPath;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static string CopyAndHash(
        string sourcePath,
        string temporaryPath,
        CancellationToken cancellationToken)
    {
        // Windows cannot move the temporary file while the FileShare.None
        // destination handle is open, so the caller moves it after this method returns.
        using FileStream source = new(
            sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.SequentialScan);
        using FileStream destination = new(
            temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            81920, FileOptions.WriteThrough);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        byte[] buffer = new byte[81920];
        long copied = 0;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            copied += read;
            if (copied > MaximumArchiveSize)
                throw Invalid("The migration archive exceeds the size limit.");
            destination.Write(buffer, 0, read);
            hash.AppendData(buffer, 0, read);
        }
        destination.Flush(flushToDisk: true);
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void EnsureStagedArchiveUnchanged(
        string stagedPath,
        GoodbyeDpiMigrationActivationRequest request,
        CancellationToken cancellationToken)
    {
        if (!HashFile(stagedPath, cancellationToken).Equals(
                request.ArchiveSha256, StringComparison.Ordinal))
            throw Invalid("The protected staged archive was modified.");
    }

    private static string HashFile(string path, CancellationToken cancellationToken)
    {
        FileInfo info = new(path);
        if (info.Length <= 0 || info.Length > MaximumArchiveSize ||
            (info.Attributes & FileAttributes.ReparsePoint) != 0)
            throw Invalid("The protected staged archive has an unsafe type or size.");
        using FileStream stream = new(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.SequentialScan);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[81920];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            hash.AppendData(buffer, 0, read);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}
