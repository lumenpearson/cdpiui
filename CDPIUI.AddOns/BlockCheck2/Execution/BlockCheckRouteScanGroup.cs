using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Execution;

/// <summary>
/// Targets that share one runtime route and are scanned with the same strategy run.
/// </summary>
internal sealed record BlockCheckRouteScanGroup(
    string Key,
    BlockCheckTarget[] Targets);
