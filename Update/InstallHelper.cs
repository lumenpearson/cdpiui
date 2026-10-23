using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
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
                    CloseProcessIfRunning("goodbyeDPI");
                    CloseProcessIfRunning("CDPIUI");
                    CloseProcessIfRunning("CDPIUI_TrayIcon");

                    InstallationStateChanged?.Invoke(InstallState.Unpack);
                    List<string> stagedFiles = await StageAsync();

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
                            InstallationStateChanged?.Invoke(InstallState.Error);
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
                            InstallationStateChanged?.Invoke(InstallState.Error);
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
                    var fileEntries = zip.Entries
                        .Where(e =>
                        {
                            var name = e.FullName.Replace('\\', '/');
                            return name.StartsWith(UiRootPrefix, StringComparison.OrdinalIgnoreCase) && !name.EndsWith("/");
                        })
                        .ToList();

                    int totalFiles = fileEntries.Count;
                    if (totalFiles == 0)
                    {
                        Log($"No files found to extract in archive under '{UiRootPrefix}'.");
                        FinishStatus = InstallError.ExtractingFile;
                        return null;
                    }

                    var staged = new List<string>();
                    foreach (var entry in fileEntries)
                    {
                        var relativePath = entry.FullName.Replace('\\', '/').Substring(UiRootPrefix.Length);
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

                        staged.Add(relativePath);
                        ProgressChanged?.Invoke((double)staged.Count / totalFiles);
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