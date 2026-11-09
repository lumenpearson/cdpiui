using System;
using System.Collections.Generic;
using System.IO;

namespace CDPIUI.Shared.Security;

/// <summary>Recoverable exact-directory name replacement; content verification is the caller's responsibility.</summary>
public static class WindowsDirectoryTransaction
{
    /// <summary>
    /// Keeps both root identities through rename and rollback. Does not freeze child bytes
    /// or provide a crash journal. Verified no-delete snapshots are intentionally incompatible
    /// with this namespace switch; use VerifiedFilePromotion to consume such snapshots.
    /// </summary>
    public static void Replace(string source, string destination, Action? published = null, Action<string, Exception>? cleanupFailed = null)
    {
        source = DirectoryNameLease.NormalizeNonRootPath(source);
        destination = DirectoryNameLease.NormalizeNonRootPath(destination);
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase) ||
            source.StartsWith(destination + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            destination.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Replacement directories must be distinct and must not contain each other.");
        using var prepared = DirectoryNameLease.Open(source);
        DirectoryNameLease? original = null;
        try
        {
            try { original = DirectoryNameLease.Open(destination); }
            catch (DirectoryNotFoundException) { }
            catch (FileNotFoundException) { }
            try
            {
                original?.MoveTo(destination + ".previous-" + Guid.NewGuid().ToString("N"));
                prepared.MoveTo(destination);
                published?.Invoke();
            }
            catch (Exception failure)
            {
                var recoveryFailures = new List<Exception>();
                // A native rename can succeed before its following anchor check fails;
                // CurrentPath records that change before any subsequent fallible work.
                if (!string.Equals(prepared.CurrentPath, source, StringComparison.OrdinalIgnoreCase))
                {
                    try { prepared.MoveTo(source); }
                    catch (Exception ex) { recoveryFailures.Add(new IOException("New directory retained for recovery: " + prepared.CurrentPath, ex)); }
                }
                if (original != null && !string.Equals(original.CurrentPath, destination, StringComparison.OrdinalIgnoreCase))
                {
                    try { original.MoveTo(destination); }
                    catch (Exception ex) { recoveryFailures.Add(new IOException("Original retained for recovery: " + original.CurrentPath + "; destination: " + destination, ex)); }
                }
                if (recoveryFailures.Count != 0)
                {
                    recoveryFailures.Insert(0, failure);
                    throw new AggregateException("Directory publication requires recovery from the retained paths.", recoveryFailures);
                }
                throw;
            }
            if (original != null)
            {
                try { original.DeleteTree(); }
                catch (Exception ex)
                {
                    if (cleanupFailed == null) throw new IOException("New directory is installed; previous directory retained: " + original.CurrentPath, ex);
                    cleanupFailed(original.CurrentPath, ex);
                }
            }
        }
        finally { original?.Dispose(); }
    }
}
