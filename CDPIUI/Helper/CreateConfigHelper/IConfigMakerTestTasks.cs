#nullable enable

using System;
using System.Threading.Tasks;

namespace CDPIUI.Helper.CreateConfigHelper;

internal interface IConfigMakerTestTasks
{
    event Action<Tuple<string, bool>>? StateUpdated;
    Task<bool> IsRunningAsync(string componentId);
    Task StopAsync(string componentId);
    Task RunAsync(string componentId, string arguments);
    Task RestoreAsync(string componentId);
}
