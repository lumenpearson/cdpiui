using CDPIUI.TrayIcon.Helper;

namespace CDPIUI.TrayIcon.Forms;

/// <summary>
/// The icon and the tool tip of the tray for the components that run now.
/// </summary>
internal static class TrayStatusText
{
    public static string GetCurrentIcon()
    {
        int rt = 0;
        // Tasks are added from pool threads while the icon is updated.
        var tasks = TasksHelper.Instance.GetTasksSnapshotSync();

        foreach (var task in tasks)
        {
            if (task.ProcessManager.IsProcessRunning)
            {
                rt++;
            }
        }

        if (rt == 0)
        {
            return "trayLogoStopped";
        }
        else if (rt == tasks.Count)
        {
            return "trayLogoStarted";
        }
        else
        {
            return "trayLogoStartedNotAll";
        }
    }

    public static string GetNowRunningComponentsString()
    {
        string result = string.Empty;
        int cnt = 0;
        foreach (var task in TasksHelper.Instance.GetTasksSnapshotSync())
        {
            if (task.ProcessManager.IsProcessRunning)
            {
                result += $"{LocaleHelper.GetLocalizedComponentName(task.Id)}, ";
                cnt++;
            }
        }
        result = result.Length > 2 ? result[..^2] : result;
        if (cnt == 0)
            return LocaleHelper.GetLocaleString("AllStopped");
        return string.Format(cnt > 1 ? LocaleHelper.GetLocaleString("StartedNowS") : LocaleHelper.GetLocaleString("StartedNow"), result);
    }
}
