using CDPIUI.AddOns.BlockCheck2.Analysis;
using CDPIUI.AddOns.BlockCheck2.Catalog;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Synthesis;

namespace CDPIUI.AddOns.BlockCheck2.Execution;

public sealed class BlockCheckScanService
{
    private readonly StrategyCatalogValidator _validator;
    private readonly Zapret2ConfigWriter _writer;

    public BlockCheckScanService(
        StrategyCatalogValidator? validator = null,
        Zapret2ConfigWriter? writer = null)
    {
        _validator = validator ?? new StrategyCatalogValidator();
        _writer = writer ?? new Zapret2ConfigWriter();
    }

    public async Task<BlockCheckScanResult> ScanAsync(
        StrategyCatalog catalog,
        IEnumerable<BlockCheckTarget> targets,
        IBlockCheckStrategyRunner strategyRunner,
        IBlockCheckProbeRunner probeRunner,
        BlockCheckScanOptions? options = null,
        IProgress<BlockCheckScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(strategyRunner);
        ArgumentNullException.ThrowIfNull(probeRunner);

        options ??= new BlockCheckScanOptions();
        BlockCheckTarget[] targetArray = targets.ToArray();
        List<BlockCheckIssue> issues =
        [
            .. _validator.Validate(catalog),
            .. _validator.ValidateTargets(targetArray),
        ];

        ValidateOptions(options, issues);

        HashSet<string> catalogIds = catalog.Strategies
            .Select(strategy => strategy.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string>? selectedIds = options.StrategyIds?
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (selectedIds != null)
        {
            foreach (string unknownId in selectedIds.Where(id => !catalogIds.Contains(id)))
            {
                issues.Add(BlockCheckScanIssues.Error("SCAN_STRATEGY_UNKNOWN", "Requested strategy is absent from the catalog.", unknownId));
            }
        }

        if (issues.Any(issue => issue.Severity == BlockCheckIssueSeverity.Error))
        {
            return new BlockCheckScanResult { Issues = issues };
        }

        bool prepareAttempted = false;
        try
        {
            prepareAttempted = true;
            await strategyRunner.PrepareAsync(cancellationToken).ConfigureAwait(false);
            return await ScanPreparedAsync(
                    catalog,
                    targetArray,
                    selectedIds,
                    strategyRunner,
                    probeRunner,
                    options,
                    progress,
                    issues,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            issues.Add(BlockCheckScanIssues.Canceled());
            return new BlockCheckScanResult
            {
                Issues = issues,
                WasCanceled = true,
            };
        }
        catch (Exception exception)
        {
            issues.Add(BlockCheckScanIssues.Error(
                "SCAN_PREPARE_FAILED",
                $"Could not prepare the BlockCheck process environment: {exception.Message}"));
            return new BlockCheckScanResult { Issues = issues };
        }
        finally
        {
            if (prepareAttempted)
            {
                try
                {
                    await strategyRunner.CompleteAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    issues.Add(BlockCheckScanIssues.Error(
                        "SCAN_COMPLETE_FAILED",
                        $"Could not restore the process environment: {exception.Message}"));
                }
            }
        }
    }

    private async Task<BlockCheckScanResult> ScanPreparedAsync(
        StrategyCatalog catalog,
        BlockCheckTarget[] targetArray,
        HashSet<string>? selectedIds,
        IBlockCheckStrategyRunner strategyRunner,
        IBlockCheckProbeRunner probeRunner,
        BlockCheckScanOptions options,
        IProgress<BlockCheckScanProgress>? progress,
        List<BlockCheckIssue> issues,
        CancellationToken cancellationToken)
    {
        BlockCheckBaselineOutcome baseline = await BlockCheckBaselineProber.ProbeAsync(
                targetArray,
                probeRunner,
                options,
                progress,
                issues,
                cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<ProbeResult> baselineResults = baseline.Results;

        if (baseline.WasCanceled)
        {
            issues.Add(BlockCheckScanIssues.Canceled());
            return new BlockCheckScanResult
            {
                BaselineResults = baselineResults,
                Issues = issues,
                WasCanceled = true,
            };
        }

        int successfulChecks = baselineResults.Sum(result => result.Attempts.Count(attempt => attempt.Success));
        int failedChecks = baselineResults.Sum(result => result.Attempts.Count(attempt => !attempt.Success));

        HashSet<string> ignoredTargetIds = BlockCheckScanPlanner.FindDnsUnresolvedTargets(baselineResults, options);
        BlockCheckTarget[] activeTargets = targetArray
            .Where(target => !ignoredTargetIds.Contains(target.Id))
            .ToArray();
        if (ignoredTargetIds.Count > 0)
        {
            issues.Add(BlockCheckScanPlanner.CreateDnsTargetsIgnoredIssue(targetArray, ignoredTargetIds));
        }

        if (activeTargets.Length == 0)
        {
            issues.Add(BlockCheckScanIssues.Error(
                "NO_RESOLVABLE_TARGETS",
                "None of the requested URLs has an address in the selected DNS resolver."));
            return new BlockCheckScanResult
            {
                BaselineResults = baselineResults,
                IgnoredTargetIds = ignoredTargetIds,
                Issues = issues,
            };
        }

        BlockCheckRouteScanGroup[] routes = BlockCheckScanPlanner.BuildRoutes(activeTargets, baselineResults, options);

        StrategyDefinition[] strategies = BlockCheckScanPlanner.OrderStrategiesProgressively(catalog.Strategies
            .Where(strategy => selectedIds != null
                ? selectedIds.Contains(strategy.Id)
                : strategy.ScanTier <= options.MaximumTier));

        Dictionary<string, StrategyDefinition[]> strategiesByRoute = routes.ToDictionary(
            route => route.Key,
            route => strategies
                .Where(strategy => route.Targets.All(strategy.AppliesTo))
                .ToArray());
        int totalJobs = strategiesByRoute.Values.Sum(routeStrategies => routeStrategies.Length);

        if (routes.Length > 0 && totalJobs == 0)
        {
            issues.Add(BlockCheckScanIssues.Error(
                "SCAN_NO_APPLICABLE_STRATEGIES",
                "No selected catalog strategy applies to every target in the requested runtime routes."));
            return new BlockCheckScanResult
            {
                BaselineResults = baselineResults,
                IgnoredTargetIds = ignoredTargetIds,
                Issues = issues,
            };
        }

        var sweep = new BlockCheckStrategySweep(
            _writer,
            strategyRunner,
            probeRunner,
            options,
            progress,
            issues,
            totalJobs,
            successfulChecks,
            failedChecks,
            cancellationToken);
        await sweep.RunAsync(routes, strategiesByRoute).ConfigureAwait(false);

        if (sweep.WasCanceled)
        {
            issues.Add(BlockCheckScanIssues.Canceled());
        }

        return new BlockCheckScanResult
        {
            BaselineResults = baselineResults,
            ProbeResults = sweep.Results,
            IgnoredTargetIds = ignoredTargetIds,
            Issues = issues,
            WasCanceled = sweep.WasCanceled,
        };
    }

    private static void ValidateOptions(
        BlockCheckScanOptions options,
        ICollection<BlockCheckIssue> issues)
    {
        if (options.AttemptsPerTarget < 1)
        {
            issues.Add(BlockCheckScanIssues.Error("SCAN_ATTEMPTS_INVALID", "Attempts per target must be at least one."));
        }

        if (!Enum.IsDefined(options.MaximumTier))
        {
            issues.Add(BlockCheckScanIssues.Error("SCAN_TIER_INVALID", "Maximum scan tier is invalid."));
        }

        if (options.SuccessfulStrategiesPerRoute < 1)
        {
            issues.Add(BlockCheckScanIssues.Error(
                "SCAN_SUCCESS_COUNT_INVALID",
                "Successful strategies per route must be at least one."));
        }

        if (!double.IsFinite(options.SuccessfulStrategyRate) ||
            options.SuccessfulStrategyRate <= 0d ||
            options.SuccessfulStrategyRate > 1d)
        {
            issues.Add(BlockCheckScanIssues.Error(
                "SCAN_SUCCESS_RATE_INVALID",
                "Successful strategy rate must be greater than zero and at most one."));
        }
    }
}
