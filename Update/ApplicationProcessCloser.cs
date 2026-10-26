using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Update;

/// <summary>
/// Closes the running application before its files are replaced.
/// </summary>
internal static class ApplicationProcessCloser
{
    private static readonly string[] ApplicationProcesses = { "goodbyeDPI", "CDPIUI", "CDPIUI_TrayIcon" };
    private const string TrayShutdownEventName = @"Global\CDPIUI_TrayShutdown";

    /// <summary>
    /// Closes the application and checks that no process that holds its files is left
    /// </summary>
    public static bool StopApplication()
    {
        EventWaitHandle shutdownEvent = null;
        try
        {
            // Let the tray exit as from its menu first: it then stops the components and
            // restores the system proxy settings, which a killed tray leaves behind.
            bool opened;
            try
            { opened = EventWaitHandle.TryOpenExisting(TrayShutdownEventName, out shutdownEvent); }
            catch (Exception ex)
            {
                UpdateLog.Write($"Cannot ask the tray to exit: {ex.Message}");
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
                    UpdateLog.Write($"{name}.exe is still running. Update aborted.");
                    return false;
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            UpdateLog.Write($"Cannot close the application: {ex}");
            return false;
        }
        finally
        {
            try
            { shutdownEvent?.Reset(); }
            catch (Exception ex)
            {
                // The event belongs to the tray. If it cannot be reset, the next tray creates it again.
                UpdateLog.Write($"Cannot reset the tray shutdown event: {ex.Message}");
            }
            shutdownEvent?.Dispose();
        }
    }

    private static bool WaitForExit(string processName, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        // An exited process can stay in the list for a moment while handles to it are open.
        var exitedIds = new HashSet<int>();
        while (true)
        {
            Process[] processes = Process.GetProcessesByName(processName);
            try
            {
                var running = processes.Where(p => !exitedIds.Contains(p.Id)).ToList();
                if (running.Count == 0)
                    return true;

                bool waited = false;
                foreach (var process in running)
                {
                    TimeSpan remaining = timeout - stopwatch.Elapsed;
                    if (remaining <= TimeSpan.Zero)
                        return false;

                    // Wait on the process handle: the wait ends as soon as the process exits.
                    try
                    {
                        if (process.WaitForExit((int)Math.Ceiling(remaining.TotalMilliseconds)))
                            exitedIds.Add(process.Id);
                        waited = true;
                    }
                    catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
                    {
                        // No access to the handle, or the process exited before the wait.
                    }
                }

                // Without access to any handle the only way left is to check the list again.
                if (!waited)
                {
                    TimeSpan remaining = timeout - stopwatch.Elapsed;
                    if (remaining <= TimeSpan.Zero)
                        return false;
                    Thread.Sleep(Math.Min(250, (int)Math.Ceiling(remaining.TotalMilliseconds)));
                }
            }
            finally
            {
                foreach (var process in processes)
                    process.Dispose();
            }
        }
    }

    private static void CloseProcessIfRunning(string processNameWithoutExt)
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
                    UpdateLog.Write($"process {processNameWithoutExt}.exe terminated (pid {p.Id})");
                }
                catch (Exception ex)
                {
                    // The process can exit before the kill, or it can belong to another user.
                    // StopApplication checks after this that no process is left.
                    UpdateLog.Write($"Cannot terminate {processNameWithoutExt}.exe: {ex.Message}");
                }
                finally
                {
                    p.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            UpdateLog.Write($"Error while trying to close process {processNameWithoutExt}: {ex}");
        }
    }
}
