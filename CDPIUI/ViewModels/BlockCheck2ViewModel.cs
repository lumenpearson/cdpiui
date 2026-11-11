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

public sealed partial class BlockCheck2ViewModel : ObservableObject, IBlockCheck2RunHost
{
    private readonly BlockCheckAddOnService _addOnService;
    private readonly BlockCheckSessionPreparationService _preparationService;
    private readonly BlockCheckReportHistoryService _reportHistoryService;
    private readonly BlockCheckMaximumDurationEstimator _maximumDurationEstimator = new();
    private readonly BlockCheck2ProgressViewModel _progress;
    private readonly BlockCheck2RunResultViewModel _results;
    private readonly BlockCheck2RunController _runController;
    private readonly BlockCheck2SiteListController<BlockCheck2SiteListItem> _siteLists;
    private BlockCheckSessionState _state = BlockCheckSessionState.Idle;
    private IReadOnlyList<BlockCheckTarget> _targets = [];

    public BlockCheck2ViewModel(
        BlockCheckAddOnService? addOnService = null,
        BlockCheckSessionPreparationService? preparationService = null,
        BlockCheckReportBuilder? reportBuilder = null,
        BlockCheckReportSerializer? reportSerializer = null,
        BlockCheckReportHistoryService? reportHistoryService = null)
    {
        _addOnService = addOnService ?? new BlockCheckAddOnService();
        _preparationService = preparationService ?? new BlockCheckSessionPreparationService();
        BlockCheckReportBuilder builder = reportBuilder ?? new BlockCheckReportBuilder();
        BlockCheckReportSerializer serializer = reportSerializer ?? new BlockCheckReportSerializer();
        _reportHistoryService = reportHistoryService ?? new BlockCheckReportHistoryService(serializer);
        _siteLists = new BlockCheck2SiteListController<BlockCheck2SiteListItem>(AvailableSiteLists, SelectedSiteLists,
            new BlockCheck2SiteListBackend(), () => _generatedSiteListRoot, NotifySiteListSelectionChanged);
        _siteLists.PropertyChanged += (_, args) => Notify(args.PropertyName);
        _options = new BlockCheck2OptionsViewModel(
            new BlockCheck2OptionsSettings(), () => IsRunning, () => SelectedSiteLists.Count);
        _options.PropertyChanged += (_, args) => Notify(args.PropertyName);
        _results = new BlockCheck2RunResultViewModel(new BlockCheck2ReportOperations(builder, serializer), _options);
        _results.PropertyChanged += (_, args) => Notify(args.PropertyName);
        _progress = new BlockCheck2ProgressViewModel(() => _targets,
            () => (int)_options.RequestsPerDomain, () => _options.StartupGraceSeconds);
        _progress.PropertyChanged += (_, args) => Notify(args.PropertyName);
        _runController = new BlockCheck2RunController(this);
    }

    public BlockCheckSessionState State => _state;
    public BlockCheckScanPhase? CurrentPhase => _progress.CurrentPhase;
    public int ProgressCurrent => _progress.ProgressCurrent;
    public int ProgressTotal => _progress.ProgressTotal;
    public TimeSpan? EstimatedRemainingTime => _progress.EstimatedRemainingTime;
    public TimeSpan? EstimatedOverallRemainingTime => _progress.EstimatedOverallRemainingTime;
    public bool IsProgressIndeterminate => _progress.IsProgressIndeterminate;
    public int SuccessfulChecks => _progress.SuccessfulChecks;
    public int FailedChecks => _progress.FailedChecks;
    public int CompletedChecks => _progress.CompletedChecks;
    public int SuccessfulStrategies => _progress.SuccessfulStrategies;
    public double ProgressValue => _progress.ProgressValue;
    public string CurrentTargetId => _progress.CurrentTargetId;
    public string CurrentTargetUrl => _progress.CurrentTargetUrl;
    public string CurrentTargetConnectionDetails => _progress.CurrentTargetConnectionDetails;
    public string CurrentTargetDisplay => _progress.CurrentTargetDisplay;
    public string CurrentStrategyId => _progress.CurrentStrategyId;
    public string? OperationError => _results.OperationError;
    public string? HistorySaveError => _results.HistorySaveError;
    public IReadOnlyList<BlockCheckIssue> Issues => _results.Issues;
    public IReadOnlyList<BlockCheckTarget> Targets => _targets;
    public int TargetCount => _targets.Count;
    public BlockCheckRunResult? LastResult => _results.LastResult;
    public BlockCheckReport? LastReport => _results.LastReport;
    public BlockCheckResultSession? LastSession => _results.LastSession;
    public bool NoBypassRequired => _results.NoBypassRequired;
    public bool LastReportIsHttpOnly => _results.LastReportIsHttpOnly;
    public string ReportJson => _results.ReportJson;
    public string ReportText => _results.ReportText;
    public string PresetArguments => _results.PresetArguments;

    public bool IsRunning => _state is BlockCheckSessionState.Running or BlockCheckSessionState.Canceling;
    public bool CanStart => _options.CanStart;
    public bool CanCancel => _state == BlockCheckSessionState.Running;
    public bool CanExportReport => _results.CanExportReport;
    public bool CanExportPreset => _results.CanExportPreset;

    public Task<BlockCheckRunResult?> StartAsync() => _runController.StartAsync();

    public void Cancel() => _runController.Cancel();

    bool IBlockCheck2RunHost.CanStart => CanStart;
    bool IBlockCheck2RunHost.CanCancel => CanCancel;
    BlockCheckReport? IBlockCheck2RunHost.LastReport => LastReport;
    void IBlockCheck2RunHost.CleanupGeneratedSiteListsIfUnviewable() => CleanupGeneratedSiteListsIfUnviewable();
    void IBlockCheck2RunHost.RenewGeneratedSiteListRoot() =>
        _generatedSiteListRoot = BlockCheck2SiteListStorage.CreateGeneratedSiteListRoot();
    void IBlockCheck2RunHost.ResetResultState() => ResetResultState();
    void IBlockCheck2RunHost.StartProgress() => _progress.StartRun();
    BlockCheckSessionPreparationResult IBlockCheck2RunHost.Prepare() =>
        _preparationService.Prepare(CreateSessionRequest());
    void IBlockCheck2RunHost.ApplyPrepared(BlockCheckSessionPreparationResult prepared)
    {
        _targets = prepared.Targets;
        _results.SetPreparedIssues(prepared.Issues);
        Notify(nameof(Targets));
        Notify(nameof(TargetCount));
        Notify(nameof(Issues));
    }
    void IBlockCheck2RunHost.SetState(BlockCheckSessionState state) => SetState(state);
    Task<BlockCheckRunResult> IBlockCheck2RunHost.RunPreparedAsync(
        BlockCheckSessionPreparationResult prepared, CancellationToken cancellationToken) => RunPreparedAsync(prepared, cancellationToken);
    void IBlockCheck2RunHost.ApplyRunResult(BlockCheckSessionPreparationResult prepared, BlockCheckRunResult result) =>
        ApplyRunResult(prepared, result);
    void IBlockCheck2RunHost.CompleteDisplayedProgress() => CompleteDisplayedProgress();
    Task IBlockCheck2RunHost.SaveReportToHistoryAsync(BlockCheckReport report) => SaveReportToHistoryAsync(report);
    void IBlockCheck2RunHost.SetOperationError(string message) => _results.SetOperationError(message);
    void IBlockCheck2RunHost.FinishProgress() => _progress.FinishRun();
    void IBlockCheck2RunHost.NotifyActionState() => NotifyActionState();

    public Task SaveReportJsonAsync(string filePath, CancellationToken cancellationToken = default) =>
        _results.SaveReportJsonAsync(filePath, cancellationToken);

    public Task SaveReportTextAsync(string filePath, CancellationToken cancellationToken = default) =>
        _results.SaveReportTextAsync(filePath, cancellationToken);

    public Task SavePresetArgumentsAsync(string filePath, CancellationToken cancellationToken = default) =>
        _results.SavePresetArgumentsAsync(filePath, cancellationToken);

    public void Reset()
    {
        if (IsRunning)
        {
            return;
        }

        if (_results.LastReport == null)
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
                StartupGracePeriod = TimeSpan.FromSeconds(_options.StartupGraceSeconds),
            },
            probeRunnerOptions: new CurlBlockCheckProbeRunnerOptions
            {
                ConnectTimeout = TimeSpan.FromSeconds(_options.ConnectTimeoutSeconds),
                RequestTimeout = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds),
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

    private void ApplyRunResult(BlockCheckSessionPreparationResult prepared, BlockCheckRunResult result) =>
        _results.ApplyRunResult(prepared, result);

    private async Task SaveReportToHistoryAsync(BlockCheckReport report)
    {
        try
        {
            await _reportHistoryService.SaveAsync(report);
        }
        catch (Exception exception)
        {
            _results.SetHistorySaveError(exception.Message);
            Logger.Instance.CreateWarningLog(
                nameof(BlockCheck2ViewModel),
                $"Cannot save BlockCheck2 report to history: {exception.Message}");
        }
    }

    private void UpdateProgress(BlockCheckScanProgress progress) => _progress.Update(progress);

    private void ResetResultState() => _results.Reset(_progress.Reset);

    private void CompleteDisplayedProgress() => _progress.CompleteDisplayedProgress();

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
