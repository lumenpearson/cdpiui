#nullable enable

using System.Collections.Generic;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Synthesis;

namespace CDPIUI.ViewModels;

internal sealed class BlockCheck2ManualPresetOperations : IBlockCheck2ManualPresetOperations
{
    private readonly BlockCheckManualPresetService _service;

    public BlockCheck2ManualPresetOperations(BlockCheckManualPresetService service) => _service = service;

    public BlockCheckManualPresetResult Build(StrategyCatalog catalog, IEnumerable<BlockCheckTarget> targets,
        IEnumerable<BlockCheckManualAssignment> assignments, IEnumerable<ProbeResult> evidence, BlockCheckManualPresetOptions options) =>
        _service.Build(catalog, targets, assignments, evidence, options);
}
