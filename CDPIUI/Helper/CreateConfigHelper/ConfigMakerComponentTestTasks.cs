#nullable enable

using System;
using System.Threading.Tasks;
using CDPIUI.Core.ComponentServices;

namespace CDPIUI.Helper.CreateConfigHelper;

// Resolves the existing manager only when an operation is requested, as the editor did before.
internal sealed class ConfigMakerComponentTestTasks : IConfigMakerTestTasks
{
    public event Action<Tuple<string, bool>>? StateUpdated
    {
        add => ComponentTasksManager.Instance.TaskStateUpdated += value;
        remove => ComponentTasksManager.Instance.TaskStateUpdated -= value;
    }

    public Task<bool> IsRunningAsync(string componentId) => ComponentTasksManager.Instance.IsTaskRunning(componentId);
    public Task StopAsync(string componentId) => ComponentTasksManager.Instance.StopTask(componentId);
    public Task RunAsync(string componentId, string arguments) => ComponentTasksManager.Instance.CreateAndRunNewTask(componentId, arguments);
    public Task RestoreAsync(string componentId) => ComponentTasksManager.Instance.CreateAndRunNewTask(componentId);
}
