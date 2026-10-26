using CDPIUI.Shared.ComponentsTask;
using CDPIUI.TrayIcon.Helper.Basic;

namespace CDPIUI.TrayIcon.Helper;

public class TasksHelper : TasksManageService<ProcessManager>
{
    private static TasksHelper? _instance;
    private static readonly object SyncRoot = new object();

    public static TasksHelper Instance
    {
        get
        {
            lock (SyncRoot)
            {
                _instance ??= new TasksHelper();
                return _instance;
            }
        }
    }

    public TasksHelper() { }

    // The snapshot: tasks may be added or removed while the messages are sent.
    public async Task SendTasksOutput(string? id = null)
    {
        foreach (var task in await GetTasksSnapshot())
        {
            if (string.IsNullOrEmpty(id) || task.Id == id)
                task.ProcessManager.SendDefaultProcessOutput();
        }
    }

    public async Task SendTasksState(string? id = null)
    {
        foreach (var task in await GetTasksSnapshot())
        {
            if (!string.IsNullOrEmpty(id) && task.Id != id)
                continue;
            task.ProcessManager.SendState();
            task.ProcessManager.SendNowSelectedComponentName();
        }
    }

    public async Task SetIsStartArgsChangedProperty(string id, bool value)
    {
        var task = await GetTaskFromId(id);
        if (task == null)
            return;

        task.ProcessManager.IsProcessInfoChanged = value;
    }

    public async Task EnableProxyOnTask(string id, string _proxyType, string ip, string port)
    {
        var existTask = await GetTaskFromId(id);

        // The snapshot: the loop awaits, and tasks may be added meanwhile. The other tasks stop
        // directly, not in their queues: this runs in the queue of id, and two setups that wait
        // for each other's queue would never finish.
        foreach (var task in await GetTasksSnapshot())
        {
            if (existTask != task && (bool)(task.ProcessManager?.IsProxyEnabled() ?? false))
            {
                await task.ProcessManager.StopProcess();
            }
        }

        if (existTask != null)
            await existTask.ProcessManager.StartProxy(_proxyType, ip, port);
    }

    public async Task InitProxyOnTask(string id, string proxiFyrePath)
    {
        var existTask = await GetTaskFromId(id);
        existTask?.ProcessManager.InitProxy(proxiFyrePath);
    }

    public async Task CleanProxyOnTask(string id)
    {
        var existTask = await GetTaskFromId(id);
        existTask?.ProcessManager.CleanProxy();
    }


    // GoodbyeDPI, Zapret and Zapret2 use the WinDivert driver; Zapret2 was missing, so the
    // service could be deleted under a running winws2.
    List<string> _serviceUsedComponentsIds = ["CSGIVS036", "CSZTBN012", "CSZTBN062"];

    // Each stop joins its component's queue before the first await, so it runs after the
    // commands for that component that came first and does not cut into a start or restart.
    public async Task StopService()
    {
        await Task.WhenAll(_serviceUsedComponentsIds.Select(id =>
            ComponentCommandQueue.Run(id, () => StopTask(id))));
        await ProcessManager.StopService();
    }

    // Stops every task in its component's queue, for user and conditional task commands.
    // The exit and GoodCheck stop with StopAllTasks: they must not wait behind queued commands.
    public async Task StopAllTasksInOrder()
    {
        // The synchronous snapshot lets every stop join its queue before the first await, so a
        // start that arrives later is not stopped by this earlier command.
        await Task.WhenAll(GetTasksSnapshotSync().Select(task =>
            ComponentCommandQueue.Run(task.Id, async () =>
            {
                try
                {
                    await task.ProcessManager.StopProcess();
                }
                catch (Exception ex)
                {
                    // One failed task must not keep the other tasks running.
                    Logger.Instance.CreateErrorLog(nameof(TasksHelper), $"Cannot stop the task {task.Id}: {ex}");
                }
            })));
    }

}
