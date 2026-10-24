using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.AddOns.BlockCheck2;
using CDPIUI.AddOns.BlockCheck2.Execution;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.AddOns.BlockCheck2.Reporting;
using CDPIUI.AddOns.GoodCheck;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Helper.AddOns.BlockCheck2;
using CDPIUI.Shared;

namespace CDPIUI.ViewModels;

public sealed class BlockCheck2ViewModel : INotifyPropertyChanged
{
    private const string SettingsGroup = "BLOCKCHECK";
    private const int MinimumRequestsPerDomain = 1;
    private const int MaximumRequestsPerDomain = 20;

    private readonly BlockCheckAddOnService _addOnService;
    private readonly BlockCheckSessionPreparationService _preparationService;
    private readonly BlockCheckReportBuilder _reportBuilder;
    private readonly BlockCheckReportSerializer _reportSerializer;
    private readonly BlockCheckReportHistoryService _reportHistoryService;
    private readonly BlockCheckMaximumDurationEstimator _maximumDurationEstimator = new();
    private readonly BlockCheckRemainingTimeEstimator _remainingTimeEstimator = new();
    private readonly Stopwatch _runStopwatch = new();
    private string _generatedSiteListRoot = CreateGeneratedSiteListRoot();
    private CancellationTokenSource? _cancellation;
    private string _targetInput = string.Empty;
    private BlockCheckRunPreset _selectedRunPreset = BlockCheckRunPreset.Balanced;
    private bool _includeHttp;
    private bool _includeTls12 = true;
    private bool _includeTls13;
    private bool _includeTlsAuto = true;
    private bool _includeQuic;
    private bool _includeIpv4 = true;
    private bool _includeIpv6;
    private BlockCheckSessionState _state = BlockCheckSessionState.Idle;
    private BlockCheckScanPhase? _currentPhase;
    private int _progressCurrent;
    private int _progressTotal;
    private TimeSpan? _estimatedRemainingTime;
    private TimeSpan? _estimatedOverallRemainingTime;
    private BlockCheckScanPhase? _estimatorPhase;
    private int _lastObservedCheckCount;
    private TimeSpan _lastObservedCheckElapsed;
    private double? _secondsPerCheck;
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
    private bool _siteListsLoaded;
    private bool _isSiteListLoading;
    private string? _siteListLoadError;
    private double _connectTimeoutSeconds = 5d;
    private double _requestTimeoutSeconds = 10d;
    private double _startupGraceSeconds = 0.75d;
    private bool _testAllStrategies;
    private double _requestsPerDomain = 3d;

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

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<BlockCheckRunPreset> AvailableRunPresets { get; } =
        Enum.GetValues<BlockCheckRunPreset>();
    public ObservableCollection<BlockCheck2SiteListItem> AvailableSiteLists { get; } = [];
    public ObservableCollection<BlockCheck2SiteListItem> SelectedSiteLists { get; } = [];

    public string TargetInput
    {
        get => _targetInput;
        set
        {
            if (_targetInput == value)
            {
                return;
            }

            _targetInput = value ?? string.Empty;
            Notify();
            Notify(nameof(DirectTargetInputCount));
            Notify(nameof(HasTargetSources));
            Notify(nameof(CanStart));
        }
    }

    public BlockCheckRunPreset SelectedRunPreset
    {
        get => _selectedRunPreset;
        set
        {
            if (_selectedRunPreset == value)
            {
                return;
            }

            _selectedRunPreset = value;
            Notify();
            SaveSetting(nameof(SelectedRunPreset), (int)value);
            RequestsPerDomain = BlockCheckRunPresetFactory.GetRecommendedAttemptsPerTarget(value);
        }
    }

    public bool IncludeHttp
    {
        get => _includeHttp;
        set => SetProtocolSelection(ref _includeHttp, value);
    }

    public bool IncludeTls12
    {
        get => _includeTls12;
        set => SetProtocolSelection(ref _includeTls12, value);
    }

    public bool IncludeTls13
    {
        get => _includeTls13;
        set => SetProtocolSelection(ref _includeTls13, value);
    }

    public bool IncludeTlsAuto
    {
        get => _includeTlsAuto;
        set => SetProtocolSelection(ref _includeTlsAuto, value);
    }

    public bool IncludeQuic
    {
        get => _includeQuic;
        set => SetProtocolSelection(ref _includeQuic, value);
    }

    public bool IsHttpOnly => _includeHttp && !_includeTls12 && !_includeTls13 && !_includeTlsAuto && !_includeQuic;

    public bool IncludeIpv4
    {
        get => _includeIpv4;
        set => SetIpVersionSelection(ref _includeIpv4, value);
    }

    public bool IncludeIpv6
    {
        get => _includeIpv6;
        set => SetIpVersionSelection(ref _includeIpv6, value);
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
    public bool IsSiteListLoading => _isSiteListLoading;
    public string? SiteListLoadError => _siteListLoadError;
    public int SelectedSiteListCount => SelectedSiteLists.Count;
    public int SelectedDomainCount => SelectedSiteLists.Sum(item => item.DomainCount);
    public int DirectTargetInputCount => _targetInput
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
    public bool HasTargetSources => DirectTargetInputCount > 0 || SelectedSiteLists.Count > 0;
    public bool HasProtocolSelection =>
        _includeHttp || _includeTls12 || _includeTls13 || _includeTlsAuto || _includeQuic;
    public bool HasIpVersionSelection => _includeIpv4 || _includeIpv6;
    public bool CanContinueFromTargets => !IsRunning && HasProtocolSelection && HasIpVersionSelection;
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

    public double ConnectTimeoutSeconds
    {
        get => _connectTimeoutSeconds;
        set => SetAdvancedOption(ref _connectTimeoutSeconds, value);
    }

    public double RequestTimeoutSeconds
    {
        get => _requestTimeoutSeconds;
        set => SetAdvancedOption(ref _requestTimeoutSeconds, value);
    }

    public double StartupGraceSeconds
    {
        get => _startupGraceSeconds;
        set => SetAdvancedOption(ref _startupGraceSeconds, value);
    }

    public bool TestAllStrategies
    {
        get => _testAllStrategies;
        set
        {
            if (_testAllStrategies == value)
            {
                return;
            }

            _testAllStrategies = value;
            Notify();
            SaveSetting(nameof(TestAllStrategies), value);
        }
    }

    public double RequestsPerDomain
    {
        get => _requestsPerDomain;
        set
        {
            if (_requestsPerDomain.Equals(value))
            {
                return;
            }

            _requestsPerDomain = value;
            Notify();
            Notify(nameof(AdvancedOptionsValid));
            Notify(nameof(CanStart));
            if (IsValidRequestsPerDomain(value))
            {
                SaveSetting(nameof(RequestsPerDomain), (int)value);
            }
        }
    }

    public bool AdvancedOptionsValid =>
        double.IsFinite(_connectTimeoutSeconds) && _connectTimeoutSeconds is >= 1d and <= 60d &&
        double.IsFinite(_requestTimeoutSeconds) && _requestTimeoutSeconds is >= 2d and <= 180d &&
        _requestTimeoutSeconds >= _connectTimeoutSeconds &&
        double.IsFinite(_startupGraceSeconds) && _startupGraceSeconds is >= 0.25d and <= 10d &&
        IsValidRequestsPerDomain(_requestsPerDomain);

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
            _generatedSiteListRoot = CreateGeneratedSiteListRoot();
        }
        ResetResultState();
        _remainingTimeEstimator.Reset();
        _runStopwatch.Restart();
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
                cancellationToken: runCancellation.Token);

            BlockCheckRunResult result = new()
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
            CompleteDisplayedProgress();
            BlockCheckSessionState finalState = result.WasCanceled
                ? BlockCheckSessionState.Canceled
                : result.Success
                    ? BlockCheckSessionState.Completed
                    : result.Synthesis?.IsBestEffort == true
                        ? BlockCheckSessionState.CompletedWithWarnings
                        : BlockCheckSessionState.Failed;
            // The final state allows a new run, so it is set only after the report is saved;
            // otherwise a second run started meanwhile had its CTS disposed by this one.
            try
            {
                await _reportHistoryService.SaveAsync(_lastReport);
            }
            catch (Exception exception)
            {
                _historySaveError = exception.Message;
                Notify(nameof(HistorySaveError));
                Logger.Instance.CreateWarningLog(
                    nameof(BlockCheck2ViewModel),
                    $"Cannot save BlockCheck2 report to history: {exception.Message}");
            }
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
            _runStopwatch.Stop();
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

    public async Task LoadSiteListsAsync()
    {
        if (_siteListsLoaded || _isSiteListLoading)
        {
            return;
        }

        _isSiteListLoading = true;
        _siteListLoadError = null;
        Notify(nameof(IsSiteListLoading));
        Notify(nameof(SiteListLoadError));
        try
        {
            List<SiteListElement> items = await SiteListHelper.GetAllAvailableSiteListTemplatesAsync();
            AvailableSiteLists.Clear();
            foreach (SiteListElement item in items
                         .Where(item => !string.IsNullOrWhiteSpace(item.Directory))
                         .Where(item => !IsLegacyGeneratedSiteList(item.Directory!))
                         .OrderBy(item => item.PackName, StringComparer.CurrentCultureIgnoreCase)
                         .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                var siteListItem = new BlockCheck2SiteListItem(
                    item.Name ?? System.IO.Path.GetFileName(item.Directory!) ?? "site-list.txt",
                    item.PackName ?? string.Empty,
                    item.Directory!,
                    individualSiteListRoot: _generatedSiteListRoot);
                if (siteListItem.DomainCount > 0 && !siteListItem.DisplayName.Contains("exclude"))
                    AvailableSiteLists.Add(siteListItem);

            }
            _siteListsLoaded = true;
        }
        catch (Exception exception)
        {
            _siteListLoadError = exception.Message;
            Notify(nameof(SiteListLoadError));
        }
        finally
        {
            _isSiteListLoading = false;
            Notify(nameof(IsSiteListLoading));
        }
    }

    public BlockCheck2SiteListItem AddCustomSiteList(string filePath, string sourceName)
    {
        string fullPath = Path.GetFullPath(filePath);
        BlockCheck2SiteListItem? existing = AvailableSiteLists
            .Concat(SelectedSiteLists)
            .FirstOrDefault(
            item => string.Equals(item.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            return existing;
        }

        BlockCheck2SiteListItem added = new(
            Path.GetFileName(fullPath),
            sourceName,
            fullPath,
            individualSiteListRoot: _generatedSiteListRoot);
        return added;
    }

    public async Task<BlockCheck2SiteListItem> UpdateCustomSiteListAsync(
        string content,
        string displayName,
        string sourceName,
        CancellationToken cancellationToken = default)
    {
        string normalizedContent = string.Join(
            Environment.NewLine,
            (content ?? string.Empty)
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase));
        if (normalizedContent.Length == 0)
        {
            throw new InvalidDataException(
                WinUI3Localizer.Localizer.Get().GetLocalizedString("BlockCheck2CustomSiteListEmpty"));
        }

        BlockCheck2SiteListItem? item = SelectedSiteLists.FirstOrDefault(candidate => candidate.IsCustom);
        string filePath = item?.FilePath ?? CreateCustomSiteListPath();
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(
            filePath,
            normalizedContent + Environment.NewLine,
            new UTF8Encoding(false),
            cancellationToken);

        if (item == null)
        {
            item = new BlockCheck2SiteListItem(
                displayName,
                sourceName,
                filePath,
                isCustom: true,
                editableContent: normalizedContent,
                individualSiteListRoot: _generatedSiteListRoot);
            SelectedSiteLists.Insert(0, item);
        }
        else
        {
            item.UpdateCustomContent(normalizedContent);
        }

        NotifySiteListSelectionChanged();
        return item;
    }

    public void SelectSiteList(BlockCheck2SiteListItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (SelectedSiteLists.Any(selected => string.Equals(
                selected.FilePath,
                item.FilePath,
                StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        SelectedSiteLists.Add(item);
        NotifySiteListSelectionChanged();
    }

    public void RemoveSiteList(BlockCheck2SiteListItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (SelectedSiteLists.Remove(item))
        {
            NotifySiteListSelectionChanged();
        }
    }

    public void SetSelectedSiteLists(IEnumerable<BlockCheck2SiteListItem> selected)
    {
        SelectedSiteLists.Clear();
        foreach (BlockCheck2SiteListItem item in selected.DistinctBy(
                     item => item.FilePath,
                     StringComparer.OrdinalIgnoreCase))
        {
            SelectedSiteLists.Add(item);
        }
        NotifySiteListSelectionChanged();
    }

    public async Task<BlockCheck2PreparationPreview> BuildPreparationPreviewAsync(
        CancellationToken cancellationToken = default)
    {
        BlockCheckSessionRequest request = CreateSessionRequest();
        double requestTimeout = _requestTimeoutSeconds;
        double startupGrace = _startupGraceSeconds;
        BlockCheck2PreparationPreview preview = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            BlockCheckSessionPreparationResult prepared = _preparationService.Prepare(request);
            BlockCheckMaximumDurationEstimate? estimate = prepared.Success
                ? _maximumDurationEstimator.Estimate(
                    prepared.Catalog,
                    prepared.Targets,
                    prepared.RunOptions,
                    TimeSpan.FromSeconds(requestTimeout),
                    TimeSpan.FromSeconds(startupGrace))
                : null;
            return new BlockCheck2PreparationPreview(
                prepared.Targets.Count,
                estimate,
                prepared.Issues);
        }, cancellationToken);

        if (!preview.Success)
        {
            CleanupGeneratedSiteListsIfUnviewable();
        }

        return preview;
    }

    public void CleanupGeneratedSiteListsIfUnviewable()
    {
        if (_lastReport == null)
        {
            CleanupGeneratedSiteLists();
        }
    }

    private static string CreateCustomSiteListPath()
    {
        string directory = Path.Combine(
            Directories.StoreLocalUserItemDirectory,
            SharedConstants.LocalUserItemSiteListsFolder,
            "BlockCheck2",
            "Custom");
        return Path.Combine(directory, $"custom_sites_{Guid.NewGuid():N}.txt");
    }

    private void NotifySiteListSelectionChanged()
    {
        Notify(nameof(SelectedSiteListCount));
        Notify(nameof(SelectedDomainCount));
        Notify(nameof(HasTargetSources));
        Notify(nameof(CanStart));
    }

    public void ResetAdvancedOptions()
    {
        ConnectTimeoutSeconds = 5d;
        RequestTimeoutSeconds = 10d;
        StartupGraceSeconds = 0.75d;
        TestAllStrategies = false;
        RequestsPerDomain = BlockCheckRunPresetFactory.GetRecommendedAttemptsPerTarget(_selectedRunPreset);
    }

    private void LoadSettings()
    {
        int presetValue = ReadIntSetting(
            nameof(SelectedRunPreset),
            (int)BlockCheckRunPreset.Balanced);
        _selectedRunPreset = Enum.IsDefined(typeof(BlockCheckRunPreset), presetValue)
            ? (BlockCheckRunPreset)presetValue
            : BlockCheckRunPreset.Balanced;

        _includeHttp = ReadBoolSetting(nameof(IncludeHttp), defaultValue: false);
        _includeTls12 = ReadBoolSetting(nameof(IncludeTls12), defaultValue: true);
        _includeTls13 = ReadBoolSetting(nameof(IncludeTls13), defaultValue: false);
        _includeTlsAuto = ReadBoolSetting(nameof(IncludeTlsAuto), defaultValue: true);
        _includeQuic = ReadBoolSetting(nameof(IncludeQuic), defaultValue: false);
        _includeIpv4 = ReadBoolSetting(nameof(IncludeIpv4), defaultValue: true);
        _includeIpv6 = ReadBoolSetting(nameof(IncludeIpv6), defaultValue: false);

        double savedConnectTimeout = ReadDoubleSetting(nameof(ConnectTimeoutSeconds), 5d);
        _connectTimeoutSeconds = double.IsFinite(savedConnectTimeout) && savedConnectTimeout is >= 1d and <= 60d
            ? savedConnectTimeout
            : 5d;

        double savedRequestTimeout = ReadDoubleSetting(nameof(RequestTimeoutSeconds), 10d);
        _requestTimeoutSeconds = double.IsFinite(savedRequestTimeout) &&
                                savedRequestTimeout is >= 2d and <= 180d &&
                                savedRequestTimeout >= _connectTimeoutSeconds
            ? savedRequestTimeout
            : Math.Max(10d, _connectTimeoutSeconds);

        double savedStartupGrace = ReadDoubleSetting(nameof(StartupGraceSeconds), 0.75d);
        _startupGraceSeconds = double.IsFinite(savedStartupGrace) && savedStartupGrace is >= 0.25d and <= 10d
            ? savedStartupGrace
            : 0.75d;

        _testAllStrategies = ReadBoolSetting(nameof(TestAllStrategies), defaultValue: false);
        int recommendedAttempts = BlockCheckRunPresetFactory.GetRecommendedAttemptsPerTarget(_selectedRunPreset);
        int savedAttempts = ReadIntSetting(nameof(RequestsPerDomain), recommendedAttempts);
        _requestsPerDomain = IsValidRequestsPerDomain(savedAttempts)
            ? savedAttempts
            : recommendedAttempts;
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
        _generatedSiteListRoot = CreateGeneratedSiteListRoot();
        ResetResultState();
        _targets = [];
        Notify(nameof(Targets));
        Notify(nameof(TargetCount));
        SetState(BlockCheckSessionState.Idle);
    }

    private BlockCheckSessionRequest CreateSessionRequest() => new()
    {
        TargetInput = _targetInput,
        SiteLists = SelectedSiteLists.Select(item => item.CreateInput(_generatedSiteListRoot)).ToArray(),
        RunPreset = _selectedRunPreset,
        TestAllStrategies = _testAllStrategies,
        AttemptsPerTarget = (int)_requestsPerDomain,
        Protocols = SelectedProtocols(),
        IpVersions = SelectedIpVersions(),
    };

    private IReadOnlySet<BlockCheckProtocol> SelectedProtocols()
    {
        HashSet<BlockCheckProtocol> selected = [];
        if (_includeHttp)
            selected.Add(BlockCheckProtocol.Http);
        if (_includeTls12)
            selected.Add(BlockCheckProtocol.Tls12);
        if (_includeTls13)
            selected.Add(BlockCheckProtocol.Tls13);
        if (_includeTlsAuto)
            selected.Add(BlockCheckProtocol.TlsAuto);
        if (_includeQuic)
            selected.Add(BlockCheckProtocol.Quic);
        return selected;
    }

    private IReadOnlySet<BlockCheckIpVersion> SelectedIpVersions()
    {
        HashSet<BlockCheckIpVersion> selected = [];
        if (_includeIpv4)
            selected.Add(BlockCheckIpVersion.IPv4);
        if (_includeIpv6)
            selected.Add(BlockCheckIpVersion.IPv6);
        return selected;
    }

    private void UpdateProgress(BlockCheckScanProgress progress)
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

        _currentPhase = progress.Phase;
        _progressCurrent = progress.CompletedStrategyTargets;
        _progressTotal = progress.TotalStrategyTargets;
        _currentTargetId = progress.TargetId;
        _currentStrategyId = progress.StrategyId;
        _successfulChecks = progress.SuccessfulChecks;
        _failedChecks = progress.FailedChecks;
        _successfulStrategies = progress.SuccessfulStrategies;
        _estimatedRemainingTime = _remainingTimeEstimator.Update(
            progress.CompletedStrategyTargets,
            progress.TotalStrategyTargets,
            _runStopwatch.Elapsed);
        UpdateObservedCheckDuration(progress.CompletedChecks, _runStopwatch.Elapsed);
        _estimatedOverallRemainingTime = EstimateOverallRemainingTime(progress.Phase);
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
        _estimatorPhase = null;
        _lastObservedCheckCount = 0;
        _lastObservedCheckElapsed = TimeSpan.Zero;
        _secondsPerCheck = null;
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

    private void SetProtocolSelection(ref bool field, bool value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        Notify(propertyName);
        SaveSetting(propertyName, value);
        Notify(nameof(IsHttpOnly));
        Notify(nameof(HasProtocolSelection));
        Notify(nameof(CanContinueFromTargets));
        Notify(nameof(CanStart));
    }

    private void SetIpVersionSelection(ref bool field, bool value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        Notify(propertyName);
        SaveSetting(propertyName, value);
        Notify(nameof(HasIpVersionSelection));
        Notify(nameof(CanContinueFromTargets));
        Notify(nameof(CanStart));
    }

    private void SetAdvancedOption(
        ref double field,
        double value,
        [CallerMemberName] string? propertyName = null)
    {
        if (field.Equals(value))
        {
            return;
        }

        field = value;
        Notify(propertyName);
        if (double.IsFinite(value))
        {
            SaveSetting(propertyName, value);
        }
        Notify(nameof(AdvancedOptionsValid));
        Notify(nameof(CanStart));
    }

    private BlockCheckTarget? CurrentTarget() => _targets.FirstOrDefault(
        item => string.Equals(item.Id, _currentTargetId, StringComparison.Ordinal));

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

    private TimeSpan? EstimateOverallRemainingTime(BlockCheckScanPhase phase)
    {
        if (phase == BlockCheckScanPhase.BaselineProbing || !_estimatedRemainingTime.HasValue)
        {
            return null;
        }

        if (GetEstimatorStage(phase) != 1 || !_secondsPerCheck.HasValue)
        {
            return _estimatedRemainingTime;
        }

        int validationAttempts = (int)_requestsPerDomain;
        double validationSeconds = _targets.Count * validationAttempts * _secondsPerCheck.Value +
                                   _startupGraceSeconds;
        return _estimatedRemainingTime.Value + TimeSpan.FromSeconds(validationSeconds);
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

    private void Notify([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static bool IsValidRequestsPerDomain(double value) =>
        double.IsFinite(value) &&
        value is >= MinimumRequestsPerDomain and <= MaximumRequestsPerDomain &&
        Math.Abs(value - Math.Round(value)) < 0.000001d;

    private static int ReadIntSetting(string key, int defaultValue)
    {
        try
        {
            return SettingsManager.Instance.GetValueOrDefault(
                SettingsGroup,
                key,
                defaultValue: defaultValue);
        }
        catch (Exception exception)
        {
            LogSettingsFailure(key, exception);
            return defaultValue;
        }
    }

    private static double ReadDoubleSetting(string key, double defaultValue)
    {
        try
        {
            return SettingsManager.Instance.GetValueOrDefault(
                SettingsGroup,
                key,
                defaultValue: defaultValue);
        }
        catch (Exception exception)
        {
            LogSettingsFailure(key, exception);
            return defaultValue;
        }
    }

    private static bool ReadBoolSetting(string key, bool defaultValue)
    {
        try
        {
            return SettingsManager.Instance.GetValueOrDefault(
                SettingsGroup,
                key,
                defaultValue: defaultValue);
        }
        catch (Exception exception)
        {
            LogSettingsFailure(key, exception);
            return defaultValue;
        }
    }

    private static void SaveSetting<T>(string? key, T value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        try
        {
            SettingsManager.Instance.SetValue(SettingsGroup, key, value);
        }
        catch (Exception exception)
        {
            LogSettingsFailure(key, exception);
        }
    }

    private static void LogSettingsFailure(string key, Exception exception) =>
        Logger.Instance.CreateWarningLog(
            nameof(BlockCheck2ViewModel),
            $"Cannot read or save BlockCheck2 setting '{key}': {exception.Message}");

    private static string CreateGeneratedSiteListRoot() => Path.Combine(
        Directories.DataDirectory,
        "BlockCheck2",
        "GeneratedSiteLists",
        Guid.NewGuid().ToString("N"));

    private static bool IsLegacyGeneratedSiteList(string filePath)
    {
        string legacyRoot = Path.GetFullPath(Path.Combine(
            Directories.StoreLocalUserItemDirectory,
            SharedConstants.LocalUserItemSiteListsFolder,
            "BlockCheck2",
            "PerSite"));
        string fullPath = Path.GetFullPath(filePath);
        return fullPath.StartsWith(
            legacyRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }

    private void CleanupGeneratedSiteLists()
    {
        try
        {
            string generatedBase = Path.GetFullPath(Path.Combine(
                    Directories.DataDirectory,
                    "BlockCheck2",
                    "GeneratedSiteLists"))
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string target = Path.GetFullPath(_generatedSiteListRoot);
            if (!target.StartsWith(generatedBase, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Instance.CreateWarningLog(
                    nameof(BlockCheck2ViewModel),
                    $"Refusing to remove a generated site-list path outside BlockCheck2 storage: {target}");
                return;
            }

            if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Logger.Instance.CreateWarningLog(
                nameof(BlockCheck2ViewModel),
                $"Cannot remove generated BlockCheck2 site lists: {exception.Message}");
        }
    }
}

public sealed record BlockCheck2PreparationPreview(
    int TargetCount,
    BlockCheckMaximumDurationEstimate? MaximumDuration,
    IReadOnlyList<BlockCheckIssue> Issues)
{
    public bool Success => Issues.All(issue => issue.Severity != BlockCheckIssueSeverity.Error);
}

public sealed class BlockCheck2SiteListItem : INotifyPropertyChanged
{
    private int _processingModeIndex;
    private string _editableContent;
    private int _domainCount;

    public BlockCheck2SiteListItem(
        string displayName,
        string sourceName,
        string filePath,
        bool isCustom = false,
        string? editableContent = null,
        string? individualSiteListRoot = null)
    {
        DisplayName = displayName;
        SourceName = sourceName;
        FilePath = Path.GetFullPath(filePath);
        IsCustom = isCustom;
        _editableContent = editableContent ?? string.Empty;
        _domainCount = CountDomains(_editableContent, FilePath);
        IndividualSiteListRoot = individualSiteListRoot;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string DisplayName { get; }
    public string SourceName { get; }
    public string FilePath { get; }
    public bool IsCustom { get; }
    public string EditableContent => _editableContent;
    public int DomainCount => _domainCount;
    private string? IndividualSiteListRoot { get; }

    public int ProcessingModeIndex
    {
        get => _processingModeIndex;
        set
        {
            int normalized = value == 1 ? 1 : 0;
            if (_processingModeIndex == normalized)
            {
                return;
            }

            _processingModeIndex = normalized;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProcessingModeIndex)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProcessingMode)));
        }
    }

    public BlockCheckSiteListProcessingMode ProcessingMode => _processingModeIndex == 1
        ? BlockCheckSiteListProcessingMode.EachSite
        : BlockCheckSiteListProcessingMode.WholeList;

    public BlockCheckSiteListInput CreateInput(string? individualSiteListRoot = null) => new(
        DisplayName,
        FilePath,
        ProcessingMode,
        ProcessingMode == BlockCheckSiteListProcessingMode.EachSite
            ? CreateIndividualSiteListDirectory(
                FilePath,
                individualSiteListRoot ?? IndividualSiteListRoot ?? CreateFallbackIndividualSiteListRoot())
            : null);

    public void UpdateCustomContent(string content)
    {
        _editableContent = content ?? string.Empty;
        _domainCount = CountDomains(_editableContent, FilePath);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EditableContent)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DomainCount)));
    }

    private static int CountDomains(string content, string filePath)
    {
        try
        {
            IEnumerable<string> lines = string.IsNullOrWhiteSpace(content)
                ? File.ReadLines(filePath)
                : content.Split(['\r', '\n']);
            return lines
                .Select(line => (line ?? string.Empty).Trim().TrimStart('\uFEFF'))
                .Where(line => line.Length > 0 && !line.StartsWith('#'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static string CreateIndividualSiteListDirectory(string filePath, string rootDirectory)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            Path.GetFullPath(filePath).ToUpperInvariant()));
        string id = Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant();
        return Path.Combine(Path.GetFullPath(rootDirectory), id);
    }

    private static string CreateFallbackIndividualSiteListRoot() => Path.Combine(
        Directories.DataDirectory,
        "BlockCheck2",
        "GeneratedSiteLists",
        "Unscoped");
}
