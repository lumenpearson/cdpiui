using CDPIUI.Shared.ConditionalLaunch;
using CDPIUI.TrayIcon.Helper;
using CDPIUI.TrayIcon.Helper.Basic;

namespace CDPIUI.TrayIcon.ConditionalLaunch;

/// <summary>
/// Runs conditional tasks, each one at most once at a time, and reports a completed run.
/// </summary>
internal sealed class ConditionalTaskRunner
{
    // The lock of the engine, which also guards the other state of the engine.
    private readonly object _sync;
    private readonly HashSet<string> _runningTasks = new(StringComparer.OrdinalIgnoreCase);

    public ConditionalTaskRunner(object sync)
    {
        _sync = sync;
    }

    public void RunHighestPriority(IReadOnlyCollection<ConditionalTask> tasks)
    {
        var highestPriority = tasks.Max(task => (int)task.Priority);
        foreach (var task in tasks.Where(task => (int)task.Priority == highestPriority))
            _ = RunOnceAsync(task);
    }

    public async Task RunOnceAsync(ConditionalTask task)
    {
        lock (_sync)
        {
            if (!_runningTasks.Add(task.Id))
                return;
        }

        try
        {
            Logger.Instance.CreateDebugLog(
                nameof(ConditionalLaunchEngine),
                $"Starting conditional task '{task.Name}' ({task.Id}).");
            await ConditionalActionExecutor.ExecuteTaskAsync(task);
            Logger.Instance.CreateDebugLog(
                nameof(ConditionalLaunchEngine),
                $"Conditional task '{task.Name}' ({task.Id}) completed.");
            if (SettingsManager.Instance.GetValueOrDefault<bool>(
                "NOTIFICATIONS",
                "conditionalLaunchActions",
                defaultValue: true))
            {
                NotifyHelper.ShowMessage(
                    LocaleHelper.GetLocaleString("ConditionalLaunchNotificationTitle"),
                    string.Format(
                        LocaleHelper.GetLocaleString("ConditionalLaunchActionCompletedMessage"),
                        task.Name),
                    string.Empty);
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(
                nameof(ConditionalLaunchEngine),
                $"Task '{task.Name}' failed: {ex}");
        }
        finally
        {
            lock (_sync)
                _runningTasks.Remove(task.Id);
        }
    }
}
