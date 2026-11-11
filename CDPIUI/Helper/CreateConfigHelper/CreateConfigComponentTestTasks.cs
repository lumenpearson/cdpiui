#nullable enable

using System;
using System.Threading.Tasks;
using CDPIUI.Core.ComponentServices;

namespace CDPIUI.Helper.CreateConfigHelper;

// Keep singleton access at the original operation/subscription points.
internal sealed class CreateConfigComponentTestTasks : ICreateConfigTestTasks
{
    public event Action<Tuple<string, bool>>? StateUpdated
    {
        add => ComponentTasksManager.Instance.TaskStateUpdated += value;
        remove => ComponentTasksManager.Instance.TaskStateUpdated -= value;
    }

    public Task StopAsync(string componentId) => ComponentTasksManager.Instance.StopTask(componentId);
    public Task RunAsync(string componentId, string arguments) =>
        ComponentTasksManager.Instance.CreateAndRunNewTask(componentId, arguments);
}
