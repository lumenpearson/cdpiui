using CDPIUI.TrayIcon.Helper;
using CDPIUI.TrayIcon.Helper.Basic;

namespace CDPIUI.TrayIcon.Forms;

/// <summary>
/// Restarts the running components after the system resumes from sleep.
/// </summary>
internal static class ComponentResumeRestarter
{
    public static async Task RestartRunningComponentsAsync()
    {
        // Network adapters come back some seconds after resume; components started
        // before that bind to no interface and the connection stays broken (issue #7).
        await WaitForNetworkAsync(TimeSpan.FromSeconds(30));
        foreach (var task in await TasksHelper.Instance.GetTasksSnapshot())
        {
            try
            {
                // Checked in the queue: a stop that came first must not be undone by the restart.
                await ComponentCommandQueue.Run(task.Id, async () =>
                {
                    if (task.ProcessManager.IsProcessRunning)
                        await TasksHelper.Instance.RestartTask(task.Id);
                });
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(EmptyForm), $"Cannot restart {task.Id} after resume: {ex.Message}");
            }
        }
    }

    private static async Task WaitForNetworkAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                    return;
            }
            catch (System.Net.NetworkInformation.NetworkInformationException) { return; }
            await Task.Delay(1000);
        }
    }
}
