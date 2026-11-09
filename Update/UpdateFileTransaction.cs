using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Update;

/// <summary>Windows apply/rollback using verified input streams and held file identities.</summary>
internal sealed class UpdateFileTransaction
{
    private readonly string _targetRoot, _stagingRoot, _backupRoot;
    private readonly Dictionary<string, byte[]> _hashes;
    private readonly Action<string> _log;
    private readonly List<AppliedFile> _applied = new();

    private sealed class AppliedFile
    {
        public string Destination;
        public WritablePathLease Original;
        public WritablePathLease NewFile;
    }

    public Exception Failure { get; private set; }
    public bool RollbackComplete { get; private set; } = true;

    public UpdateFileTransaction(string target, string staging, string backup,
        IReadOnlyDictionary<string, byte[]> hashes, Action<string> log)
    {
        _targetRoot = Path.GetFullPath(target);
        _stagingRoot = Path.GetFullPath(staging);
        _backupRoot = Path.GetFullPath(backup);
        _hashes = hashes.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        _log = log;
    }

    public bool Apply(IReadOnlyList<string> paths, Action<string> copying, Action<double> progress, Action rollingBack)
    {
        try
        {
            using var snapshot = ReadOnlyDirectorySnapshot.Capture(_stagingRoot);
            var sources = snapshot.Files.ToDictionary(file => file.RelativePath, StringComparer.OrdinalIgnoreCase);
            var plan = new List<(string Relative, string Destination, string Backup, ReadOnlyDirectorySnapshot.FileEntry Source)>();
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string relative in paths)
            {
                string staged = ArchivePaths.GetDestinationOrThrow(_stagingRoot, relative);
                string canonical = Path.GetRelativePath(_stagingRoot, staged);
                if (!unique.Add(canonical) || !_hashes.TryGetValue(canonical, out byte[] expected) || !sources.TryGetValue(canonical, out var file))
                    throw new InvalidDataException("The staged update no longer matches its extraction plan.");
                using (var hash = SHA256.Create())
                {
                    file.Stream.Position = 0;
                    if (!expected.SequenceEqual(hash.ComputeHash(file.Stream)))
                        throw new InvalidDataException($"Staged bytes changed: {relative}");
                }
                string destination = ArchivePaths.GetDestinationOrThrow(_targetRoot, relative);
                string backup = ArchivePaths.GetDestinationOrThrow(_backupRoot, relative);
                CheckPaths(destination, backup);
                plan.Add((relative, destination, backup, file));
            }
            if (unique.Count != _hashes.Count)
                throw new InvalidDataException("The update plan omits extracted files.");

            for (int i = 0; i < plan.Count; i++)
            {
                var entry = plan[i];
                copying?.Invoke(entry.Relative);
                CheckPaths(entry.Destination, entry.Backup);
                var applied = new AppliedFile { Destination = entry.Destination };
                _applied.Add(applied);
                if (File.Exists(entry.Destination))
                {
                    applied.Original = WritablePathLease.OpenExistingForMove(entry.Destination);
                    applied.Original.MoveTo(entry.Backup);
                }

                string temporary = Path.Combine(Path.GetDirectoryName(entry.Destination), ".cdpiui-update-" + Guid.NewGuid().ToString("N") + ".tmp");
                applied.NewFile = WritablePathLease.CreateForMove(temporary);
                entry.Source.Stream.Position = 0;
                entry.Source.Stream.CopyTo(applied.NewFile.Stream);
                applied.NewFile.MoveTo(entry.Destination);
                // Keep both the installed object and its original held until every file
                // and progress callback has finished, including any rollback they trigger.
                progress?.Invoke((double)(i + 1) / plan.Count);
            }
            return true;
        }
        catch (Exception ex)
        {
            Failure = ex;
            Log($"Failed to apply the verified update: {ex}");
            try { rollingBack?.Invoke(); }
            catch (Exception callbackError) { Log($"Rollback notification failed: {callbackError}"); }
            RollbackComplete = Rollback();
            return false;
        }
        finally
        {
            foreach (var applied in _applied)
            {
                try { applied.NewFile?.Dispose(); }
                catch (Exception ex) { Log($"Cannot release installed file: {ex}"); }
                try { applied.Original?.Dispose(); }
                catch (Exception ex) { Log($"Cannot release original file: {ex}"); }
            }
        }
    }

    private void CheckPaths(string destination, string backup)
    {
        ArchivePaths.EnsureNoReparsePoints(_targetRoot, destination);
        ArchivePaths.EnsureNoReparsePoints(_backupRoot, backup);
    }

    private bool Rollback()
    {
        bool complete = true;
        for (int i = _applied.Count - 1; i >= 0; i--)
        {
            var applied = _applied[i];
            try
            {
                // Retain the destination parent while deleting this exact new object and
                // restoring its original. A changed/unsafe parent leaves the backup held.
                using var parent = ReadOnlyPathLease.OpenDirectoryForMutation(Path.GetDirectoryName(applied.Destination));
                if (applied.NewFile != null)
                {
                    applied.NewFile.DeleteOnClose();
                    applied.NewFile.Dispose();
                    applied.NewFile = null;
                }
                if (applied.Original != null && !string.Equals(applied.Original.CurrentPath, applied.Destination, StringComparison.OrdinalIgnoreCase))
                    applied.Original.MoveTo(applied.Destination);
            }
            catch (Exception ex)
            {
                complete = false;
                Log($"Cannot restore {applied.Destination}; keep its backup: {ex}");
            }
        }
        return complete;
    }

    private void Log(string message)
    {
        try { _log?.Invoke(message); }
        catch (Exception ex) { global::System.Diagnostics.Trace.WriteLine(ex); }
    }
}
