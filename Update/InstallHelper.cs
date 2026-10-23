using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Update
{
    public enum InstallState
    {
        None,
        Prepare,
        Unpack,
        Copy,
        Finalize,
        Completed,
        Error
    }

    public enum InstallError
    {
        None,
        PermissionDenied,
        FileNotFound,
        ExtractingFile,
        UnexpectedError,
        LaunchingApp,
        NoOldVersion,
        OK
    }

    public class InstallHelper
    {
        private static InstallHelper _instance;
        private static readonly object _lock = new object();

        public static InstallHelper Instance
        {
            get
            {
                lock (_lock)
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
        private const string UiRootPrefix = "CDPIUI/";
        private const string LayersPrefix = "layers/";

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
            _archiveFilePath = archiveFilePath;
            _targetFolder = targetFolder;
            _targetExecutable = targetExecutable;
            FinishStatus = InstallError.None;
        }

        public void StartInstall()
        {
            Task.Run(async () =>
            {
                try
                {
                    InstallationStateChanged?.Invoke(InstallState.Prepare);
                    Log("Starting extraction process");
                    if (!StopApplication())
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
                        FinishStatus = InstallError.OK;
                        bool launched = LaunchApplication(false);
                        if (launched)
                        {
                            InstallationStateChanged?.Invoke(InstallState.Completed);
                            ProgressChanged?.Invoke(1.0);
                            Log("Update applied successfully and application launched.");
                        }
                        else
                        {
                            // The window stays open with the error; it was closed at once and
                            // the user never saw why the application did not start.
                            InstallationStateChanged?.Invoke(InstallState.Error);
                            return;
                        }
                        Application.Exit();
                    }
                    else
                    {
                        Log("Update was not applied. The previous version is kept.");
                        Cleanup();
                        FinishStatus = InstallError.OK;
                        bool launched = LaunchApplication(true);
                        if (launched)
                        {
                            InstallationStateChanged?.Invoke(InstallState.Completed);
                            ProgressChanged?.Invoke(1.0);
                        }
                        else
                        {
                            // The window stays open with the error; it was closed at once and
                            // the user never saw why the application did not start.
                            InstallationStateChanged?.Invoke(InstallState.Error);
                            return;
                        }

                        Application.Exit();
                    }
                }
                catch (Exception ex)
                {
                    Log($"Unexpected top-level error: {ex}");
                    FinishStatus = InstallError.UnexpectedError;
                    ErrorOccurred?.Invoke(FinishStatus, ex.Message);
                    InstallationStateChanged?.Invoke(InstallState.Error);
                }
            });
        }

        private string StagingFolder => Path.Combine(_targetFolder, ".update-staging");
        private string BackupFolder => Path.Combine(_targetFolder, ".update-backup");
        // Files written by the last update, so files dropped from a newer version can be removed.
        // A patch holds only the files that changed since the previous build, so a file that is
        // missing from it is not a removed file. Removing such files (by a manifest of the previous
        // update) deleted files that an earlier patch had brought and this one did not change.
        private string ObsoleteManifestPath => Path.Combine(_targetFolder, ".update-manifest");

        private static readonly string[] ApplicationProcesses = { "goodbyeDPI", "CDPIUI", "CDPIUI_TrayIcon" };
        private const string TrayShutdownEventName = @"Global\CDPIUI_TrayShutdown";

        /// <summary>
        /// Closes the application and checks that no process that holds its files is left
        /// </summary>
        private bool StopApplication()
        {
            EventWaitHandle shutdownEvent = null;
            try
            {
                // Let the tray exit as from its menu first: it then stops the components and
                // restores the system proxy settings, which a killed tray leaves behind.
                bool opened;
                try { opened = EventWaitHandle.TryOpenExisting(TrayShutdownEventName, out shutdownEvent); }
                catch (Exception ex)
                {
                    Log($"Cannot ask the tray to exit: {ex.Message}");
                    opened = false;
                }
                if (opened)
                {
                    shutdownEvent.Set();
                    WaitForExit("CDPIUI_TrayIcon", TimeSpan.FromSeconds(20));
                }

                foreach (string name in ApplicationProcesses)
                    CloseProcessIfRunning(name);

                foreach (string name in ApplicationProcesses)
                {
                    if (!WaitForExit(name, TimeSpan.FromSeconds(10)))
                    {
                        Log($"{name}.exe is still running. Update aborted.");
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Log($"Cannot close the application: {ex}");
                return false;
            }
            finally
            {
                try { shutdownEvent?.Reset(); } catch { }
                shutdownEvent?.Dispose();
            }
        }

        private static bool WaitForExit(string processName, TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                Process[] processes = Process.GetProcessesByName(processName);
                bool running = processes.Length > 0;
                foreach (var process in processes) process.Dispose();

                if (!running) return true;
                if (stopwatch.Elapsed > timeout) return false;
                Thread.Sleep(250);
            }
        }

        private void CloseProcessIfRunning(string processNameWithoutExt)
        {
            try
            {
                var procs = Process.GetProcessesByName(processNameWithoutExt);
                foreach (var p in procs)
                {
                    try
                    {
                        p.Kill();
                        p.WaitForExit(2000);
                        Log($"process {processNameWithoutExt}.exe terminated (pid {p.Id})");
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Log($"Error while trying to close process {processNameWithoutExt}: {ex}");
            }
        }

        /// <summary>
        /// Extracts the update into the staging folder without touching the installed files.
        /// </summary>
        /// <returns>Relative paths of the staged files, or null if the update cannot be applied</returns>
        private async Task<List<string>> StageAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_archiveFilePath) || !File.Exists(_archiveFilePath))
                {
                    FinishStatus = InstallError.FileNotFound;
                    return null;
                }

                if (!Directory.Exists(_targetFolder))
                    Directory.CreateDirectory(_targetFolder);

                DeleteDirectory(StagingFolder);
                DeleteDirectory(BackupFolder);
                Directory.CreateDirectory(StagingFolder);

                using (var zip = ZipFile.OpenRead(_archiveFilePath))
                {
                    // Entries with the path relative to the installation, layer by layer: a later
                    // layer overwrites the files of an earlier one.
                    var fileEntries = GetLayerPrefixes(zip)
                        .SelectMany(prefix => zip.Entries
                            .Select(e => new { Entry = e, Name = e.FullName.Replace('\\', '/') })
                            .Where(e => e.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !e.Name.EndsWith("/"))
                            .Select(e => new { e.Entry, RelativePath = e.Name.Substring(prefix.Length) }))
                        .ToList();

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
                    foreach (var item in fileEntries)
                    {
                        var entry = item.Entry;
                        var relativePath = item.RelativePath;
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
                        if (_skipFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase) && File.Exists(destPath))
                        {
                            Log($"Skipping {relativePath}");
                            continue;
                        }

                        CurrentFileChanged?.Invoke($"Extracting {relativePath}");
                        Directory.CreateDirectory(Path.GetDirectoryName(stagedPath));
                        using (var stream = entry.Open())
                        using (var outFile = new FileStream(stagedPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            await stream.CopyToAsync(outFile);
                        }

                        if (stagedSet.Add(relativePath))
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

        /// <summary>
        /// Prefixes of the archive folders to apply, in order: layers/0/, layers/1/, ... in packages
        /// made by the application, or CDPIUI/ in a single published patch.
        /// </summary>
        private static List<string> GetLayerPrefixes(ZipArchive zip)
        {
            var layers = zip.Entries
                .Select(e => e.FullName.Replace('\\', '/'))
                .Where(name => name.StartsWith(LayersPrefix, StringComparison.OrdinalIgnoreCase))
                .Select(name => name.Substring(LayersPrefix.Length).Split('/')[0])
                .Distinct()
                .Select(index => { int number; return int.TryParse(index, out number) ? number : -1; })
                .Where(number => number >= 0)
                .OrderBy(number => number)
                .Select(number => $"{LayersPrefix}{number}/")
                .ToList();

            return layers.Count > 0 ? layers : new List<string> { UiRootPrefix };
        }

        /// <summary>
        /// Moves the staged files into place. Replaced files are kept in the backup folder
        /// and moved back if any file cannot be applied.
        /// </summary>
        private bool ApplyStaged(List<string> stagedFiles)
        {
            // Relative path and whether an installed file was moved to the backup folder.
            var applied = new List<KeyValuePair<string, bool>>();
            try
            {
                for (int i = 0; i < stagedFiles.Count; i++)
                {
                    string relativePath = stagedFiles[i];
                    string destPath, stagedPath, backupPath;
                    TryGetDestination(_targetFolder, relativePath, out destPath);
                    TryGetDestination(StagingFolder, relativePath, out stagedPath);
                    TryGetDestination(BackupFolder, relativePath, out backupPath);

                    CurrentFileChanged?.Invoke($"Copying {relativePath}");
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath));

                    bool backedUp = false;
                    if (File.Exists(destPath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
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

        private bool Rollback(List<KeyValuePair<string, bool>> applied)
        {
            bool restored = true;
            for (int i = applied.Count - 1; i >= 0; i--)
            {
                string relativePath = applied[i].Key;
                string destPath, backupPath;
                TryGetDestination(_targetFolder, relativePath, out destPath);
                TryGetDestination(BackupFolder, relativePath, out backupPath);
                try
                {
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
                if (File.Exists(ObsoleteManifestPath))
                    File.Delete(ObsoleteManifestPath);
            }
            catch (Exception ex)
            {
                Log($"Cannot delete the old update manifest: {ex.Message}");
            }
        }

        private void DeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch (Exception ex)
            {
                Log($"Failed to delete {path}: {ex.Message}");
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

                Process.Start(psi);
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

        private static bool TryGetDestination(string root, string relativePath, out string destination)
        {
            destination = null;
            if (string.IsNullOrEmpty(relativePath) || Path.IsPathRooted(relativePath) || relativePath.IndexOf(':') >= 0)
                return false;

            string fullRoot = Path.GetFullPath(root);
            if (!fullRoot.EndsWith(Path.DirectorySeparatorChar.ToString()))
                fullRoot += Path.DirectorySeparatorChar;

            string fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                return false;

            destination = fullPath;
            return true;
        }

        private void Log(string text)
        {
            try
            {
                string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {text}{Environment.NewLine}";
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update.log"), logLine);
            }
            catch
            {
                // pass
            }
        }
    }
}