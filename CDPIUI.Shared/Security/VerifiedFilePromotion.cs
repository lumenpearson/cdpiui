using System;
using System.Collections.Generic;
using System.IO;
using CDPIUI.Shared.Basic.Filesystem;

namespace CDPIUI.Shared.Security;

/// <summary>Publishes one held, verified snapshot, restoring originals when publication fails.</summary>
public static class VerifiedFilePromotion
{
    /// <summary>
    /// Prepares the entire batch before changing installed files. The callback runs while
    /// published files and originals remain held; a callback failure rolls the batch back.
    /// This is process-level recovery, not a durable crash journal or an atomic directory switch.
    /// </summary>
    public static void Publish(ReadOnlyDirectorySnapshot snapshot, string destinationDirectory, Action<string>? published = null)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        var directories = new Dictionary<string, ReadOnlyPathLease>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<PendingFile>();
        string root = Path.GetFullPath(destinationDirectory);
        if (root.Length > Path.GetPathRoot(root)!.Length)
            root = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        bool committed = false;
        Exception? completionFailure = null;
        try
        {
            directories.Add(root, ReadOnlyPathLease.OpenDirectoryForMutation(root));
            foreach (string relative in snapshot.Directories)
                GetDirectory(ArchivePaths.GetDestinationOrThrow(root, relative), root, directories);
            foreach (var file in snapshot.Files)
            {
                string destination = ArchivePaths.GetDestinationOrThrow(root, file.RelativePath);
                var parent = GetDirectory(Path.GetDirectoryName(destination)!, root, directories);
                var entry = new PendingFile(destination, parent, file.RelativePath);
                entries.Add(entry);
                try { entry.Original = WritablePathLease.OpenExistingForMove(destination, parent); }
                catch (FileNotFoundException) { }
                if (entry.Original != null && (WindowsFileHandles.GetAttributes(entry.Original.Stream.SafeFileHandle) & FileAttributes.ReadOnly) != 0)
                    throw new UnauthorizedAccessException("A read-only installed file cannot be replaced.");
                byte[]? descriptor = entry.Original == null ? null : FileAccessDescriptor.Read(entry.Original.Stream.SafeFileHandle);
                entry.Prepared = WritablePathLease.CreateForMove(Path.Combine(Path.GetDirectoryName(destination)!,
                    ".cdpiui-promotion-" + Guid.NewGuid().ToString("N") + ".tmp"), parent, descriptor);
                file.Stream.Position = 0;
                file.Stream.CopyTo(entry.Prepared.Stream);
                entry.Prepared.Stream.Flush(flushToDisk: true);
            }

            foreach (var entry in entries)
            {
                if (entry.Original != null)
                {
                    entry.Original.MoveTo(Path.Combine(Path.GetDirectoryName(entry.Destination)!,
                        ".cdpiui-promotion-" + Guid.NewGuid().ToString("N") + ".bak"), entry.Parent);
                    entry.OriginalMoved = true;
                }
                // Never replace an object planted after preparation. Only the captured
                // original was authorized for replacement, and it has already been moved.
                entry.Prepared!.MoveTo(entry.Destination, entry.Parent);
                published?.Invoke(entry.RelativePath);
            }
            committed = true;
            var cleanupFailures = new List<Exception>();
            foreach (var entry in entries)
            {
                if (entry.Original == null) continue;
                try { entry.Original.DeleteOnClose(); }
                catch (Exception ex) { cleanupFailures.Add(new IOException("Published files are installed; retained original: " + entry.Original.CurrentPath, ex)); }
            }
            if (cleanupFailures.Count != 0)
                throw new AggregateException("Publication completed, but some originals require cleanup.", cleanupFailures);
        }
        catch (Exception failure)
        {
            completionFailure = failure;
            if (committed) throw;
            var recoveryFailures = new List<Exception>();
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                if (entry.Prepared != null)
                {
                    try
                    {
                        entry.Prepared.DeleteOnClose();
                        entry.Prepared.Dispose();
                        entry.Prepared = null;
                    }
                    catch (Exception ex) { recoveryFailures.Add(new IOException("Cannot remove the held new file: " + entry.Prepared!.CurrentPath, ex)); }
                }
                if (entry.OriginalMoved)
                {
                    try { entry.Original!.MoveTo(entry.Destination, entry.Parent); }
                    catch (Exception ex) { recoveryFailures.Add(new IOException("Original retained for recovery: " + entry.Original!.CurrentPath + "; destination: " + entry.Destination, ex)); }
                }
            }
            if (recoveryFailures.Count != 0)
            {
                recoveryFailures.Insert(0, failure);
                completionFailure = new AggregateException("Publication failed and rollback requires recovery from the retained paths.", recoveryFailures);
                throw completionFailure;
            }
            throw;
        }
        finally
        {
            var closeFailures = new List<Exception>();
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                Close(entries[i].Prepared, closeFailures);
                Close(entries[i].Original, closeFailures);
            }
            // References from children keep ancestors alive regardless of enumeration order.
            foreach (var directory in directories.Values)
            {
                try { directory.Dispose(); }
                catch (Exception ex) { closeFailures.Add(ex); }
            }
            if (closeFailures.Count != 0)
            {
                if (completionFailure != null) closeFailures.Insert(0, completionFailure);
                throw new AggregateException("Cannot release all promotion resources; inspect the retained paths.", closeFailures);
            }
        }
    }

    private static void Close(WritablePathLease? lease, List<Exception> failures)
    {
        try { lease?.Dispose(); }
        catch (Exception ex) { failures.Add(new IOException("Cannot close the held file: " + lease!.CurrentPath, ex)); }
    }

    private static ReadOnlyPathLease GetDirectory(string path, string root, Dictionary<string, ReadOnlyPathLease> directories)
    {
        if (directories.TryGetValue(path, out var held)) return held;
        if (!path.StartsWith(root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? root : root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A promotion directory must remain beneath the held root.");
        // Every path comes from ArchivePaths beneath root; build only its immediate components.
        var parent = GetDirectory(Path.GetDirectoryName(path)!, root, directories);
        held = ReadOnlyPathLease.OpenOrCreateMutationDirectoryInHeldDirectory(path, parent);
        directories.Add(path, held);
        return held;
    }

    private sealed class PendingFile
    {
        internal readonly string Destination, RelativePath;
        internal readonly ReadOnlyPathLease Parent;
        internal WritablePathLease? Original, Prepared;
        internal bool OriginalMoved;
        internal PendingFile(string destination, ReadOnlyPathLease parent, string relativePath)
        { Destination = destination; Parent = parent; RelativePath = relativePath; }
    }
}
