using CDPIUI.AddOns.BlockCheck2.Analysis;
using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Execution;

/// <summary>
/// Maps the targets a combined preset failed on to the strategy plans and routes a repair
/// excludes before the next synthesis.
/// </summary>
internal static class BlockCheckPresetRepairPlanner
{
    public static HashSet<BlockCheckStrategyRouteExclusion> FindExclusions(
        IReadOnlySet<string> failedTargetIds,
        IReadOnlyList<Zapret2ProfilePlan> profiles,
        IReadOnlyDictionary<string, BlockCheckTarget> targetsById,
        IReadOnlyDictionary<string, RuntimeRouteKey> routesByTargetId)
    {
        HashSet<BlockCheckStrategyRouteExclusion> exclusions = [];
        foreach (string failedTargetId in failedTargetIds)
        {
            if (!targetsById.TryGetValue(failedTargetId, out BlockCheckTarget? failedTarget) ||
                !routesByTargetId.TryGetValue(failedTargetId, out RuntimeRouteKey failedRoute))
            {
                continue;
            }

            Zapret2ProfilePlan[] implicated = profiles
                .Where(profile => profile.TargetIds.Contains(failedTargetId))
                .ToArray();
            bool directlyAssigned = implicated.Length > 0;
            if (!directlyAssigned)
            {
                implicated = profiles
                    .Where(profile => ProfileCanCapture(profile, failedTarget))
                    .ToArray();
            }

            foreach (Zapret2ProfilePlan profile in implicated)
            {
                if (directlyAssigned)
                {
                    exclusions.Add(new BlockCheckStrategyRouteExclusion(
                        CandidateNormalizer.GetPlanSignature(profile.Primary),
                        failedRoute));
                    continue;
                }

                foreach (string profileTargetId in profile.TargetIds)
                {
                    if (routesByTargetId.TryGetValue(profileTargetId, out RuntimeRouteKey profileRoute))
                    {
                        exclusions.Add(new BlockCheckStrategyRouteExclusion(
                            CandidateNormalizer.GetPlanSignature(profile.Primary),
                            profileRoute));
                    }
                }
            }
        }

        return exclusions;
    }

    public static bool IsExcluded(
        ProbeResult result,
        IReadOnlySet<BlockCheckStrategyRouteExclusion> exclusions,
        IReadOnlyDictionary<string, RuntimeRouteKey> routesByTargetId,
        IReadOnlyDictionary<string, string> signaturesByStrategyId) =>
        routesByTargetId.TryGetValue(result.TargetId, out RuntimeRouteKey route) &&
        signaturesByStrategyId.TryGetValue(result.StrategyId, out string? signature) &&
        exclusions.Contains(new BlockCheckStrategyRouteExclusion(signature, route));

    public static HashSet<string> GetExcludedStrategyIds(
        IReadOnlySet<BlockCheckStrategyRouteExclusion> exclusions,
        IReadOnlyDictionary<string, string> signaturesByStrategyId)
    {
        HashSet<string> signatures = exclusions
            .Select(exclusion => exclusion.PlanSignature)
            .ToHashSet(StringComparer.Ordinal);
        return signaturesByStrategyId
            .Where(pair => signatures.Contains(pair.Value))
            .Select(pair => pair.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool ProfileCanCapture(Zapret2ProfilePlan profile, BlockCheckTarget target)
    {
        if (profile.Filter.IpVersion != target.IpVersion ||
            profile.Filter.Transport != target.Transport ||
            profile.Filter.Port != target.Port ||
            !string.Equals(profile.Filter.Layer7Protocol, target.Layer7Protocol, StringComparison.Ordinal))
        {
            return false;
        }

        string host = BlockCheckTarget.NormalizeHost(target.Host);
        return profile.Filter.Domains.Any(domain =>
            string.Equals(host, domain, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith($".{domain}", StringComparison.OrdinalIgnoreCase));
    }
}
