using CDPIUI.Shared.ComponentsTask;

namespace CDPIUI.TrayIcon.Helper
{
    public class TasksHelper : TasksManageService<ProcessManager>
    {
        private static TasksHelper? _instance;
        private static readonly object _lock = new object();

        public static TasksHelper Instance
        {
            get
            {
                lock (_lock)
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
            if (task == null) return;

            task.ProcessManager.IsProcessInfoChanged = value;
        }

        public async Task EnableProxyOnTask(string id, string _proxyType, string ip, string port)
        {
            var existTask = await GetTaskFromId(id);

            foreach (var task in Tasks)
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
        List<string> serviceUsedComponentsIds = ["CSGIVS036", "CSZTBN012", "CSZTBN062"];

        public async Task StopService()
        {
            foreach (var task in await GetTasksSnapshot())
            {
                if (serviceUsedComponentsIds.Contains(task.Id))
                {
                    await task.ProcessManager.StopProcess();
                }
            }
            await ProcessManager.StopService();
        }
        
    }
}
