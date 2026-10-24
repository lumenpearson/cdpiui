using System;
using System.Diagnostics;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;

namespace CDPIUI.ViewModels;

/// <summary>
/// Estimates the remaining time of the current scan stage and of the whole run from the
/// progress reports. Each stage has its own measurement; the overall estimate of the strategy
/// stage adds the expected duration of the validation that follows it.
/// </summary>
internal sealed class BlockCheck2ProgressEstimator
{
    private readonly BlockCheckRemainingTimeEstimator _remainingTimeEstimator = new();
    private readonly Stopwatch _runStopwatch = new();
    private BlockCheckScanPhase? _estimatorPhase;
    private int _lastObservedCheckCount;
    private TimeSpan _lastObservedCheckElapsed;
    private double? _secondsPerCheck;

    public void StartRun()
    {
        _remainingTimeEstimator.Reset();
        _runStopwatch.Restart();
    }

    public void StopRun() => _runStopwatch.Stop();

    public void ResetObservations()
    {
        _estimatorPhase = null;
        _lastObservedCheckCount = 0;
        _lastObservedCheckElapsed = TimeSpan.Zero;
        _secondsPerCheck = null;
    }

    /// <summary>
    /// Records <paramref name="progress"/> and returns the remaining time of the current stage
    /// and of the whole run.
    /// </summary>
    public (TimeSpan? Stage, TimeSpan? Overall) Update(
        BlockCheckScanProgress progress,
        int targetCount,
        int validationAttempts,
        double startupGraceSeconds)
    {
        if (!_estimatorPhase.HasValue ||
            GetEstimatorStage(_estimatorPhase.Value) != GetEstimatorStage(progress.Phase))
        {
            _remainingTimeEstimator.Reset();
            _runStopwatch.Restart();
            _estimatorPhase = progress.Phase;
            _lastObservedCheckCount = progress.CompletedChecks;
            _lastObservedCheckElapsed = TimeSpan.Zero;
        }

        TimeSpan? stage = _remainingTimeEstimator.Update(
            progress.CompletedStrategyTargets,
            progress.TotalStrategyTargets,
            _runStopwatch.Elapsed);
        UpdateObservedCheckDuration(progress.CompletedChecks, _runStopwatch.Elapsed);
        TimeSpan? overall = EstimateOverallRemainingTime(
            progress.Phase,
            stage,
            targetCount,
            validationAttempts,
            startupGraceSeconds);
        return (stage, overall);
    }

    private void UpdateObservedCheckDuration(int completedChecks, TimeSpan elapsed)
    {
        if (completedChecks <= _lastObservedCheckCount)
        {
            return;
        }

        int completedDelta = completedChecks - _lastObservedCheckCount;
        double elapsedDelta = Math.Max(0d, (elapsed - _lastObservedCheckElapsed).TotalSeconds);
        double sample = elapsedDelta / completedDelta;
        if (sample > 0d)
        {
            _secondsPerCheck = _secondsPerCheck.HasValue
                ? 0.2d * sample + 0.8d * _secondsPerCheck.Value
                : sample;
        }

        _lastObservedCheckCount = completedChecks;
        _lastObservedCheckElapsed = elapsed;
    }

    private TimeSpan? EstimateOverallRemainingTime(
        BlockCheckScanPhase phase,
        TimeSpan? stageRemaining,
        int targetCount,
        int validationAttempts,
        double startupGraceSeconds)
    {
        if (phase == BlockCheckScanPhase.BaselineProbing || !stageRemaining.HasValue)
        {
            return null;
        }

        if (GetEstimatorStage(phase) != 1 || !_secondsPerCheck.HasValue)
        {
            return stageRemaining;
        }

        double validationSeconds = targetCount * validationAttempts * _secondsPerCheck.Value +
                                   startupGraceSeconds;
        return stageRemaining.Value + TimeSpan.FromSeconds(validationSeconds);
    }

    private static int GetEstimatorStage(BlockCheckScanPhase phase) => phase switch
    {
        BlockCheckScanPhase.BaselineProbing => 0,
        BlockCheckScanPhase.StartingStrategy or
            BlockCheckScanPhase.Probing or
            BlockCheckScanPhase.StrategyCompleted => 1,
        BlockCheckScanPhase.ValidatingPreset => 2,
        BlockCheckScanPhase.RepairingPreset => 3,
        _ => 4,
    };
}
