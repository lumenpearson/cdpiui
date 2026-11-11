#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.AddOns.BlockCheck2.Reporting;

namespace CDPIUI.ViewModels;

// Owns result presentation; BlockCheck2RunController owns the run and cancellation lifecycle.
internal sealed class BlockCheck2RunResultViewModel : ObservableObject
{
    private readonly IBlockCheck2ReportOperations _reports;
    private readonly BlockCheck2OptionsViewModel _options;
    private string? _operationError;
    private string? _historySaveError;
    private IReadOnlyList<BlockCheckIssue> _issues = [];
    private BlockCheckRunResult? _lastResult;
    private BlockCheckReport? _lastReport;
    private BlockCheckResultSession? _lastSession;

    public BlockCheck2RunResultViewModel(IBlockCheck2ReportOperations reports, BlockCheck2OptionsViewModel options)
    {
        _reports = reports;
        _options = options;
    }

    public string? OperationError => _operationError;
    public string? HistorySaveError => _historySaveError;
    public IReadOnlyList<BlockCheckIssue> Issues => _issues;
    public BlockCheckRunResult? LastResult => _lastResult;
    public BlockCheckReport? LastReport => _lastReport;
    public BlockCheckResultSession? LastSession => _lastSession;
    public bool NoBypassRequired => _lastReport?.Issues.Any(issue =>
        string.Equals(issue.Code, "NO_BYPASS_REQUIRED", StringComparison.Ordinal)) == true;
    public bool LastReportIsHttpOnly => _lastReport?.Targets.Count > 0 &&
        _lastReport.Targets.All(target => target.Protocol == BlockCheckProtocol.Http);
    public string ReportJson => _lastReport == null ? string.Empty : _reports.SerializeJson(_lastReport);
    public string ReportText => _lastReport == null ? string.Empty : _reports.SerializeText(_lastReport);
    public string PresetArguments => _lastReport?.PresetArguments ?? string.Empty;
    public bool CanExportReport => _lastReport != null;
    public bool CanExportPreset => (_lastReport?.Success == true || _lastReport?.IsBestEffort == true) &&
                                   !string.IsNullOrWhiteSpace(_lastReport.PresetArguments);

    // The host publishes Targets and TargetCount before the matching Issues notification.
    public void SetPreparedIssues(IReadOnlyList<BlockCheckIssue> issues) => _issues = issues;

    public void SetOperationError(string message)
    {
        _operationError = message;
        OnPropertyChanged(nameof(OperationError));
    }

    public void SetHistorySaveError(string message)
    {
        _historySaveError = message;
        OnPropertyChanged(nameof(HistorySaveError));
    }

    public void ApplyRunResult(BlockCheckSessionPreparationResult prepared, BlockCheckRunResult result)
    {
        _lastResult = result;
        _lastReport = _reports.Build(prepared.Catalog, _options.SelectedRunPreset, prepared.Targets, result);
        _lastSession = new BlockCheckResultSession
        {
            Catalog = prepared.Catalog,
            Targets = prepared.Targets,
            RunOptions = prepared.RunOptions,
            RunResult = result,
            Report = _lastReport,
            ConnectTimeout = TimeSpan.FromSeconds(_options.ConnectTimeoutSeconds),
            RequestTimeout = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds),
            StartupGracePeriod = TimeSpan.FromSeconds(_options.StartupGraceSeconds),
        };
        _issues = _lastReport.Issues;
        NotifyResult();
    }

    public Task SaveReportJsonAsync(string filePath, CancellationToken cancellationToken = default) =>
        _reports.SaveJsonAsync(filePath,
            _lastReport ?? throw new InvalidOperationException("No BlockCheck2 report is available."), cancellationToken);

    public Task SaveReportTextAsync(string filePath, CancellationToken cancellationToken = default) =>
        _reports.SaveTextAsync(filePath,
            _lastReport ?? throw new InvalidOperationException("No BlockCheck2 report is available."), cancellationToken);

    public Task SavePresetArgumentsAsync(string filePath, CancellationToken cancellationToken = default) =>
        _reports.SavePresetArgumentsAsync(filePath,
            _lastReport ?? throw new InvalidOperationException("No BlockCheck2 report is available."), cancellationToken);

    public void Reset(Action<Action> resetProgress)
    {
        _operationError = null;
        _historySaveError = null;
        _issues = [];
        _lastResult = null;
        _lastReport = null;
        _lastSession = null;
        resetProgress(() =>
        {
            OnPropertyChanged(nameof(OperationError));
            OnPropertyChanged(nameof(HistorySaveError));
            NotifyResult();
        });
    }

    private void NotifyResult()
    {
        OnPropertyChanged(nameof(Issues));
        OnPropertyChanged(nameof(LastResult));
        OnPropertyChanged(nameof(LastReport));
        OnPropertyChanged(nameof(LastSession));
        OnPropertyChanged(nameof(NoBypassRequired));
        OnPropertyChanged(nameof(LastReportIsHttpOnly));
        OnPropertyChanged(nameof(ReportJson));
        OnPropertyChanged(nameof(ReportText));
        OnPropertyChanged(nameof(PresetArguments));
        OnPropertyChanged(nameof(CanExportReport));
        OnPropertyChanged(nameof(CanExportPreset));
    }
}
