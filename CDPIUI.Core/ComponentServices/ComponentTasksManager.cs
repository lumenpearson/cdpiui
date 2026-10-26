using System.Diagnostics;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Communication;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Database;
using CDPIUI.Shared.ComponentsTask;

namespace CDPIUI.Core.ComponentServices;

public class ComponentTasksManager : TasksManageService<ProcessService>
{
    private static ComponentTasksManager? _instance;
    private static readonly object SyncRoot = new object();

    public static ComponentTasksManager Instance
    {
        get
        {
            lock (SyncRoot)
            {
                _instance ??= new ComponentTasksManager();
                return _instance;
            }
        }
    }

    private ComponentTasksManager()
    {
        _ = UpdateTaskList();

        ComponentItemsLoaderHelper.Instance.InitRequested += () => _ = SendTasksData();
        StoreHelper.Instance.ItemActionsStopped += (id) => RequestComponentItemsInit();
        StoreHelper.Instance.ItemRemoved += (id) => RequestComponentItemsInit();
    }

    /// <summary>
    /// Rebuilds the task list from the installed components, for example after a component
    /// was installed or updated, and starts again the components that were running.
    /// </summary>
    public async Task UpdateTaskList()
    {
        try
        {
            // Taken before stopping: StopAllTasks clears the running state.
            var runningIds = (await GetTasksSnapshot())
                .Where(x => x.ProcessManager?.IsProcessRunning ?? false)
                .Select(x => x.Id)
                .ToHashSet();

            await StopAllTasks();

            foreach (var item in DatabaseHelper.Instance.GetItemsByType("component"))
            {
                if (item.Id == null)
                    continue;
                if (runningIds.Contains(item.Id))
                    await CreateAndRunNewTask(item.Id);
                else
                    await AddNewTaskAsync(item.Id);
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(ComponentTasksManager), $"Cannot update the task list: {ex}");
        }
    }

    public void RequestComponentItemsInit()
    {
        ComponentItemsLoaderHelper.Instance.Init();
    }

    public async Task SendTaskData(string id)
    {
        var task = await GetTaskFromId(id);
        if (task != null && DatabaseHelper.Instance.IsItemInstalled(task.Id))
        {
            ComponentHelper? componentHelper =
                ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(task.Id);

            try
            {
                string startupParams = componentHelper == null
                    ? string.Empty
                    : await Task.Run(componentHelper.GetStartupParams);

                if (string.IsNullOrEmpty(startupParams))
                {
                    await PipeHelper.SendSettingsPacket(
                        Shared.Pipe.Models.SettingsMessageIds.ComponentSetupNotFinished,
                        new() { { "componentId", task.Id } });
                    return;
                }
                await PipeHelper.SendSettingsPacket(
                        Shared.Pipe.Models.SettingsMessageIds.ComponentSetupFinished,
                        new() { { "componentId", task.Id } });
            }
            catch
            {
                await PipeHelper.SendSettingsPacket(
                        Shared.Pipe.Models.SettingsMessageIds.ComponentSetupNotFinished,
                        new() { { "componentId", task.Id } });
            }
        }
        else
        {
            // The task can be missing here, so the requested id is reported.
            await PipeHelper.SendSettingsPacket(
                        Shared.Pipe.Models.SettingsMessageIds.ComponentNotInstalled,
                        new() { { "componentId", id } });
        }
    }

    // Catches and logs its own errors, so a caller can start it without waiting for it.
    public async Task SendTasksData()
    {
        try
        {
            foreach (var task in await GetTasksSnapshot())
            {
                await SendTaskData(task.Id);
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(ComponentTasksManager), $"Cannot send the task data: {ex}");
        }
    }




    // TODO: Remove hardcoded Ids
    List<string> _serviceUsedComponentsIds = ["CSGIVS036", "CSZTBN012", "CSZTBN062"];

    public async Task StopService()
    {
        foreach (var task in Tasks)
        {
            if (_serviceUsedComponentsIds.Contains(task.Id))
            {
                await task.ProcessManager.StopProcess();
            }
        }
        await ProcessService.StopService();
    }

    public async Task RunAllPreferredActions()
    {
        foreach (var task in await GetTasksSnapshot())
        {
            // "await x?.M()" awaits null and throws when ProcessManager is null.
            if (task.ProcessManager != null)
                await task.ProcessManager.RunActionsIfAutorunSelected();
        }

    }

    [Obsolete("Not supported for UI")]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
    public override Task CreateAndRunNewTask(string id, string executable, string args)
    {
        throw new NotImplementedException();
    }
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
}
