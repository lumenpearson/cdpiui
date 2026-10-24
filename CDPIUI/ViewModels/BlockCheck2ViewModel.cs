using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.AddOns.BlockCheck2;
using CDPIUI.AddOns.BlockCheck2.Execution;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.AddOns.BlockCheck2.Reporting;
using CDPIUI.Core.Basic;
using CDPIUI.Helper.AddOns.BlockCheck2;

namespace CDPIUI.ViewModels;

public sealed partial class BlockCheck2ViewModel : ObservableObject
{
    private readonly BlockCheckAddOnService _addOnService;
    private readonly BlockCheckSessionPreparationService _preparationService;
    private readonly BlockCheckReportBuilder _reportBuilder;
    private readonly BlockCheckReportSerializer _reportSerializer;
    private readonly BlockCheckReportHistoryService _reportHistoryService;
    private readonly BlockCheckMaximumDurationEstimator _maximumDurationEstimator = new();
    private readonly BlockCheck2ProgressEstimator _progressEstimator = new();
    private CancellationTokenSource? _cancellation;
    private BlockCheckSessionState _state = BlockCheckSessionState.Idle;
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
    private string? _operationError;
    private string? _historySaveError;
    private IReadOnlyList<BlockCheckIssue> _issues = [];
    private IReadOnlyList<BlockCheckTarget> _targets = [];
    private BlockCheckRunResult? _lastResult;
    private BlockCheckReport? _lastReport;
    private BlockCheckResultSession? _lastSession;

    public BlockCheck2ViewModel(
        BlockCheckAddOnService? addOnService = null,
        BlockCheckSessionPreparationService? preparationService = null,
        BlockCheckReportBuilder? reportBuilder = null,
        BlockCheckReportSerializer? reportSerializer = null,
        BlockCheckReportHistoryService? reportHistoryService = null)
    {
        _addOnService = addOnService ?? new BlockCheckAddOnService();
        _preparationService = preparationService ?? new BlockCheckSessionPreparationService();
        _reportBuilder = reportBuilder ?? new BlockCheckReportBuilder();
        _reportSerializer = reportSerializer ?? new BlockCheckReportSerializer();
        _reportHistoryService = reportHistoryService ?? new BlockCheckReportHistoryService(_reportSerializer);
        LoadSettings();
    }

    public BlockCheckSessionState State => _state;
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
    public string? OperationError => _operationError;
    public string? HistorySaveError => _historySaveError;
    public IReadOnlyList<BlockCheckIssue> Issues => _issues;
    public IReadOnlyList<BlockCheckTarget> Targets => _targets;
    public int TargetCount => _targets.Count;
    public BlockCheckRunResult? LastResult => _lastResult;
    public BlockCheckReport? LastReport => _lastReport;
    public BlockCheckResultSession? LastSession => _lastSession;
    public bool NoBypassRequired => _lastReport?.Issues.Any(issue =>
        string.Equals(issue.Code, "NO_BYPASS_REQUIRED", StringComparison.Ordinal)) == true;
    public bool LastReportIsHttpOnly => _lastReport?.Targets.Count > 0 &&
        _lastReport.Targets.All(target => target.Protocol == BlockCheckProtocol.Http);
    public string ReportJson => _lastReport == null ? string.Empty : _reportSerializer.SerializeJson(_lastReport);
    public string ReportText => _lastReport == null ? string.Empty : _reportSerializer.SerializeText(_lastReport);
    public string PresetArguments => _lastReport?.PresetArguments ?? string.Empty;

    public bool IsRunning => _state is BlockCheckSessionState.Running or BlockCheckSessionState.Canceling;
    public bool CanStart => !IsRunning &&
                            HasTargetSources &&
                            HasProtocolSelection &&
                            HasIpVersionSelection &&
                            AdvancedOptionsValid;
    public bool CanCancel => _state == BlockCheckSessionState.Running;
    public bool CanExportReport => _lastReport != null;
    public bool CanExportPreset => (_lastReport?.Success == true || _lastReport?.IsBestEffort == true) &&
                                   !string.IsNullOrWhiteSpace(_lastReport.PresetArguments);

    public async Task<BlockCheckRunResult?> StartAsync()
    {
        if (!CanStart)
        {
            return null;
        }

        if (_lastReport != null)
        {
            _generatedSiteListRoot = BlockCheck2SiteListStorage.CreateGeneratedSiteListRoot();
        }
        ResetResultState();
        _progressEstimator.StartRun();
        CancellationTokenSource? runCancellation = null;
        try
        {
            BlockCheckSessionPreparationResult prepared = _preparationService.Prepare(CreateSessionRequest());
            _targets = prepared.Targets;
            _issues = prepared.Issues;
            Notify(nameof(Targets));
            Notify(nameof(TargetCount));
            Notify(nameof(Issues));
            if (!prepared.Success)
            {
                SetState(BlockCheckSessionState.InputInvalid);
                return null;
            }

            runCancellation = new CancellationTokenSource();
            _cancellation = runCancellation;
            SetState(BlockCheckSessionState.Running);
            BlockCheckRunResult result = await RunPreparedAsync(prepared, runCancellation.Token);

            ApplyRunResult(prepared, result);
            CompleteDisplayedProgress();
            BlockCheckSessionState finalState = GetFinalState(result);
            // The final state allows a new run, so it is set only after the report is saved;
            // otherwise a second run started meanwhile had its CTS disposed by this one.
            await SaveReportToHistoryAsync(_lastReport!);
            SetState(finalState);
            return result;
        }
        catch (OperationCanceledException)
        {
            SetState(BlockCheckSessionState.Canceled);
            return null;
        }
        catch (Exception exception)
        {
            _operationError = exception.Message;
            Notify(nameof(OperationError));
            SetState(BlockCheckSessionState.Failed);
            return null;
        }
        finally
        {
            _progressEstimator.StopRun();
            _estimatedRemainingTime = null;
            _estimatedOverallRemainingTime = null;
            Notify(nameof(EstimatedRemainingTime));
            Notify(nameof(EstimatedOverallRemainingTime));
            runCancellation?.Dispose();
            if (ReferenceEquals(_cancellation, runCancellation))
            {
                _cancellation = null;
            }
            CleanupGeneratedSiteListsIfUnviewable();
            NotifyActionState();
        }
    }

    public void Cancel()
    {
        if (!CanCancel || _cancellation == null)
        {
            return;
        }

        SetState(BlockCheckSessionState.Canceling);
        _cancellation.Cancel();
    }

    public Task SaveReportJsonAsync(string filePath, CancellationToken cancellationToken = default) =>
        _reportSerializer.SaveJsonAsync(
            filePath,
            _lastReport ?? throw new InvalidOperationException("No BlockCheck2 report is available."),
            cancellationToken);

    public Task SaveReportTextAsync(string filePath, CancellationToken cancellationToken = default) =>
        _reportSerializer.SaveTextAsync(
            filePath,
            _lastReport ?? throw new InvalidOperationException("No BlockCheck2 report is available."),
            cancellationToken);

    public Task SavePresetArgumentsAsync(string filePath, CancellationToken cancellationToken = default) =>
        _reportSerializer.SavePresetArgumentsAsync(
            filePath,
            _lastReport ?? throw new InvalidOperationException("No BlockCheck2 report is available."),
            cancellationToken);

    public void Reset()
    {
        if (IsRunning)
        {
            return;
        }

        if (_lastReport == null)
        {
            CleanupGeneratedSiteLists();
        }
        _generatedSiteListRoot = BlockCheck2SiteListStorage.CreateGeneratedSiteListRoot();
        ResetResultState();
        _targets = [];
        Notify(nameof(Targets));
        Notify(nameof(TargetCount));
        SetState(BlockCheckSessionState.Idle);
    }

    private async Task<BlockCheckRunResult> RunPreparedAsync(
        BlockCheckSessionPreparationResult prepared,
        CancellationToken cancellationToken)
    {
        Progress<BlockCheckScanProgress> progress = new(UpdateProgress);
        BlockCheckRunResult runResult = await _addOnService.RunWithCdpiuiAdaptersAsync(
            prepared.Catalog,
            prepared.Targets,
            prepared.RunOptions,
            progress,
            strategyRunnerOptions: new CdpiuiZapret2StrategyRunnerOptions
            {
                StartupGracePeriod = TimeSpan.FromSeconds(_startupGraceSeconds),
            },
            probeRunnerOptions: new CurlBlockCheckProbeRunnerOptions
            {
                ConnectTimeout = TimeSpan.FromSeconds(_connectTimeoutSeconds),
                RequestTimeout = TimeSpan.FromSeconds(_requestTimeoutSeconds),
            },
            cancellationToken: cancellationToken);

        return new BlockCheckRunResult
        {
            PreflightIssues = prepared.Issues
                .Concat(runResult.PreflightIssues)
                .DistinctBy(issue => (issue.Severity, issue.Code, issue.Message, issue.SubjectId))
                .ToArray(),
            Scan = runResult.Scan,
            Synthesis = runResult.Synthesis,
            Validation = runResult.Validation,
            WasCanceled = runResult.WasCanceled,
        };
    }

    private void ApplyRunResult(BlockCheckSessionPreparationResult prepared, BlockCheckRunResult result)
    {
        _lastResult = result;
        _lastReport = _reportBuilder.Build(
            prepared.Catalog,
            _selectedRunPreset,
            prepared.Targets,
            result);
        _lastSession = new BlockCheckResultSession
        {
            Catalog = prepared.Catalog,
            Targets = prepared.Targets,
            RunOptions = prepared.RunOptions,
            RunResult = result,
            Report = _lastReport,
            ConnectTimeout = TimeSpan.FromSeconds(_connectTimeoutSeconds),
            RequestTimeout = TimeSpan.FromSeconds(_requestTimeoutSeconds),
            StartupGracePeriod = TimeSpan.FromSeconds(_startupGraceSeconds),
        };
        _issues = _lastReport.Issues;
        NotifyResult();
    }

    private static BlockCheckSessionState GetFinalState(BlockCheckRunResult result) =>
        result.WasCanceled
            ? BlockCheckSessionState.Canceled
            : result.Success
                ? BlockCheckSessionState.Completed
                : result.Synthesis?.IsBestEffort == true
                    ? BlockCheckSessionState.CompletedWithWarnings
                    : BlockCheckSessionState.Failed;

    private async Task SaveReportToHistoryAsync(BlockCheckReport report)
    {
        try
        {
            await _reportHistoryService.SaveAsync(report);
        }
        catch (Exception exception)
        {
            _historySaveError = exception.Message;
            Notify(nameof(HistorySaveError));
            Logger.Instance.CreateWarningLog(
                nameof(BlockCheck2ViewModel),
                $"Cannot save BlockCheck2 report to history: {exception.Message}");
        }
    }

    private void UpdateProgress(BlockCheckScanProgress progress)
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
            _targets.Count,
            (int)_requestsPerDomain,
            _startupGraceSeconds);
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

    private void ResetResultState()
    {
        _operationError = null;
        _historySaveError = null;
        _issues = [];
        _lastResult = null;
        _lastReport = null;
        _lastSession = null;
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
        Notify(nameof(OperationError));
        Notify(nameof(HistorySaveError));
        NotifyResult();
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

    private void NotifyResult()
    {
        Notify(nameof(Issues));
        Notify(nameof(LastResult));
        Notify(nameof(LastReport));
        Notify(nameof(LastSession));
        Notify(nameof(NoBypassRequired));
        Notify(nameof(LastReportIsHttpOnly));
        Notify(nameof(ReportJson));
        Notify(nameof(ReportText));
        Notify(nameof(PresetArguments));
        Notify(nameof(CanExportReport));
        Notify(nameof(CanExportPreset));
    }

    private void CompleteDisplayedProgress()
    {
        if (_progressTotal <= 0)
        {
            return;
        }

        _progressCurrent = _progressTotal;
        Notify(nameof(ProgressCurrent));
        Notify(nameof(ProgressValue));
    }

    private BlockCheckTarget? CurrentTarget() => _targets.FirstOrDefault(
        item => string.Equals(item.Id, _currentTargetId, StringComparison.Ordinal));

    private void SetState(BlockCheckSessionState value)
    {
        if (_state == value)
        {
            return;
        }

        _state = value;
        Notify(nameof(State));
        NotifyActionState();
    }

    private void NotifyActionState()
    {
        Notify(nameof(IsRunning));
        Notify(nameof(CanContinueFromTargets));
        Notify(nameof(CanStart));
        Notify(nameof(CanCancel));
        Notify(nameof(CanExportReport));
        Notify(nameof(CanExportPreset));
    }

    private void Notify([CallerMemberName] string? propertyName = null) => OnPropertyChanged(propertyName);
}
