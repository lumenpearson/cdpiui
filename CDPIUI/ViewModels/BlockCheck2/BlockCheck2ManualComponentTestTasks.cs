using System;
using System.Threading.Tasks;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Helper.CreateConfigHelper;

namespace CDPIUI.ViewModels.BlockCheck2;

// Binds the receiver before the caller evaluates the current component ID.
internal sealed class BlockCheck2ManualComponentTestTasks(ComponentTasksManager manager) : IConfigMakerTestTasks
{
    public event Action<Tuple<string, bool>>? StateUpdated
    {
        add => manager.TaskStateUpdated += value;
        remove => manager.TaskStateUpdated -= value;
    }

    public Task<bool> IsRunningAsync(string componentId) => manager.IsTaskRunning(componentId);
    public Task StopAsync(string componentId) => manager.StopTask(componentId);
    public Task RunAsync(string componentId, string arguments) => manager.CreateAndRunNewTask(componentId, arguments);
    public Task RestoreAsync(string componentId) => manager.CreateAndRunNewTask(componentId);
}
