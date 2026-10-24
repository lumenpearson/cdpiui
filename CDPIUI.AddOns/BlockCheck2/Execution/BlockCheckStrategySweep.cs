using CDPIUI.AddOns.BlockCheck2.Analysis;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Synthesis;

namespace CDPIUI.AddOns.BlockCheck2.Execution;

/// <summary>
/// Runs the strategies of a prepared scan route by route: starts each strategy, probes the
/// targets of its route and keeps the counters the progress reports show.
/// </summary>
internal sealed class BlockCheckStrategySweep
{
    // When winws2 or its driver cannot start, every strategy fails the same way; the scan
    // went on through all of them and the synthesis ran with nothing.
    private const int MaxConsecutiveStartFailures = 5;

    private readonly Zapret2ConfigWriter _writer;
    private readonly IBlockCheckStrategyRunner _strategyRunner;
    private readonly IBlockCheckProbeRunner _probeRunner;
    private readonly BlockCheckScanOptions _options;
    private readonly IProgress<BlockCheckScanProgress>? _progress;
    private readonly List<BlockCheckIssue> _issues;
    private readonly CancellationToken _cancellationToken;
    private readonly int _totalJobs;
    private readonly List<ProbeResult> _results = [];

    private int _completed;
    private int _successfulChecks;
    private int _failedChecks;
    private int _successfulStrategies;
    private int _consecutiveStartFailures;
    private bool _abortedOnStartFailures;

    public BlockCheckStrategySweep(
        Zapret2ConfigWriter writer,
        IBlockCheckStrategyRunner strategyRunner,
        IBlockCheckProbeRunner probeRunner,
        BlockCheckScanOptions options,
        IProgress<BlockCheckScanProgress>? progress,
        List<BlockCheckIssue> issues,
        int totalJobs,
        int successfulBaselineChecks,
        int failedBaselineChecks,
        CancellationToken cancellationToken)
    {
        _writer = writer;
        _strategyRunner = strategyRunner;
        _probeRunner = probeRunner;
        _options = options;
        _progress = progress;
        _issues = issues;
        _totalJobs = totalJobs;
        _successfulChecks = successfulBaselineChecks;
        _failedChecks = failedBaselineChecks;
        _cancellationToken = cancellationToken;
    }

    public IReadOnlyList<ProbeResult> Results => _results;

    public bool WasCanceled { get; private set; }

    public async Task RunAsync(
        IReadOnlyList<BlockCheckRouteScanGroup> routes,
        IReadOnlyDictionary<string, StrategyDefinition[]> strategiesByRoute)
    {
        foreach (BlockCheckRouteScanGroup route in routes)
        {
            await RunRouteAsync(route, strategiesByRoute[route.Key]).ConfigureAwait(false);

            if (WasCanceled || _abortedOnStartFailures)
            {
                break;
            }
        }
    }

    private async Task RunRouteAsync(BlockCheckRouteScanGroup route, StrategyDefinition[] strategies)
    {
        HashSet<string> successfulPlans = new(StringComparer.Ordinal);
        foreach (StrategyDefinition strategy in strategies)
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                WasCanceled = true;
                break;
            }
            string progressTargetId = route.Targets[0].Id;
            Report(BlockCheckScanPhase.StartingStrategy, progressTargetId, strategy.Id, 0);

            Zapret2WriteResult configuration = _writer.Write(
                [BlockCheckScanPlanner.BuildProfile(route, strategy)],
                _options.WriterOptions);
            if (!configuration.Success)
            {
                RecordConfigurationFailure(route, strategy, configuration);
                Report(BlockCheckScanPhase.StrategyCompleted, progressTargetId, strategy.Id, 0);
                continue;
            }

            Dictionary<string, List<ProbeAttempt>> attemptsByTarget = route.Targets
                .ToDictionary(target => target.Id, _ => new List<ProbeAttempt>(), StringComparer.Ordinal);
            await ProbeRouteAsync(route, strategy, configuration.CommandLine, attemptsByTarget).ConfigureAwait(false);

            ProbeResult[] routeResults = route.Targets
                .Where(target => attemptsByTarget[target.Id].Count > 0)
                .Select(target => new ProbeResult
                {
                    StrategyId = strategy.Id,
                    TargetId = target.Id,
                    Attempts = attemptsByTarget[target.Id],
                })
                .ToArray();
            _results.AddRange(routeResults);
            if (!WasCanceled &&
                routeResults.Length == route.Targets.Length &&
                routeResults.All(result => BlockCheckScanPlanner.IsSuccessful(result, _options.SuccessfulStrategyRate)))
            {
                successfulPlans.Add(CandidateNormalizer.GetPlanSignature(strategy));
                _successfulStrategies++;
            }

            if (WasCanceled)
            {
                break;
            }

            _completed++;
            Report(
                BlockCheckScanPhase.StrategyCompleted,
                progressTargetId,
                strategy.Id,
                routeResults.Sum(result => result.Attempts.Count));

            if (_abortedOnStartFailures)
            {
                break;
            }

            if (_options.EnableRouteEarlyStop &&
                successfulPlans.Count >= _options.SuccessfulStrategiesPerRoute)
            {
                break;
            }
        }
    }

    private void RecordConfigurationFailure(
        BlockCheckRouteScanGroup route,
        StrategyDefinition strategy,
        Zapret2WriteResult configuration)
    {
        _issues.AddRange(configuration.Issues.Select(issue => new BlockCheckIssue(
            BlockCheckIssueSeverity.Warning,
            "SCAN_STRATEGY_CONFIG_FAILED",
            $"Strategy configuration could not be generated: {issue.Message}",
            strategy.Id)));
        _results.AddRange(route.Targets.Select(target => new ProbeResult
        {
            StrategyId = strategy.Id,
            TargetId = target.Id,
            Attempts =
            [
                BlockCheckScanIssues.FailedAttempt(
                    "strategy_config_failed",
                    "Zapret2 arguments could not be generated for this strategy."),
            ],
        }));
        _failedChecks += route.Targets.Length;
        _completed++;
    }

    private async Task ProbeRouteAsync(
        BlockCheckRouteScanGroup route,
        StrategyDefinition strategy,
        string commandLine,
        Dictionary<string, List<ProbeAttempt>> attemptsByTarget)
    {
        bool startAttempted = false;
        try
        {
            startAttempted = true;
            await _strategyRunner.StartAsync(commandLine, _cancellationToken)
                .ConfigureAwait(false);
            _consecutiveStartFailures = 0;

            foreach (BlockCheckTarget target in route.Targets)
            {
                await ProbeTargetAsync(target, strategy, attemptsByTarget[target.Id]).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
            WasCanceled = true;
        }
        catch (Exception exception)
        {
            RecordStartFailure(route, strategy, attemptsByTarget, exception);
        }
        finally
        {
            if (startAttempted)
            {
                try
                {
                    await _strategyRunner.StopAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    _issues.Add(BlockCheckScanIssues.Warning(
                        "SCAN_STOP_FAILED",
                        $"Could not stop strategy process: {exception.Message}",
                        strategy.Id));
                }
            }
        }
    }

    private async Task ProbeTargetAsync(
        BlockCheckTarget target,
        StrategyDefinition strategy,
        List<ProbeAttempt> attempts)
    {
        for (int attempt = 1; attempt <= _options.AttemptsPerTarget; attempt++)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            Report(BlockCheckScanPhase.Probing, target.Id, strategy.Id, attempt);
            try
            {
                ProbeAttempt probeAttempt = await _probeRunner
                    .ProbeAsync(target, _cancellationToken)
                    .ConfigureAwait(false);
                attempts.Add(probeAttempt);
                if (probeAttempt.Success)
                {
                    _successfulChecks++;
                }
                else
                {
                    _failedChecks++;
                }
            }
            catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                attempts.Add(BlockCheckScanIssues.FailedAttempt(
                    "target_probe_failed",
                    exception.Message));
                _failedChecks++;
                _issues.Add(BlockCheckScanIssues.Warning(
                    "SCAN_TARGET_PROBE_FAILED",
                    $"Strategy/target scan failed: {exception.Message}",
                    $"{strategy.Id}:{target.Id}"));
                Report(BlockCheckScanPhase.Probing, target.Id, strategy.Id, attempt);
                return;
            }

            Report(BlockCheckScanPhase.Probing, target.Id, strategy.Id, attempt);
        }
    }

    private void RecordStartFailure(
        BlockCheckRouteScanGroup route,
        StrategyDefinition strategy,
        Dictionary<string, List<ProbeAttempt>> attemptsByTarget,
        Exception exception)
    {
        foreach (List<ProbeAttempt> attempts in attemptsByTarget.Values
                     .Where(attempts => attempts.Count == 0))
        {
            attempts.Add(BlockCheckScanIssues.FailedAttempt(
                "strategy_start_failed",
                exception.Message));
            _failedChecks++;
        }
        _issues.Add(BlockCheckScanIssues.Warning(
            "SCAN_STRATEGY_FAILED",
            $"Strategy/route scan failed: {exception.Message}",
            $"{strategy.Id}:{route.Key}"));
        if (attemptsByTarget.Values.All(attempts => attempts.All(a => a.FailureCode == "strategy_start_failed")) &&
            ++_consecutiveStartFailures >= MaxConsecutiveStartFailures)
        {
            _abortedOnStartFailures = true;
            _issues.Add(BlockCheckScanIssues.Error(
                "SCAN_STRATEGIES_DO_NOT_START",
                $"The last {MaxConsecutiveStartFailures} strategies could not be started, so the scan was stopped: {exception.Message}"));
        }
    }

    private void Report(BlockCheckScanPhase phase, string targetId, string strategyId, int attemptNumber) =>
        _progress?.Report(new BlockCheckScanProgress(
            phase,
            _completed,
            _totalJobs,
            targetId,
            strategyId,
            attemptNumber,
            _successfulChecks,
            _failedChecks,
            _successfulStrategies));
}
