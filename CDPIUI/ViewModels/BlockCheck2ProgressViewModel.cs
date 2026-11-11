#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.ViewModels;

// Owns displayed progress; run/cancellation/report lifecycle stays with the host.
internal sealed class BlockCheck2ProgressViewModel : ObservableObject
{
    private readonly Func<IReadOnlyList<BlockCheckTarget>> _targets;
    private readonly Func<int> _validationAttempts;
    private readonly Func<double> _startupGraceSeconds;
    private readonly IBlockCheck2ProgressEstimator _progressEstimator;
    private BlockCheckScanPhase? _currentPhase;
    private int _progressCurrent;
    private int _progressTotal;
    private TimeSpan? _estimatedRemainingTime;
    private TimeSpan? _estimatedOverallRemainingTime;
    private int _successfulChecks;
    private int _failedChecks;
    private int _successfulStrategies;
    private string _currentTargetId = string.Empty;
    private string _currentStrategyId = string.Empty;

    public BlockCheck2ProgressViewModel(Func<IReadOnlyList<BlockCheckTarget>> targets,
        Func<int> validationAttempts, Func<double> startupGraceSeconds,
        IBlockCheck2ProgressEstimator? estimator = null)
    {
        _targets = targets;
        _validationAttempts = validationAttempts;
        _startupGraceSeconds = startupGraceSeconds;
        _progressEstimator = estimator ?? new BlockCheck2ProgressEstimator();
    }

    public void StartRun() => _progressEstimator.StartRun();

    public void FinishRun()
    {
        _progressEstimator.StopRun();
        _estimatedRemainingTime = null;
        _estimatedOverallRemainingTime = null;
        Notify(nameof(EstimatedRemainingTime));
        Notify(nameof(EstimatedOverallRemainingTime));
    }

    public BlockCheckScanPhase? CurrentPhase => _currentPhase;
    public int ProgressCurrent => _progressCurrent;
    public int ProgressTotal => _progressTotal;
    public TimeSpan? EstimatedRemainingTime => _estimatedRemainingTime;
    public TimeSpan? EstimatedOverallRemainingTime => _estimatedOverallRemainingTime;
    public bool IsProgressIndeterminate => _progressTotal <= 0;
    public int SuccessfulChecks => _successfulChecks;
    public int FailedChecks => _failedChecks;
    public int CompletedChecks => _successfulChecks + _failedChecks;
    public int SuccessfulStrategies => _successfulStrategies;
    public double ProgressValue => _progressTotal <= 0
        ? 0d
        : Math.Clamp((double)_progressCurrent / _progressTotal, 0d, 1d);
    public string CurrentTargetId => _currentTargetId;
    public string CurrentTargetUrl
    {
        get
        {
            BlockCheckTarget? target = CurrentTarget();
            return target == null
                ? _currentTargetId
                : BlockCheckTargetDisplayFormatter.FormatUrl(target);
        }
    }
    public string CurrentTargetConnectionDetails
    {
        get
        {
            BlockCheckTarget? target = CurrentTarget();
            return target == null
                ? string.Empty
                : BlockCheckTargetDisplayFormatter.FormatConnectionDetails(target);
        }
    }
    public string CurrentTargetDisplay
    {
        get
        {
            BlockCheckTarget? target = CurrentTarget();
            return target == null
                ? _currentTargetId
                : BlockCheckTargetDisplayFormatter.Format(target);
        }
    }
    public string CurrentStrategyId => _currentStrategyId;

    public void Update(BlockCheckScanProgress progress)
    {
        _currentPhase = progress.Phase;
        _progressCurrent = progress.CompletedStrategyTargets;
        _progressTotal = progress.TotalStrategyTargets;
        _currentTargetId = progress.TargetId;
        _currentStrategyId = progress.StrategyId;
        _successfulChecks = progress.SuccessfulChecks;
        _failedChecks = progress.FailedChecks;
        _successfulStrategies = progress.SuccessfulStrategies;
        (_estimatedRemainingTime, _estimatedOverallRemainingTime) = _progressEstimator.Update(
            progress,
            _targets().Count,
            _validationAttempts(),
            _startupGraceSeconds());
        Notify(nameof(CurrentPhase));
        Notify(nameof(ProgressCurrent));
        Notify(nameof(ProgressTotal));
        Notify(nameof(ProgressValue));
        Notify(nameof(IsProgressIndeterminate));
        Notify(nameof(CurrentTargetId));
        Notify(nameof(CurrentTargetUrl));
        Notify(nameof(CurrentTargetConnectionDetails));
        Notify(nameof(CurrentTargetDisplay));
        Notify(nameof(CurrentStrategyId));
        Notify(nameof(EstimatedRemainingTime));
        Notify(nameof(EstimatedOverallRemainingTime));
        Notify(nameof(SuccessfulChecks));
        Notify(nameof(FailedChecks));
        Notify(nameof(CompletedChecks));
        Notify(nameof(SuccessfulStrategies));
    }

    // Reset all fields before the host notifies errors/results, then notify progress.
    public void Reset(Action beforeNotifications)
    {
        _currentPhase = null;
        _progressCurrent = 0;
        _progressTotal = 0;
        _estimatedRemainingTime = null;
        _estimatedOverallRemainingTime = null;
        _progressEstimator.ResetObservations();
        _successfulChecks = 0;
        _failedChecks = 0;
        _successfulStrategies = 0;
        _currentTargetId = string.Empty;
        _currentStrategyId = string.Empty;
        beforeNotifications();
        Notify(nameof(CurrentPhase));
        Notify(nameof(ProgressCurrent));
        Notify(nameof(ProgressTotal));
        Notify(nameof(ProgressValue));
        Notify(nameof(IsProgressIndeterminate));
        Notify(nameof(EstimatedRemainingTime));
        Notify(nameof(EstimatedOverallRemainingTime));
        Notify(nameof(SuccessfulChecks));
        Notify(nameof(FailedChecks));
        Notify(nameof(CompletedChecks));
        Notify(nameof(SuccessfulStrategies));
        Notify(nameof(CurrentTargetId));
        Notify(nameof(CurrentTargetUrl));
        Notify(nameof(CurrentTargetConnectionDetails));
        Notify(nameof(CurrentTargetDisplay));
        Notify(nameof(CurrentStrategyId));
    }

    public void CompleteDisplayedProgress()
    {
        if (_progressTotal <= 0)
        {
            return;
        }

        _progressCurrent = _progressTotal;
        Notify(nameof(ProgressCurrent));
        Notify(nameof(ProgressValue));
    }

    private BlockCheckTarget? CurrentTarget() => _targets().FirstOrDefault(
        item => string.Equals(item.Id, _currentTargetId, StringComparison.Ordinal));

    private void Notify(string propertyName) => OnPropertyChanged(propertyName);
}
