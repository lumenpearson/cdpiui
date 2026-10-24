using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Execution;

/// <summary>
/// A strategy plan that a preset repair no longer uses for one runtime route.
/// </summary>
internal readonly record struct BlockCheckStrategyRouteExclusion(
    string PlanSignature,
    RuntimeRouteKey Route);
