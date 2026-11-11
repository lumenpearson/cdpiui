#nullable enable

using System.Collections.Generic;
using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.ViewModels;

internal interface IBlockCheck2ManualPresetOperations
{
    BlockCheckManualPresetResult Build(StrategyCatalog catalog, IEnumerable<BlockCheckTarget> targets,
        IEnumerable<BlockCheckManualAssignment> assignments, IEnumerable<ProbeResult> evidence, BlockCheckManualPresetOptions options);
}
