using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Execution;

/// <summary>
/// Starts one synthesized preset, probes every target with it and reports which targets failed.
/// An error of the process or the probe itself is an infrastructure failure: every target counts
/// as failed and the repair loop stops.
/// </summary>
internal sealed class BlockCheckPresetCandidateRun
{
    private readonly IReadOnlyList<BlockCheckTarget> _targets;
    private readonly IBlockCheckStrategyRunner _strategyRunner;
    private readonly IBlockCheckProbeRunner _probeRunner;
    private readonly BlockCheckPresetValidationOptions _options;
    private readonly int _candidateNumber;
    private readonly IProgress<BlockCheckScanProgress>? _progress;
    private readonly ICollection<BlockCheckIssue> _issues;
    private readonly CancellationToken _cancellationToken;
    private readonly int _totalChecks;

    private bool _infrastructureFailure;
    private int _successfulChecks;
    private int _failedChecks;
    private int _completedChecks;

    public BlockCheckPresetCandidateRun(
        IReadOnlyList<BlockCheckTarget> targets,
        IBlockCheckStrategyRunner strategyRunner,
        IBlockCheckProbeRunner probeRunner,
        BlockCheckPresetValidationOptions options,
        int candidateNumber,
        IProgress<BlockCheckScanProgress>? progress,
        ICollection<BlockCheckIssue> issues,
        CancellationToken cancellationToken)
    {
        _targets = targets;
        _strategyRunner = strategyRunner;
        _probeRunner = probeRunner;
        _options = options;
        _candidateNumber = candidateNumber;
        _progress = progress;
        _issues = issues;
        _cancellationToken = cancellationToken;
        _totalChecks = targets.Count * options.AttemptsPerTarget;
    }

    public async Task<BlockCheckPresetValidationAttempt> RunAsync(
        string commandLine,
        IReadOnlySet<string> excludedStrategyIds)
    {
        Dictionary<string, List<ProbeAttempt>> attemptsByTarget = _targets
            .ToDictionary(target => target.Id, _ => new List<ProbeAttempt>(), StringComparer.Ordinal);
        bool startAttempted = false;
        try
        {
            startAttempted = true;
            await _strategyRunner.StartAsync(commandLine, _cancellationToken).ConfigureAwait(false);

            for (int targetIndex = 0; targetIndex < _targets.Count; targetIndex++)
            {
                BlockCheckTarget target = _targets[targetIndex];
                await ProbeTargetAsync(target, attemptsByTarget[target.Id]).ConfigureAwait(false);

                if (_infrastructureFailure)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _infrastructureFailure = true;
            _issues.Add(BlockCheckScanIssues.Error(
                "PRESET_VALIDATION_START_FAILED",
                $"Could not start the synthesized preset: {exception.Message}"));
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
                    _infrastructureFailure = true;
                    _issues.Add(BlockCheckScanIssues.Error(
                        "PRESET_VALIDATION_STOP_FAILED",
                        $"Could not stop the synthesized preset: {exception.Message}"));
                }
            }
        }

        return CreateAttempt(attemptsByTarget, excludedStrategyIds);
    }

    private async Task ProbeTargetAsync(BlockCheckTarget target, List<ProbeAttempt> targetAttempts)
    {
        for (int attemptNumber = 1; attemptNumber <= _options.AttemptsPerTarget; attemptNumber++)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            Report(target, attemptNumber);
            try
            {
                ProbeAttempt probeAttempt = await _probeRunner
                    .ProbeAsync(target, _cancellationToken)
                    .ConfigureAwait(false);
                targetAttempts.Add(probeAttempt);
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
                _infrastructureFailure = true;
                _failedChecks++;
                _issues.Add(BlockCheckScanIssues.Error(
                    "PRESET_VALIDATION_PROBE_FAILED",
                    $"Preset validation probe failed: {exception.Message}",
                    target.Id));
                _completedChecks++;
                Report(target, attemptNumber);
                return;
            }

            _completedChecks++;
            Report(target, attemptNumber);
        }
    }

    private BlockCheckPresetValidationAttempt CreateAttempt(
        Dictionary<string, List<ProbeAttempt>> attemptsByTarget,
        IReadOnlySet<string> excludedStrategyIds)
    {
        ProbeResult[] probeResults = _targets
            .Select(target => new ProbeResult
            {
                StrategyId = string.Empty,
                TargetId = target.Id,
                Attempts = attemptsByTarget[target.Id],
            })
            .ToArray();
        HashSet<string> failedTargetIds = _infrastructureFailure
            ? _targets.Select(target => target.Id).ToHashSet(StringComparer.Ordinal)
            : probeResults
                .Where(result => !IsSuccessful(result))
                .Select(result => result.TargetId)
                .ToHashSet(StringComparer.Ordinal);

        return new BlockCheckPresetValidationAttempt
        {
            CandidateNumber = _candidateNumber,
            ProbeResults = probeResults,
            FailedTargetIds = failedTargetIds,
            ExcludedStrategyIds = excludedStrategyIds,
            InfrastructureFailure = _infrastructureFailure,
        };
    }

    private bool IsSuccessful(ProbeResult result)
    {
        ProbeSummary summary = ProbeSummary.FromAttempts(result.Attempts);
        return summary.AttemptCount >= _options.AttemptsPerTarget &&
               summary.SuccessRate >= _options.MinimumSuccessRate;
    }

    private void Report(BlockCheckTarget target, int attemptNumber) =>
        _progress?.Report(new BlockCheckScanProgress(
            BlockCheckScanPhase.ValidatingPreset,
            _completedChecks,
            _totalChecks,
            target.Id,
            $"preset-{_candidateNumber}",
            attemptNumber,
            _successfulChecks,
            _failedChecks));
}
