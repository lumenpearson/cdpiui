using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace CDPIUI.Shared.Basic.Filesystem;

public static class ArchivePaths
{
    // Limits for archives from the Store, local packs and application patches. The largest
    // real packages are about a hundred megabytes; a zip bomb is far above these values.
    public const int MaxEntries = 20000;
    public const long MaxTotalBytes = 1L << 30;
    public const long RatioCheckMinimumBytes = 1L << 20;
    public const int MaxCompressionRatio = 200;

    public static bool TryGetDestination(string root, string relativePath, out string destination)
    {
        destination = string.Empty;
        if (string.IsNullOrEmpty(relativePath) || Path.IsPathRooted(relativePath) || relativePath.Contains(':'))
            return false;

        string fullRoot = Path.GetFullPath(root);
        if (!fullRoot.EndsWith(Path.DirectorySeparatorChar))
            fullRoot += Path.DirectorySeparatorChar;

        string fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            return false;

        destination = fullPath;
        return true;
    }

    public static string GetDestinationOrThrow(string root, string relativePath)
    {
        if (!TryGetDestination(root, relativePath, out string destination))
            throw new InvalidDataException($"Archive entry \"{relativePath}\" points outside of the target directory.");
        return destination;
    }

    /// <summary>
    /// Rejects an archive with too many entries, too much data or an entry that is compressed
    /// suspiciously well, before anything is written to disk.
    /// </summary>
    public static void EnsureWithinLimits(IReadOnlyCollection<ZipArchiveEntry> entries)
    {
        if (entries.Count > MaxEntries)
            throw new InvalidDataException($"The archive has {entries.Count} entries; the limit is {MaxEntries}.");

        long total = 0;
        foreach (ZipArchiveEntry entry in entries)
        {
            if (entry.Length < 0 || entry.Length > MaxTotalBytes || (total += entry.Length) > MaxTotalBytes)
                throw new InvalidDataException("The archive unpacks to more data than allowed.");
            if (entry.Length >= RatioCheckMinimumBytes &&
                entry.Length / Math.Max(entry.CompressedLength, 1) > MaxCompressionRatio)
                throw new InvalidDataException($"Archive entry \"{entry.FullName}\" has a suspicious compression ratio.");
        }
    }

    /// <summary>
    /// Writes an entry to a file and stops when the entry gives more data than its header says.
    /// </summary>
    public static void ExtractToFileBounded(ZipArchiveEntry entry, string destination)
    {
        using Stream source = entry.Open();
        using var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        byte[] buffer = new byte[81920];
        long written = 0;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            written += read;
            if (written > entry.Length)
                throw new InvalidDataException($"Archive entry \"{entry.FullName}\" is larger than its header says.");
            target.Write(buffer, 0, read);
        }
    }

    /// <summary>
    /// Rejects a destination whose folder chain inside the root, or the file itself, is a symbolic
    /// link or a junction: a planted link would send the write to another place.
    /// </summary>
    public static void EnsureNoReparsePoints(string root, string destination)
    {
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string? current = Path.GetFullPath(destination);
        while (current != null && current.Length >= fullRoot.Length)
        {
            if ((File.Exists(current) || Directory.Exists(current)) &&
                File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidDataException($"\"{current}\" is a link; the archive is not extracted through links.");
            if (string.Equals(current, fullRoot, StringComparison.OrdinalIgnoreCase))
                break;
            current = Path.GetDirectoryName(current);
        }
    }
}
