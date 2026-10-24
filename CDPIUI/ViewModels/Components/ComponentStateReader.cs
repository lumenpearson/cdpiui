using System.Threading.Tasks;
using CDPIUI.Controls.MainPage;
using CDPIUI.Core.ComponentServices;

namespace CDPIUI.ViewModels.Components;

/// <summary>
/// Reads the state of the process task of a component for the component tiles.
/// </summary>
internal static class ComponentStateReader
{
    public static async Task<ComponentState> GetStateAsync(string storeId)
    {
        ProcessService processManager = (await ComponentTasksManager.Instance.GetTaskFromId(storeId))?.ProcessManager;
        if (processManager == null)
            return ComponentState.SetupRequired;

        if (processManager.IsErrorHappens)
            return ComponentState.ExitedWithException;

        return processManager.IsProcessRunning ? ComponentState.Running : ComponentState.Stopped;
    }
}
