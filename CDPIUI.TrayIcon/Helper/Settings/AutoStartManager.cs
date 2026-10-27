using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CDPIUI.Shared.Security;
using CDPIUI.TrayIcon.Helper.Basic;
using Microsoft.Win32.TaskScheduler;

namespace CDPIUI.TrayIcon.Helper;

public static class AutoStartManager
{
    internal const string TaskName = "CDPIUI_Autostart";

    private const string AutorunArgument = "--autorun";

    // The task change at startup runs on a pool thread; a pipe request must not interleave with it.
    private static readonly object SyncRoot = new object();

    public static bool AddToAutorun()
    {
        try
        {
            string? executablePath = Environment.ProcessPath;
            if (executablePath == null)
                return false;

            // The task starts the launcher, which removes the .NET variables of the user
            // profile before it starts the tray.
            string launcherPath = GetLauncherPath(executablePath);
            if (!File.Exists(launcherPath))
            {
                Logger.Instance.CreateErrorLog(nameof(AutoStartManager), $"Autorun error: {launcherPath} does not exist.");
                return false;
            }

            lock (SyncRoot)
            using (TaskService taskService = new TaskService())
            {
                Microsoft.Win32.TaskScheduler.Task existingTask = taskService.GetTask(TaskName);
                if (existingTask != null)
                {
                    taskService.RootFolder.DeleteTask(TaskName);
                }

                TaskDefinition taskDefinition = taskService.NewTask();
                taskDefinition.RegistrationInfo.Description = "Starting CDPI UI with highest rights when user logs in.";
                taskDefinition.RegistrationInfo.Author = "CDPI UI Tray Icon";

                LogonTrigger trigger = new LogonTrigger
                {
                    UserId = System.Security.Principal.WindowsIdentity.GetCurrent().Name,
                };
                taskDefinition.Triggers.Add(trigger);

                taskDefinition.Actions.Add(new ExecAction(launcherPath,
                    ElevatedLauncherCommand.GetArguments(ElevatedLauncherCommand.TrayTarget, AutorunArgument), null));

                taskDefinition.Principal.RunLevel = TaskRunLevel.Highest;

                taskDefinition.Principal.UserId = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
                taskDefinition.Principal.LogonType = TaskLogonType.InteractiveToken;

                taskDefinition.Settings.DisallowStartIfOnBatteries = false;
                taskDefinition.Settings.StopIfGoingOnBatteries = false;

                taskDefinition.Settings.ExecutionTimeLimit = TimeSpan.Zero;

                taskService.RootFolder.RegisterTaskDefinition(TaskName, taskDefinition);
            }
            return true;
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(AutoStartManager), $"Autorun error: {ex.Message}");
        }
        return false;
    }

    /// <summary>
    /// Changes a task that an earlier version made to start the tray directly: it then starts
    /// the tray through the launcher. Other settings of the task do not change.
    /// </summary>
    public static void MoveTaskToLauncher()
    {
        try
        {
            string? executablePath = Environment.ProcessPath;
            if (executablePath == null)
                return;
            string launcherPath = GetLauncherPath(executablePath);
            if (!File.Exists(launcherPath))
                return;

            lock (SyncRoot)
            {
                MoveTaskActions(executablePath, launcherPath);
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(AutoStartManager), $"Cannot change the autorun task: {ex.Message}");
        }
    }

    private static void MoveTaskActions(string executablePath, string launcherPath)
    {
        using TaskService taskService = new TaskService();
        using Microsoft.Win32.TaskScheduler.Task? task = taskService.GetTask(TaskName);
        if (task == null)
            return;

        bool changed = false;
        foreach (Microsoft.Win32.TaskScheduler.Action action in task.Definition.Actions)
        {
            // Only an action of this tray: a task of a copy in another folder stays as it is.
            if (action is ExecAction execAction && IsSamePath(execAction.Path, executablePath))
            {
                execAction.Path = launcherPath;
                execAction.Arguments = ElevatedLauncherCommand.GetArguments(
                    ElevatedLauncherCommand.TrayTarget, execAction.Arguments ?? string.Empty);
                changed = true;
            }
        }
        if (changed)
        {
            task.RegisterChanges();
            Logger.Instance.CreateInfoLog(nameof(AutoStartManager), "The autorun task now starts the tray through the launcher.");
        }
    }

    private static string GetLauncherPath(string trayPath)
    {
        return ElevatedLauncherCommand.GetPath(Path.GetDirectoryName(trayPath) ?? string.Empty);
    }

    private static bool IsSamePath(string? taskPath, string path)
    {
        if (string.IsNullOrWhiteSpace(taskPath))
            return false;
        return string.Equals(Path.GetFullPath(taskPath.Trim().Trim('"')), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
    }

    public static bool RemoveFromAutorun()
    {
        try
        {
            lock (SyncRoot)
            using (TaskService taskService = new TaskService())
            {
                taskService.RootFolder.DeleteTask(TaskName, false);
            }
            return true;
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(AutoStartManager), $"Autorun error: {ex}");
        }
        return false;
    }
}
