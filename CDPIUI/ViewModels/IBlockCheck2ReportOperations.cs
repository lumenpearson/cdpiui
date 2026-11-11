#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.AddOns.BlockCheck2.Reporting;

namespace CDPIUI.ViewModels;

internal interface IBlockCheck2ReportOperations
{
    BlockCheckReport Build(StrategyCatalog catalog, BlockCheckRunPreset preset,
        IReadOnlyList<BlockCheckTarget> targets, BlockCheckRunResult result);
    string SerializeJson(BlockCheckReport report);
    string SerializeText(BlockCheckReport report);
    Task SaveJsonAsync(string path, BlockCheckReport report, CancellationToken token);
    Task SaveTextAsync(string path, BlockCheckReport report, CancellationToken token);
    Task SavePresetArgumentsAsync(string path, BlockCheckReport report, CancellationToken token);
}
