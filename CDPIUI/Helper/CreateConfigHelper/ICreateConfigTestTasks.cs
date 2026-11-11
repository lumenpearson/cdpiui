#nullable enable

using System;
using System.Threading.Tasks;

namespace CDPIUI.Helper.CreateConfigHelper;

internal interface ICreateConfigTestTasks
{
    event Action<Tuple<string, bool>>? StateUpdated;
    Task StopAsync(string componentId);
    Task RunAsync(string componentId, string arguments);
}
