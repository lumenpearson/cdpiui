using System;
using System.IO;
using System.Threading.Tasks;

namespace Update;

internal static class UpdateInstallationGate
{
    internal const string LockFileName = ".update-lock";

    internal static async Task<bool> RunAsync(string targetRoot, Func<Task> operation, Action<Exception> lockFailure)
    {
        WritablePathLease lease;
        try { lease = WritablePathLease.OpenLock(Path.Combine(targetRoot, LockFileName)); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            lockFailure(error);
            return false;
        }
        // Keep the OS lock across all asynchronous stages, including cleanup/restart.
        // Operation errors propagate unchanged; they are not lock-acquisition errors.
        using (lease) await operation();
        return true;
    }
}
