using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CDPIUI.Shared.Security;

/// <summary>Prepares bytes under a held parent and atomically replaces one file name.</summary>
internal static class WindowsAtomicFile
{
    internal static async Task WriteAsync(string path, Func<FileStream, Task> write)
    {
        string fullPath = Path.GetFullPath(path);
        using var parent = ReadOnlyPathLease.OpenDirectoryForMutation(Path.GetDirectoryName(fullPath)!, createIfMissing: false);
        ReadOnlyPathLease? original = null;
        WritablePathLease? temporary = null;
        bool published = false;
        Exception? failure = null;
        try
        {
            try { original = ReadOnlyPathLease.OpenFileInHeldDirectory(fullPath, parent); }
            catch (FileNotFoundException) { }
            byte[]? descriptor = original == null ? null : FileAccessDescriptor.Read(original.Stream!.SafeFileHandle);
            temporary = WritablePathLease.CreateForMove(Path.Combine(Path.GetDirectoryName(fullPath)!,
                ".cdpiui-write-" + Guid.NewGuid().ToString("N") + ".tmp"), parent, descriptor);
            await write(temporary.Stream).ConfigureAwait(false);
            temporary.Stream.Flush(flushToDisk: true);
            original?.Dispose();
            original = null;
            // The destination name may have changed since capture. Replacing a late file
            // alias removes that name; it never opens or writes through its target.
            temporary.MoveTo(fullPath, parent, replace: true);
            published = true;
        }
        catch (Exception ex) { failure = ex; throw; }
        finally
        {
            var cleanupFailures = new List<Exception>();
            try { original?.Dispose(); }
            catch (Exception ex) { cleanupFailures.Add(ex); }
            if (temporary != null)
            {
                try { if (!published) temporary.DeleteOnClose(); }
                catch (Exception ex) { cleanupFailures.Add(new IOException("Cannot remove held temporary: " + temporary.CurrentPath, ex)); }
                try { temporary.Dispose(); }
                catch (Exception ex) { cleanupFailures.Add(new IOException("Cannot close held file: " + temporary.CurrentPath, ex)); }
            }
            if (cleanupFailures.Count != 0)
            {
                if (failure != null) cleanupFailures.Insert(0, failure);
                throw new AggregateException("File publication requires cleanup of the retained paths.", cleanupFailures);
            }
        }
    }
}
