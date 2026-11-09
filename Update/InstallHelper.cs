using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Update;

public class InstallHelper
{
    private static InstallHelper _instance;
    private static readonly object SyncRoot = new object();

    public static InstallHelper Instance
    {
        get
        {
            lock (SyncRoot)
            {
                if (_instance == null)
                    _instance = new InstallHelper();
                return _instance;
            }
        }
    }

    private string _archiveFilePath;
    private string _targetFolder;
    private string _targetExecutable;
    private readonly List<string> _skipFiles = new List<string> { "Update.exe", "catalog.cat" };
    private readonly Dictionary<string, byte[]> _stagedHashes = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lifecycleGate = new();
    private bool _running;

    public Action<InstallState> InstallationStateChanged;
    public Action<double> ProgressChanged;
    public Action<string> CurrentFileChanged;
    public Action<InstallError, string> ErrorOccurred;

    public InstallError FinishStatus { get; private set; } = InstallError.None;

    public InstallHelper()
    {
    }

    public void Init(string archiveFilePath, string targetFolder, string targetExecutable)
    {
        lock (_lifecycleGate)
        {
            if (_running) throw new InvalidOperationException("The running installation cannot be reconfigured.");
            _archiveFilePath = archiveFilePath;
            _targetFolder = targetFolder;
            _targetExecutable = targetExecutable;
            FinishStatus = InstallError.None;
        }
    }

    public void StartInstall()
    {
        lock (_lifecycleGate)
        {
            if (_running) return;
            _running = true;
        }
        Task.Run(async () =>
        {
            try
            {
                await RunInstallAsync();
            }
            catch (Exception ex)
            {
                Log($"Unexpected top-level error: {ex}");
                FinishStatus = InstallError.UnexpectedError;
                ErrorOccurred?.Invoke(FinishStatus, ex.Message);
                InstallationStateChanged?.Invoke(InstallState.Error);
            }
            finally { lock (_lifecycleGate) _running = false; }
        });
    }

    private async Task RunInstallAsync()
    {
        ReadOnlyPathLease packageLease;
        try
        {
            // Acquire before asking the tray to exit. Its handles may then close, but the
            // updater already owns its own read-only lease on the same package and parents.
            packageLease = ReadOnlyPathLease.OpenFile(_archiveFilePath);
        }
        catch (Exception ex)
        {
            FinishStatus = File.Exists(_archiveFilePath) ? InstallError.PermissionDenied : InstallError.FileNotFound;
            Log($"Cannot hold the update package: {ex}");
            ErrorOccurred?.Invoke(FinishStatus, ex.Message);
            InstallationStateChanged?.Invoke(InstallState.Error);
            return;
        }
        using (packageLease)
            await UpdateInstallationGate.RunAsync(_targetFolder, RunHeldInstallAsync, error =>
            {
                FinishStatus = InstallError.PermissionDenied;
                Log($"Cannot acquire the update directory lock: {error}");
                ErrorOccurred?.Invoke(FinishStatus, error.Message);
                InstallationStateChanged?.Invoke(InstallState.Error);
            });
    }

    private async Task RunHeldInstallAsync()
    {
        InstallationStateChanged?.Invoke(InstallState.Prepare);
        Log("Starting extraction process");
        if (!ApplicationProcessCloser.StopApplication())
            FinishStatus = InstallError.PermissionDenied;

        InstallationStateChanged?.Invoke(InstallState.Unpack);
        List<string> stagedFiles = FinishStatus == InstallError.None ? await StageAsync() : null;

        bool applied = false;
        if (FinishStatus == InstallError.None && stagedFiles != null)
        {
            InstallationStateChanged?.Invoke(InstallState.Copy);
            applied = ApplyStaged(stagedFiles);
        }

        if (applied)
        {
            InstallationStateChanged?.Invoke(InstallState.Finalize);
            Cleanup();
            LaunchAndExit(afterFailedUpdate: false, "Update applied successfully and application launched.");
        }
        else if (FinishStatus == InstallError.NoOldVersion)
        {
            // The rollback failed and the error box is open: the application is not
            // started, and the window closes when the user confirms the error.
            Log("Update was not applied and the previous version was not restored.");
            Cleanup();
            InstallationStateChanged?.Invoke(InstallState.Error);
        }
        else
        {
            Log("Update was not applied. The previous version is kept.");
            Cleanup();
            LaunchAndExit(afterFailedUpdate: true, successMessage: null);
        }
    }

    private void LaunchAndExit(bool afterFailedUpdate, string successMessage)
    {
        FinishStatus = InstallError.OK;
        bool launched = LaunchApplication(afterFailedUpdate);
        if (!launched)
        {
            // The window stays open with the error; it was closed at once and
            // the user never saw why the application did not start.
            InstallationStateChanged?.Invoke(InstallState.Error);
            return;
        }

        InstallationStateChanged?.Invoke(InstallState.Completed);
        ProgressChanged?.Invoke(1.0);
        if (successMessage != null)
            Log(successMessage);
        Application.Exit();
    }

    private string StagingFolder => Path.Combine(_targetFolder, ".update-staging");
    private string BackupFolder => Path.Combine(_targetFolder, ".update-backup");
    // Files written by the last update, so files dropped from a newer version can be removed.
    // A patch holds only the files that changed since the previous build, so a file that is
    // missing from it is not a removed file. Removing such files (by a manifest of the previous
    // update) deleted files that an earlier patch had brought and this one did not change.
    private string ObsoleteManifestPath => Path.Combine(_targetFolder, ".update-manifest");

    /// <summary>
    /// Extracts the update into the staging folder without touching the installed files.
    /// </summary>
    /// <returns>Relative paths of the staged files, or null if the update cannot be applied</returns>
    internal async Task<List<string>> StageAsync()
    {
        _stagedHashes.Clear();
        try
        {
            if (string.IsNullOrWhiteSpace(_archiveFilePath) || !File.Exists(_archiveFilePath))
            {
                FinishStatus = InstallError.FileNotFound;
                return null;
            }

            ArchivePaths.EnsureNoReparsePoints(_targetFolder, _targetFolder);
            if (!Directory.Exists(_targetFolder))
                ArchivePaths.EnsureDirectoryExists(_targetFolder);

            ArchivePaths.EnsureNoReparsePoints(_targetFolder, StagingFolder);
            ArchivePaths.EnsureNoReparsePoints(_targetFolder, BackupFolder);
            bool windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            if (windows)
            {
                try { WindowsFileRemoval.DeleteEmptyDirectoryIfExists(BackupFolder); }
                catch (IOException error) when ((error.HResult & 0xFFFF) == 145)
                {
                    Log("An earlier update backup is still present. Recover it before starting another update.");
                    FinishStatus = InstallError.NoOldVersion;
                    return null;
                }
            }
            else if (Directory.Exists(BackupFolder) && Directory.EnumerateFileSystemEntries(BackupFolder).Any())
            {
                Log("An earlier update backup is still present. Recover it before starting another update.");
                FinishStatus = InstallError.NoOldVersion;
                return null;
            }

            if (!DeleteDirectory(StagingFolder) || (!windows && !DeleteDirectory(BackupFolder)))
            {
                FinishStatus = InstallError.PermissionDenied;
                return null;
            }
            ArchivePaths.EnsureDirectoryExists(StagingFolder);

            using (var zip = ZipFile.OpenRead(_archiveFilePath))
            {
                // The tray checked the same limits before it staged the package. The check here
                // also covers a package that reaches the tool in another way.
                ArchivePaths.EnsureWithinLimits(zip.Entries);
                var fileEntries = GetFileEntries(zip);

                int totalFiles = fileEntries.Count;
                if (totalFiles == 0)
                {
                    Log("No files found to extract in the update archive.");
                    FinishStatus = InstallError.ExtractingFile;
                    return null;
                }

                var staged = new List<string>();
                var stagedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int extracted = 0;
                foreach (var (entry, relativePath) in fileEntries)
                {
                    string destPath;
                    string stagedPath;
                    if (!TryGetDestination(_targetFolder, relativePath, out destPath) ||
                        !TryGetDestination(StagingFolder, relativePath, out stagedPath))
                    {
                        Log($"Archive entry '{entry.FullName}' points outside of the target folder. Update aborted.");
                        FinishStatus = InstallError.ExtractingFile;
                        return null;
                    }

                    var fileName = Path.GetFileName(relativePath);
                    // The catalog of a patch lists only the files of that patch. Copied into the
                    // application folder, it no longer matched the folder after the next patch.
                    bool isCatalog = string.Equals(fileName, "catalog.cat", StringComparison.OrdinalIgnoreCase);
                    if (isCatalog || (_skipFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase) && File.Exists(destPath)))
                    {
                        Log($"Skipping {relativePath}");
                        continue;
                    }

                    CurrentFileChanged?.Invoke($"Extracting {relativePath}");
                    string canonicalPath = Path.GetRelativePath(StagingFolder, stagedPath);
                    _stagedHashes[canonicalPath] = await ExtractEntryAsync(entry, stagedPath);

                    if (stagedSet.Add(canonicalPath))
                        staged.Add(relativePath);
                    ProgressChanged?.Invoke((double)++extracted / totalFiles);
                }

                return staged;
            }
        }
        catch (UnauthorizedAccessException uaEx)
        {
            Log($"Permission denied while extracting the update: {uaEx.Message}");
            FinishStatus = InstallError.PermissionDenied;
            return null;
        }
        catch (Exception ex)
        {
            Log($"Extraction failed: {ex}");
            FinishStatus = InstallError.ExtractingFile;
            return null;
        }
    }

    private static List<(ZipArchiveEntry Entry, string RelativePath)> GetFileEntries(ZipArchive zip)
    {
        // Entries with the path relative to the installation, layer by layer: a later
        // layer overwrites the files of an earlier one.
        return GetLayerPrefixes(zip)
            .SelectMany(prefix => zip.Entries
                .Select(e => new { Entry = e, Name = e.FullName.Replace('\\', '/') })
                .Where(e => e.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !e.Name.EndsWith("/"))
                .Select(e => (e.Entry, RelativePath: e.Name.Substring(prefix.Length))))
            .ToList();
    }

    private Task<byte[]> ExtractEntryAsync(ZipArchiveEntry entry, string stagedPath)
    {
        ArchivePaths.EnsureNoReparsePoints(StagingFolder, stagedPath);
        ArchivePaths.EnsureDirectoryExists(Path.GetDirectoryName(stagedPath));
        ArchivePaths.EnsureNoReparsePoints(StagingFolder, stagedPath);
        // An entry that gives more data than its header says stops the update, as in the tray.
        return Task.Run(() =>
        {
            using var lease = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? WritablePathLease.Open(stagedPath) : null;
            using var portable = lease == null ? new FileStream(stagedPath, FileMode.Create, FileAccess.Write, FileShare.None) : null;
            var output = lease?.Stream ?? portable;
            output.SetLength(0);
            using var hash = SHA256.Create();
            using var hashing = new CryptoStream(output, hash, CryptoStreamMode.Write, leaveOpen: true);
            ArchivePaths.CopyEntryBounded(entry, hashing);
            hashing.FlushFinalBlock();
            return hash.Hash;
        });
    }

    /// <summary>
    /// Prefixes of the archive folders to apply, in order: layers/0/, layers/1/, ... in packages
    /// made by the application, or CDPIUI/ in a single published patch.
    /// </summary>
    internal static List<string> GetLayerPrefixes(ZipArchive zip) =>
        // The tray verified the catalogs of the same layers (PackageStaging): one shared rule.
        UpdatePackageLayers.GetPrefixes(zip.Entries.Select(e => e.FullName));

    /// <summary>
    /// Moves the staged files into place. Replaced files are kept in the backup folder
    /// and moved back if any file cannot be applied.
    /// </summary>
    internal bool ApplyStaged(List<string> stagedFiles)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var transaction = new UpdateFileTransaction(_targetFolder, StagingFolder, BackupFolder, _stagedHashes, Log);
            bool success = transaction.Apply(stagedFiles,
                file => CurrentFileChanged?.Invoke($"Copying {file}"),
                progress => ProgressChanged?.Invoke(progress),
                () => InstallationStateChanged?.Invoke(InstallState.Copy));
            if (!success)
            {
                FinishStatus = transaction.RollbackComplete
                    ? transaction.Failure is UnauthorizedAccessException ? InstallError.PermissionDenied : InstallError.ExtractingFile
                    : InstallError.NoOldVersion;
                if (!transaction.RollbackComplete) ErrorOccurred?.Invoke(FinishStatus, transaction.Failure?.Message);
            }
            return success;
        }
        // Relative path and whether an installed file was moved to the backup folder.
        var applied = new List<KeyValuePair<string, bool>>();
        try
        {
            // Check the complete plan before moving the first installed file.
            foreach (string relativePath in stagedFiles)
                GetCheckedPaths(relativePath, out _, out _, out _);

            for (int i = 0; i < stagedFiles.Count; i++)
            {
                string relativePath = stagedFiles[i];
                string destPath, stagedPath, backupPath;
                GetCheckedPaths(relativePath, out destPath, out stagedPath, out backupPath);

                CurrentFileChanged?.Invoke($"Copying {relativePath}");
                GetCheckedPaths(relativePath, out destPath, out stagedPath, out backupPath);
                ArchivePaths.EnsureDirectoryExists(Path.GetDirectoryName(destPath));
                GetCheckedPaths(relativePath, out destPath, out stagedPath, out backupPath);

                bool backedUp = false;
                if (File.Exists(destPath))
                {
                    ArchivePaths.EnsureDirectoryExists(Path.GetDirectoryName(backupPath));
                    GetCheckedPaths(relativePath, out destPath, out stagedPath, out backupPath);
                    File.Move(destPath, backupPath);
                    backedUp = true;
                }
                applied.Add(new KeyValuePair<string, bool>(relativePath, backedUp));

                File.Move(stagedPath, destPath);
                ProgressChanged?.Invoke((double)(i + 1) / stagedFiles.Count);
            }

            return true;
        }
        catch (Exception ex)
        {
            Log($"Failed to apply the update: {ex}");
            FinishStatus = ex is UnauthorizedAccessException ? InstallError.PermissionDenied : InstallError.ExtractingFile;

            InstallationStateChanged?.Invoke(InstallState.Copy);
            if (!Rollback(applied))
            {
                FinishStatus = InstallError.NoOldVersion;
                ErrorOccurred?.Invoke(FinishStatus, ex.Message);
            }
            return false;
        }
    }

    private void GetCheckedPaths(string relativePath, out string destination, out string staged, out string backup)
    {
        if (!TryGetDestination(_targetFolder, relativePath, out destination) ||
            !TryGetDestination(StagingFolder, relativePath, out staged) ||
            !TryGetDestination(BackupFolder, relativePath, out backup))
            throw new InvalidDataException($"Invalid update path: {relativePath}");

        ArchivePaths.EnsureNoReparsePoints(_targetFolder, destination);
        ArchivePaths.EnsureNoReparsePoints(StagingFolder, staged);
        ArchivePaths.EnsureNoReparsePoints(BackupFolder, backup);
    }

    private bool Rollback(List<KeyValuePair<string, bool>> applied)
    {
        bool restored = true;
        for (int i = applied.Count - 1; i >= 0; i--)
        {
            string relativePath = applied[i].Key;
            string destPath, backupPath;
            try
            {
                if (!TryGetDestination(_targetFolder, relativePath, out destPath) ||
                    !TryGetDestination(BackupFolder, relativePath, out backupPath))
                    throw new InvalidDataException($"Invalid rollback path: {relativePath}");
                ArchivePaths.EnsureNoReparsePoints(_targetFolder, destPath);
                ArchivePaths.EnsureNoReparsePoints(BackupFolder, backupPath);
                if (File.Exists(destPath))
                    File.Delete(destPath);

                if (applied[i].Value)
                {
                    File.Move(backupPath, destPath);
                }
                CurrentFileChanged?.Invoke($"Restored {relativePath}");
            }
            catch (Exception ex)
            {
                Log($"Failed to restore {relativePath}: {ex}");
                restored = false;
            }
        }

        Log(restored ? "Previous version restored." : "Previous version was restored only partially.");
        return restored;
    }

    private void Cleanup()
    {
        // Keep the backup if a rollback did not finish, so the files can still be recovered by hand.
        if (FinishStatus != InstallError.NoOldVersion)
            DeleteDirectory(BackupFolder);
        DeleteDirectory(StagingFolder);
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                WindowsFileRemoval.DeleteFileIfExists(ObsoleteManifestPath);
            else if (File.Exists(ObsoleteManifestPath))
                File.Delete(ObsoleteManifestPath);
        }
        catch (Exception ex)
        {
            Log($"Cannot delete the old update manifest: {ex.Message}");
        }
    }

    private bool DeleteDirectory(string path)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                WindowsFileRemoval.DeleteDirectoryIfExists(path);
                return true;
            }
            ArchivePaths.EnsureNoReparsePoints(_targetFolder, path);
            if (Directory.Exists(path))
                Directory.Delete(path, true);
            return true;
        }
        catch (Exception ex)
        {
            Log($"Failed to delete {path}: {ex.Message}");
            return false;
        }
    }

    private bool LaunchApplication(bool error = false)
    {
        try
        {
            string exePath = Path.Combine(_targetFolder, _targetExecutable);
            if (!File.Exists(exePath))
            {
                FinishStatus = InstallError.FileNotFound;
                ErrorOccurred?.Invoke(FinishStatus, $"{exePath} not found");
                return false;
            }

            string param = error ? "--after-failed-update" : "--after-patching";

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = param,
                UseShellExecute = true,
                CreateNoWindow = true,
                WorkingDirectory = _targetFolder
            };

            // The updater does not wait for the application, so only the handle is released.
            using (Process.Start(psi))
            { }
            FinishStatus = InstallError.OK;
            CurrentFileChanged?.Invoke($"Launching {_targetExecutable} {param}");
            Log($"Launching {_targetExecutable} with {param} parameter");
            return true;
        }
        catch (Exception ex)
        {
            Log($"Failed to launch application: {ex}");
            FinishStatus = InstallError.LaunchingApp;
            ErrorOccurred?.Invoke(FinishStatus, ex.Message);
            return false;
        }
    }

    internal static bool TryGetDestination(string root, string relativePath, out string destination)
    {
        bool valid = ArchivePaths.TryGetDestination(root, relativePath, out string checkedPath);
        if (valid)
        {
            string first = Path.GetRelativePath(root, checkedPath).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
            valid = !new[] { ".update-staging", ".update-backup", ".update-manifest", "update.log", UpdateInstallationGate.LockFileName }
                .Contains(first, StringComparer.OrdinalIgnoreCase);
        }
        destination = valid ? checkedPath : null;
        return valid;
    }

    private static void Log(string text) => UpdateLog.Write(text);
}
