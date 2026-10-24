using System.Diagnostics;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.TrayIcon.Helper.Basic;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;

namespace CDPIUI.TrayIcon.Helper;

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
    private static readonly TimeSpan ToolExitTimeout = TimeSpan.FromSeconds(15);

    private static EventWaitHandle? _shutdownEvent;
    private static RegisteredWaitHandle? _shutdownWait;

    public static void ListenForShutdown(Control invoker, System.Action exit)
    {
        try
        {
            _shutdownEvent = new EventWaitHandle(false, EventResetMode.ManualReset, ShutdownEventName);
            _shutdownWait = ThreadPool.RegisterWaitForSingleObject(_shutdownEvent, (_, _) =>
            {
                // The wait fires once; its registration is not needed after that.
                _shutdownWait?.Unregister(null);
                try
                { invoker.BeginInvoke(exit); }
                catch (Exception ex)
                {
                    Logger.Instance.CreateWarningLog(nameof(UninstallHelper), $"Cannot exit for uninstall: {ex.Message}");
                }
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
            try
            { shutdownEvent?.Reset(); }
            catch (Exception ex) { ReportCleanupFailure("Cannot reset the shutdown event", ex); }
            shutdownEvent?.Dispose();
        }
    }

    private static void WaitForTrayExit(int selfId)
    {
        var stopwatch = Stopwatch.StartNew();
        string trayName = Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "CDPIUI_TrayIcon";
        while (true)
        {
            Process[] trays = Process.GetProcessesByName(trayName);
            try
            {
                Process? tray = trays.FirstOrDefault(p => p.Id != selfId);
                if (tray == null)
                    return;

                TimeSpan remaining = TrayExitTimeout - stopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero || !tray.WaitForExit(remaining))
                    return;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                // The tray exited between the list and the wait; the list is read again.
            }
            catch (Exception ex)
            {
                // KillApplicationProcesses ends the tray then.
                ReportCleanupFailure("Cannot wait for the tray", ex);
                return;
            }
            finally
            {
                foreach (var p in trays)
                    p.Dispose();
            }
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
                // Expected for system and protected processes, and for processes that already exited.
                try
                { path = process.MainModule?.FileName; }
                catch { continue; }

                if (!ApplicationPathMatcher.BelongsToApplication(path, installDirectory))
                    continue;

                try
                {
                    process.Kill();
                    process.WaitForExit(5000);
                }
                catch (Exception ex)
                {
                    ReportCleanupFailure($"Cannot end {path}", ex);
                }
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
        RunTool(Path.Combine(Environment.SystemDirectory, "sc.exe"), arguments);
    }

    private static void RemoveHostsBlock(string installDirectory)
    {
        // From the installation directory, not from PATH or the current directory.
        string editor = Path.Combine(installDirectory, "EditHostFile.exe");
        if (!File.Exists(editor))
            return;

        RunTool(editor, "/remove");
    }

    private static void RunTool(string fileName, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process == null)
                ReportCleanupFailure($"Cannot start {fileName} {arguments}", null);
            else if (!process.WaitForExit(ToolExitTimeout))
                ReportCleanupFailure($"{fileName} {arguments} did not exit in {ToolExitTimeout.TotalSeconds} s", null);
        }
        catch (Exception ex)
        {
            ReportCleanupFailure($"Cannot run {fileName} {arguments}", ex);
        }
    }

    // Files the application creates next to the installed ones; the package does not know them,
    // so the installation directory would otherwise stay behind.
    private static readonly string[] RuntimeDirectories = ["Logs", ".update-staging", ".update-backup", ".old", ".staging", ".trust", ".session"];
    private static readonly string[] RuntimeFiles = ["update.log", ".update-manifest"];

    private static void RemoveRuntimeFiles(string installDirectory)
    {
        foreach (string name in RuntimeDirectories)
        {
            try
            {
                string path = Path.Combine(installDirectory, name);
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch (Exception ex)
            {
                ReportCleanupFailure($"Cannot delete {name}", ex);
            }
        }
        foreach (string name in RuntimeFiles)
        {
            try
            { File.Delete(Path.Combine(installDirectory, name)); }
            catch (Exception ex)
            {
                ReportCleanupFailure($"Cannot delete {name}", ex);
            }
        }
    }

    private static void RemoveAutorunTask()
    {
        try
        {
            using var taskService = new TaskService();
            taskService.RootFolder.DeleteTask(AutoStartManager.TaskName, false);
        }
        catch (Exception ex)
        {
            ReportCleanupFailure("Cannot delete the autorun task", ex);
        }
    }

    // The cleanup runs as SYSTEM: the tray logger would read the settings and write its files
    // into the system profile, and the Logs folder is deleted. Failures go to the debug output.
    private static void ReportCleanupFailure(string message, Exception? ex)
    {
        Trace.WriteLine($"[{nameof(UninstallHelper)}] [WARN] {message}{(ex == null ? "" : ": " + ex.Message)}");
    }
}
