using CDPIUI.AddOns.BlockCheck2.Analysis;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Synthesis;

namespace CDPIUI.AddOns.BlockCheck2.Execution;

/// <summary>
/// Decides what a scan probes: which targets are skipped after the baseline, which runtime
/// routes remain, in which order the strategies run and the profile each strategy run uses.
/// </summary>
internal static class BlockCheckScanPlanner
{
    public static HashSet<string> FindDnsUnresolvedTargets(
        IReadOnlyList<ProbeResult> baselineResults,
        BlockCheckScanOptions options) =>
        options.RunBaseline
            ? baselineResults
                .Where(IsDnsUnresolved)
                .Select(result => result.TargetId)
                .ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

    public static BlockCheckIssue CreateDnsTargetsIgnoredIssue(
        IReadOnlyList<BlockCheckTarget> targets,
        IReadOnlySet<string> ignoredTargetIds)
    {
        string[] ignoredHosts = targets
            .Where(target => ignoredTargetIds.Contains(target.Id))
            .Select(target => BlockCheckTarget.NormalizeHost(target.Host))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(host => host, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string preview = string.Join(", ", ignoredHosts.Take(5));
        if (ignoredHosts.Length > 5)
        {
            preview += $", +{ignoredHosts.Length - 5} more";
        }
        return new BlockCheckIssue(
            BlockCheckIssueSeverity.Warning,
            "DNS_TARGETS_IGNORED",
            $"Ignored {ignoredTargetIds.Count} target(s) for {ignoredHosts.Length} host(s) because " +
            $"the selected DNS resolver returned no address: {preview}. This is often DNS blocking, " +
            "which no DPI bypass strategy fixes: use encrypted DNS (DoH) for these hosts.");
    }

    public static BlockCheckRouteScanGroup[] BuildRoutes(
        IReadOnlyList<BlockCheckTarget> activeTargets,
        IReadOnlyList<ProbeResult> baselineResults,
        BlockCheckScanOptions options)
    {
        Dictionary<string, ProbeResult> baselineByTarget = baselineResults
            .ToDictionary(result => result.TargetId, StringComparer.Ordinal);
        return BlockCheckTargetGroupBuilder.Build(activeTargets)
            .Select(group => new BlockCheckRouteScanGroup(group.Id, group.Targets))
            .Where(route => !ShouldSkipBaselineAccessibleRoute(route, baselineByTarget, options))
            .ToArray();
    }

    public static StrategyDefinition[] OrderStrategiesProgressively(
        IEnumerable<StrategyDefinition> strategies)
    {
        List<StrategyDefinition> result = [];
        foreach (IGrouping<BlockCheckScanTier, StrategyDefinition> tier in strategies
                     .GroupBy(strategy => strategy.ScanTier)
                     .OrderBy(group => group.Key))
        {
            StrategyDefinition[][] families = tier
                .GroupBy(
                    strategy => string.IsNullOrWhiteSpace(strategy.Family)
                        ? strategy.Id
                        : strategy.Family,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderBy(strategy => strategy.BaseCost)
                    .ThenBy(strategy => strategy.Id, StringComparer.Ordinal)
                    .ToArray())
                .OrderBy(group => group[0].BaseCost)
                .ThenBy(group => group[0].Family, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            int maximumFamilySize = families.Length == 0
                ? 0
                : families.Max(family => family.Length);
            for (int index = 0; index < maximumFamilySize; index++)
            {
                foreach (StrategyDefinition[] family in families)
                {
                    if (index < family.Length)
                    {
                        result.Add(family[index]);
                    }
                }
            }
        }

        return [.. result];
    }

    public static Zapret2ProfilePlan BuildProfile(
        BlockCheckRouteScanGroup route,
        StrategyDefinition strategy)
    {
        BlockCheckTarget target = route.Targets[0];
        return new Zapret2ProfilePlan
        {
            Name = $"bc_scan_{strategy.Id}_{target.Id}",
            Filter = new Zapret2ProfileFilter
            {
                IpVersion = target.IpVersion,
                Transport = target.Transport,
                Port = target.Port,
                Layer7Protocol = target.Layer7Protocol,
                Domains = route.Targets.All(routeTarget => routeTarget.HostListPaths.Count == 0)
                    ? route.Targets
                        .Select(routeTarget => BlockCheckTarget.NormalizeHost(routeTarget.Host))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray()
                    : [],
                HostListPaths = route.Targets
                    .SelectMany(routeTarget => routeTarget.HostListPaths)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
            },
            Primary = strategy,
            TargetIds = route.Targets
                .Select(routeTarget => routeTarget.Id)
                .ToHashSet(StringComparer.Ordinal),
        };
    }

    public static bool IsSuccessful(ProbeResult result, double minimumRate)
    {
        ProbeSummary summary = ProbeSummary.FromAttempts(result.Attempts);
        return summary.AttemptCount > 0 && summary.SuccessRate >= minimumRate;
    }

    private static bool ShouldSkipBaselineAccessibleRoute(
        BlockCheckRouteScanGroup route,
        IReadOnlyDictionary<string, ProbeResult> baselineByTarget,
        BlockCheckScanOptions options) =>
        options.RunBaseline &&
        options.SkipFullyBaselineAccessibleRoutes &&
        route.Targets.All(target =>
            baselineByTarget.TryGetValue(target.Id, out ProbeResult? result) &&
            IsSuccessful(result, options.SuccessfulStrategyRate));

    private static bool IsDnsUnresolved(ProbeResult result) =>
        result.Attempts.Count > 0 &&
        result.Attempts.All(attempt =>
            !attempt.Success &&
            string.Equals(
                attempt.FailureCode,
                "dns-resolution-failed",
                StringComparison.OrdinalIgnoreCase));
}
