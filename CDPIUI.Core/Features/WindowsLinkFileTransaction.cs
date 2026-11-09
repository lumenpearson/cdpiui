using System.Diagnostics;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.Shared.Security;

namespace CDPIUI.Core.Features;

/// <summary>Publishes and restores held file names without writing through file links.</summary>
internal static class WindowsLinkFileTransaction
{
    internal static string? Replace(string destination, bool replacingLink, Action<string> createLink, string? expectedTarget, bool symbolic, string? previousTarget)
    {
        destination = CheckedDestination(destination);
        using var parent = ReadOnlyPathLease.OpenDirectoryForMutation(Path.GetDirectoryName(destination)!, createIfMissing: false);
        string temporary = $"{destination}.{Guid.NewGuid():N}.link";
        FileNameLease? prepared = null, displaced = null;
        bool validated = false, published = false;
        string? backup = null;
        try
        {
            createLink(temporary);
            prepared = FileNameLease.OpenExisting(temporary);
            RequireTarget(prepared, expectedTarget, symbolic);
            validated = true;
            displaced = TryOpen(destination);
            if (displaced != null)
            {
                if (replacingLink && previousTarget != null) RequireTarget(displaced, previousTarget, displaced.IsReparsePoint);
                replacingLink |= displaced.IsReparsePoint;
                backup = replacingLink ? $"{destination}.{Guid.NewGuid():N}.previous" : destination + ".bak";
                displaced.MoveTo(backup);
            }
            try
            {
                prepared.MoveTo(destination);
                published = true;
            }
            catch (Exception publishError)
            {
                if (displaced != null)
                {
                    try { displaced.MoveTo(destination); }
                    catch (Exception restoreError)
                    {
                        throw new IOException($"Cannot restore {destination}. The displaced file is kept at {displaced.CurrentPath}.",
                            new AggregateException(publishError, restoreError));
                    }
                }
                throw;
            }
            return replacingLink ? null : backup;
        }
        finally
        {
            // Creation/capture/validation failure proves no ownership of the current
            // temporary name. Never look it up again merely to delete whatever is there.
            if (prepared != null)
            {
                if (!published && validated) DeleteTemporary(prepared);
                prepared.Dispose();
            }
            if (displaced != null)
            {
                if (published && replacingLink) DeleteTemporary(displaced);
                displaced.Dispose();
            }
            if (!published && !validated) Trace.WriteLine($"[LinkFileTransaction] Unconfirmed temporary name kept at {temporary}.");
        }
    }

    internal static void Restore(string destination, string? backup, string? expectedTarget)
    {
        destination = CheckedDestination(destination);
        if (backup != null && !string.Equals(Path.GetFullPath(backup), destination + ".bak", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("The backup must be the original file's adjacent .bak name.");
        using var parent = ReadOnlyPathLease.OpenDirectoryForMutation(Path.GetDirectoryName(destination)!, createIfMissing: false);
        // Hold the backup before changing any current name. A missing or busy backup
        // cannot remove the link or a user's replacement of it.
        using var original = backup == null ? null : FileNameLease.OpenExisting(backup);
        if (original?.IsReparsePoint == true) throw new UnauthorizedAccessException("A symbolic backup is not an original file.");
        using var linked = TryOpen(destination);
        if (linked != null) RequireTarget(linked, expectedTarget, linked.IsReparsePoint);
        if (original == null)
        {
            linked?.DeleteOnClose();
            return;
        }
        bool restored = false;
        if (linked != null) linked.MoveTo($"{destination}.{Guid.NewGuid():N}.previous");
        try
        {
            original.MoveTo(destination);
            restored = true;
        }
        catch (Exception restoreError)
        {
            if (linked != null)
            {
                try { linked.MoveTo(destination); }
                catch (Exception rollbackError)
                {
                    throw new IOException($"Cannot restore {destination}. The link is kept at {linked.CurrentPath}; backup at {original.CurrentPath}.",
                        new AggregateException(restoreError, rollbackError));
                }
            }
            throw;
        }
        finally { if (restored && linked != null) DeleteTemporary(linked); }
    }

    private static void RequireTarget(FileNameLease link, string? expectedTarget, bool symbolic)
    {
        // The generic internal transaction can also publish ordinary prepared files.
        // FileSystemLinksManager always supplies the requested link target.
        if (expectedTarget == null) return;
        if (string.IsNullOrWhiteSpace(expectedTarget)) throw new ArgumentException("A link target is required.", nameof(expectedTarget));
        if (symbolic) link.RequireSymbolicTarget(expectedTarget);
        else link.RequireHardLinkTarget(expectedTarget);
    }

    private static string CheckedDestination(string destination)
    {
        // Validate the raw leaf before a creator gets an opportunity to write an ADS
        // or another Win32 alias. Parent validation is performed by the directory lease.
        string full = Path.GetFullPath(destination);
        return ArchivePaths.GetDestinationOrThrow(Path.GetDirectoryName(full)!, Path.GetFileName(destination));
    }

    private static FileNameLease? TryOpen(string path)
    {
        try { return FileNameLease.OpenExisting(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    private static void DeleteTemporary(FileNameLease temporary)
    {
        try { temporary.DeleteOnClose(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { Trace.WriteLine($"[LinkFileTransaction] Cannot delete held temporary file {temporary.CurrentPath}: {error.Message}"); }
    }
}
