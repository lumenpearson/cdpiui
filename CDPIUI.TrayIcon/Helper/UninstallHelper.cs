using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.TrayIcon.Helper.Basic;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using System.Diagnostics;

namespace CDPIUI.TrayIcon.Helper
{
    /// <summary>
    /// Shuts the application down before the MSI package removes its files
    /// </summary>
    public static class UninstallHelper
    {
        public const string CleanupArgument = "--uninstall-cleanup";

        // Set by the uninstaller; every running tray then exits as if Exit was chosen in its menu,
        // which stops the components and restores the system proxy settings.
        private const string ShutdownEventName = @"Global\CDPIUI_TrayShutdown";
        private static readonly TimeSpan TrayExitTimeout = TimeSpan.FromSeconds(20);

        private static EventWaitHandle? _shutdownEvent;
        private static RegisteredWaitHandle? _shutdownWait;

        public static void ListenForShutdown(Control invoker, System.Action exit)
        {
            try
            {
                _shutdownEvent = new EventWaitHandle(false, EventResetMode.ManualReset, ShutdownEventName);
                _shutdownWait = ThreadPool.RegisterWaitForSingleObject(_shutdownEvent, (_, _) =>
                {
                    try { invoker.BeginInvoke(exit); } catch { }
                }, null, Timeout.Infinite, executeOnlyOnce: true);
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateWarningLog(nameof(UninstallHelper), $"Cannot listen for uninstall: {ex.Message}");
            }
        }

        /// <summary>
        /// Runs from the uninstaller as SYSTEM. Does not touch settings or logs, because they would be
        /// created in the system profile.
        /// </summary>
        public static void Cleanup()
        {
            string installDirectory = AppContext.BaseDirectory;
            int selfId = Environment.ProcessId;

            EventWaitHandle? shutdownEvent = null;
            try
            {
                if (EventWaitHandle.TryOpenExisting(ShutdownEventName, out shutdownEvent))
                {
                    shutdownEvent.Set();
                    WaitForTrayExit(selfId);
                }

                KillApplicationProcesses(installDirectory, selfId);
                RemoveDrivers(installDirectory);
                RemoveAutorunTask();
                RemoveHostsBlock(installDirectory);
                RemoveRuntimeFiles(installDirectory);
            }
            finally
            {
                try { shutdownEvent?.Reset(); } catch { }
                shutdownEvent?.Dispose();
            }
        }

        private static void WaitForTrayExit(int selfId)
        {
            var stopwatch = Stopwatch.StartNew();
            string trayName = Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "CDPIUI_TrayIcon";
            while (stopwatch.Elapsed < TrayExitTimeout)
            {
                Process[] trays = Process.GetProcessesByName(trayName);
                bool running = trays.Any(p => p.Id != selfId);
                foreach (var p in trays) p.Dispose();
                if (!running) return;

                Thread.Sleep(250);
            }
        }

        private static void KillApplicationProcesses(string installDirectory, int selfId)
        {
            foreach (Process process in Process.GetProcesses())
            {
                using (process)
                {
                    if (process.Id == selfId)
                        continue;

                    string? path;
                    try { path = process.MainModule?.FileName; }
                    catch { continue; }

                    if (!ApplicationPathMatcher.BelongsToApplication(path, installDirectory))
                        continue;

                    try
                    {
                        process.Kill();
                        process.WaitForExit(5000);
                    }
                    catch { }
                }
            }
        }

        private static void RemoveDrivers(string installDirectory)
        {
            using RegistryKey? services = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services");
            if (services == null)
                return;

            foreach (string name in services.GetSubKeyNames())
            {
                if (!name.StartsWith("WinDivert", StringComparison.OrdinalIgnoreCase))
                    continue;

                string? imagePath;
                using (RegistryKey? service = services.OpenSubKey(name))
                {
                    imagePath = service?.GetValue("ImagePath") as string;
                }

                // A driver installed by other software stays.
                if (!ApplicationPathMatcher.BelongsToApplication(imagePath, installDirectory))
                    continue;

                RunServiceControl($"stop \"{name}\"");
                RunServiceControl($"delete \"{name}\"");
            }
        }

        private static void RunServiceControl(string arguments)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "sc.exe"), arguments)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                process?.WaitForExit(15000);
            }
            catch { }
        }

        private static void RemoveHostsBlock(string installDirectory)
        {
            string editor = Path.Combine(installDirectory, "EditHostFile.exe");
            if (!File.Exists(editor))
                return;

            try
            {
                using var process = Process.Start(new ProcessStartInfo(editor, "/remove")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                process?.WaitForExit(15000);
            }
            catch { }
        }

        // Files the application creates next to the installed ones; the package does not know them,
        // so the installation directory would otherwise stay behind.
        private static readonly string[] RuntimeDirectories = ["Logs", ".update-staging", ".update-backup", ".old"];
        private static readonly string[] RuntimeFiles = ["update.log", ".update-manifest"];

        private static void RemoveRuntimeFiles(string installDirectory)
        {
            foreach (string name in RuntimeDirectories)
            {
                try
                {
                    string path = Path.Combine(installDirectory, name);
                    if (Directory.Exists(path)) Directory.Delete(path, true);
                }
                catch { }
            }
            foreach (string name in RuntimeFiles)
            {
                try { File.Delete(Path.Combine(installDirectory, name)); }
                catch { }
            }
        }

        private static void RemoveAutorunTask()
        {
            try
            {
                using var taskService = new TaskService();
                taskService.RootFolder.DeleteTask(AutoStartManager.TaskName, false);
            }
            catch { }
        }
    }
}
