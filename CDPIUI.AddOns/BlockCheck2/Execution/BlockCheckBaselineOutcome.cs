using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Execution;

internal sealed record BlockCheckBaselineOutcome(
    IReadOnlyList<ProbeResult> Results,
    bool WasCanceled);
