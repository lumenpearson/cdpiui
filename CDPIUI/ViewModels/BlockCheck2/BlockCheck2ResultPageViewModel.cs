using System;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.AddOns.BlockCheck2.Reporting;
using CDPIUI.Helper.AddOns.BlockCheck2;
using Microsoft.UI.Dispatching;
using WinUI3Localizer;

namespace CDPIUI.ViewModels.BlockCheck2;

/// <summary>
/// State of the BlockCheck2 result page: the strategy list and the selected strategy, the
/// strategy and component tests, the manual assignments and the report export. The result
/// data is in <see cref="Result"/>, which the result window shares with the preset review page.
/// </summary>
public sealed partial class BlockCheck2ResultPageViewModel : ObservableObject
{
    private const string LogSource = "ResultPage";

    private readonly ILocalizer _localizer = Localizer.Get();
    private readonly IBlockCheck2ResultPageView _view;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly BlockCheckReportSerializer _reportSerializer = new();
    private bool _languageHandlerConnected;
    private bool _strategyTestRunning;

    private string _strategyCountText = string.Empty;
    private bool _areStrategiesVisible = true;
    private bool _isDetailsHitTestVisible = true;
    private bool _isNoIssuesVisible = true;
    private bool _areIssuesVisible = true;
    private bool _isStrategyTestProgressVisible;
    private bool _isTestStrategyEnabled = true;
    private bool _isManualTestEnabled = true;
    private bool? _isManualTestChecked = false;
    private string _manualTestLabel = string.Empty;
    private string _manualTestGlyph = "\uE768";
    private bool _isAddForSiteEnabled = true;
    private bool _isAddForListEnabled = true;
    private bool _isReviewPresetEnabled = true;
    private string _selectedStrategyName = string.Empty;
    private string _selectedStrategyId = string.Empty;
    private string _selectedStrategyArguments = string.Empty;
    private string _selectedSite = string.Empty;
    private string _selectedConnection = string.Empty;
    private string _selectedEvidence = string.Empty;
    private string _selectedBaseline = string.Empty;

    public BlockCheck2ResultPageViewModel(IBlockCheck2ResultPageView view, DispatcherQueue dispatcherQueue)
    {
        _view = view;
        _dispatcherQueue = dispatcherQueue;
    }

    public BlockCheck2ResultViewModel Result { get; private set; } = new();

    public string StrategyCountText
    {
        get => _strategyCountText;
        private set => SetProperty(ref _strategyCountText, value);
    }

    public bool AreStrategiesVisible
    {
        get => _areStrategiesVisible;
        private set => SetProperty(ref _areStrategiesVisible, value);
    }

    public bool IsNoStrategiesVisible => !_areStrategiesVisible;

    public bool IsDetailsHitTestVisible
    {
        get => _isDetailsHitTestVisible;
        private set => SetProperty(ref _isDetailsHitTestVisible, value);
    }

    public bool IsNoIssuesVisible
    {
        get => _isNoIssuesVisible;
        private set => SetProperty(ref _isNoIssuesVisible, value);
    }

    public bool AreIssuesVisible
    {
        get => _areIssuesVisible;
        private set => SetProperty(ref _areIssuesVisible, value);
    }

    public bool IsStrategyTestProgressVisible
    {
        get => _isStrategyTestProgressVisible;
        private set => SetProperty(ref _isStrategyTestProgressVisible, value);
    }

    public bool IsTestStrategyEnabled
    {
        get => _isTestStrategyEnabled;
        private set => SetProperty(ref _isTestStrategyEnabled, value);
    }

    public bool IsManualTestEnabled
    {
        get => _isManualTestEnabled;
        private set => SetProperty(ref _isManualTestEnabled, value);
    }

    /// <summary>Two-way: clicking the toggle button changes it before the click is handled.</summary>
    public bool? IsManualTestChecked
    {
        get => _isManualTestChecked;
        set => SetProperty(ref _isManualTestChecked, value);
    }

    public string ManualTestLabel
    {
        get => _manualTestLabel;
        private set => SetProperty(ref _manualTestLabel, value);
    }

    public string ManualTestGlyph
    {
        get => _manualTestGlyph;
        private set => SetProperty(ref _manualTestGlyph, value);
    }

    public bool IsAddForSiteEnabled
    {
        get => _isAddForSiteEnabled;
        private set => SetProperty(ref _isAddForSiteEnabled, value);
    }

    public bool IsAddForListEnabled
    {
        get => _isAddForListEnabled;
        private set => SetProperty(ref _isAddForListEnabled, value);
    }

    public bool IsReviewPresetEnabled
    {
        get => _isReviewPresetEnabled;
        private set => SetProperty(ref _isReviewPresetEnabled, value);
    }

    public string SelectedStrategyName
    {
        get => _selectedStrategyName;
        private set => SetProperty(ref _selectedStrategyName, value);
    }

    public string SelectedStrategyId
    {
        get => _selectedStrategyId;
        private set => SetProperty(ref _selectedStrategyId, value);
    }

    public string SelectedStrategyArguments
    {
        get => _selectedStrategyArguments;
        private set => SetProperty(ref _selectedStrategyArguments, value);
    }

    public string SelectedSite
    {
        get => _selectedSite;
        private set => SetProperty(ref _selectedSite, value);
    }

    public string SelectedConnection
    {
        get => _selectedConnection;
        private set => SetProperty(ref _selectedConnection, value);
    }

    public string SelectedEvidence
    {
        get => _selectedEvidence;
        private set => SetProperty(ref _selectedEvidence, value);
    }

    public string SelectedBaseline
    {
        get => _selectedBaseline;
        private set => SetProperty(ref _selectedBaseline, value);
    }

    public void LoadReport(BlockCheckReport report)
    {
        Result.Load(report);
        UpdateView();
    }

    /// <summary>Uses a result view model shared with the window; the view rebinds its collections.</summary>
    public void LoadResult(BlockCheck2ResultViewModel result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Result = result;
        OnPropertyChanged(nameof(Result));
    }

    public void LoadSession(BlockCheckResultSession session)
    {
        Result.Load(session);
        UpdateView();
    }

    public void OnLoaded()
    {
        if (!_languageHandlerConnected)
        {
            _localizer.LanguageChanged += Localizer_LanguageChanged;
            _languageHandlerConnected = true;
        }
        UpdateView();
    }

    public void OnUnloaded()
    {
        // Leaving the page or closing the window kept the test preset running and the user's
        // Zapret2 was never restored.
        _ = StopTestOnLeaveAsync();
        if (!_languageHandlerConnected)
        {
            return;
        }
        _localizer.LanguageChanged -= Localizer_LanguageChanged;
        _languageHandlerConnected = false;
    }

    private void Localizer_LanguageChanged(object? sender, LanguageChangedEventArgs e) => UpdateView();

    public void UpdateView()
    {
        ShowReportSummary();

        bool hasEvidence = Result.StrategyEvidence.Count > 0;
        SetStrategiesVisible(hasEvidence);
        IsDetailsHitTestVisible = _view.SelectedStrategy != null && Result.HasSession;
        IsNoIssuesVisible = Result.IssueCount == 0;
        AreIssuesVisible = Result.IssueCount != 0;
        UpdateStrategyCount();
        UpdateAssignmentView();
        UpdateActionButtons();
        UpdateSelectedStrategy();
    }

    private void SetStrategiesVisible(bool visible)
    {
        _areStrategiesVisible = visible;
        OnPropertiesChanged(nameof(IsNoStrategiesVisible), nameof(AreStrategiesVisible));
    }

    private void ShowReportSummary()
    {
        if (!Result.HasReport)
        {
            ShowActionMessage(BlockCheck2NoticeSeverity.Error, Text("BlockCheck2ResultEmptyTitle"), Text("BlockCheck2ResultEmptyMessage"));
            return;
        }

        var severity = Result.NoBypassRequired
            ? BlockCheck2NoticeSeverity.Informational
            : Result.Success
                ? BlockCheck2NoticeSeverity.Success
                : BlockCheck2NoticeSeverity.Warning;
        var title = Result.NoBypassRequired
            ? Text("BlockCheck2NoBypassRequiredTitle")
            : Result.Success
                ? Text("BlockCheck2ResultSuccessTitle")
                : Result.IsBestEffort
                    ? Text("BlockCheck2ResultBestEffortTitle")
                    : Text("BlockCheck2ResultPartialTitle");
        var message = Result.NoBypassRequired
            ? Text(Result.IsHttpOnly
                ? "BlockCheck2HttpOnlyNoBypassRequiredMessage"
                : "BlockCheck2NoBypassRequiredMessage")
            : Result.Success
                ? Text("BlockCheck2ResultSuccessMessage")
                : Result.IsBestEffort
                    ? Text("BlockCheck2ResultBestEffortMessage")
                    : Text("BlockCheck2ResultPartialMessage");

        ShowActionMessage(severity, title, message);
    }

    public void ApplyStrategyView(string? searchText, int sortMode, int filterMode)
    {
        Result.ApplyStrategyView(searchText, sortMode, filterMode);
        UpdateStrategyCount();
        if (Result.StrategyEvidence.Count > 0)
        {
            _view.SelectFirstStrategy();
        }
        else
        {
            UpdateSelectedStrategy();
        }
        SetStrategiesVisible(Result.StrategyEvidence.Count != 0);
    }

    public void UpdateSelectedStrategy()
    {
        BlockCheck2StrategyEvidenceItem? item = _view.SelectedStrategy;
        bool selected = item != null && Result.HasSession;
        IsDetailsHitTestVisible = selected && !_strategyTestRunning;
        IsTestStrategyEnabled = selected && !_strategyTestRunning;
        IsManualTestEnabled = selected && (!_strategyTestRunning || _isManualTestRunning);
        IsManualTestChecked = selected && _isManualTestRunning;

        ManualTestLabel = IsManualTestChecked == true
            ? Text("StopTest")
            : Text("PresetTestConfirmButton");
        ManualTestGlyph = IsManualTestChecked == true
            ? "\uE769"
            : "\uE768";

        IsAddForSiteEnabled = selected && !_strategyTestRunning;
        IsAddForListEnabled = selected &&
            Result.CanAddForSiteList(item!) &&
            !_strategyTestRunning;
        _view.NotifyMenuStateChanged();
        if (item == null)
        {
            SelectedStrategyName = Text("BlockCheck2SelectStrategyText");
            SelectedStrategyId = string.Empty;
            SelectedStrategyArguments = string.Empty;
            SelectedSite = string.Empty;
            SelectedConnection = string.Empty;
            SelectedEvidence = string.Empty;
            SelectedBaseline = string.Empty;
            return;
        }

        SelectedStrategyName = item.StrategyName;
        SelectedStrategyId = item.StrategyId;
        SelectedStrategyArguments = item.StrategyArguments;
        SelectedSite = item.SiteUrl;
        SelectedConnection = item.ConnectionDetails;
        SelectedEvidence = item.EvidenceDetails;
        SelectedBaseline = string.Format(
            Text("BlockCheck2BaselineEvidenceFormat"),
            item.BaselineText);
    }

    public async Task TestSelectedStrategyAsync()
    {
        BlockCheck2StrategyEvidenceItem? item = _view.SelectedStrategy;
        if (item == null || _strategyTestRunning)
        {
            return;
        }

        _strategyTestRunning = true;
        IsStrategyTestProgressVisible = true;
        UpdateSelectedStrategy();
        try
        {
            BlockCheckManualStrategyTestResult result = await Result.TestStrategyAsync(item);
            UpdateSelectedStrategy();
            ShowStrategyTestResult(result);
        }
        catch (Exception exception)
        {
            ShowActionError(exception.Message);
        }
        finally
        {
            _strategyTestRunning = false;
            IsStrategyTestProgressVisible = false;
            UpdateSelectedStrategy();
            UpdateAssignmentView();
        }
    }

    private void ShowStrategyTestResult(BlockCheckManualStrategyTestResult result)
    {
        if (result.ProbeResult == null)
        {
            BlockCheckIssue firstIssue = result.Issues.FirstOrDefault();
            ShowActionError(firstIssue != null
                ? BlockCheck2IssueText.GetMessage(firstIssue)
                : Text("BlockCheck2StrategyTestFailedMessage"));
            return;
        }

        ProbeSummary summary = ProbeSummary.FromAttempts(result.ProbeResult.Attempts);
        string message = string.Format(
            Text("BlockCheck2StrategyTestResultFormat"),
            summary.SuccessCount,
            summary.AttemptCount,
            summary.SuccessRate);
        if (result.Issues.Any(issue => issue.Severity == BlockCheckIssueSeverity.Error))
        {
            ShowActionError(BlockCheck2IssueText.GetMessage(result.Issues.First(issue =>
                issue.Severity == BlockCheckIssueSeverity.Error)));
        }
        else
        {
            ShowActionMessage(
                summary.SuccessCount == summary.AttemptCount
                    ? BlockCheck2NoticeSeverity.Success
                    : BlockCheck2NoticeSeverity.Warning,
                Text("BlockCheck2StrategyTestCompletedTitle"),
                message);
        }
    }

    private void UpdateStrategyCount()
    {
        StrategyCountText = string.Format(
            Text("BlockCheck2StrategyCountFormat"),
            Result.VisibleStrategyEvidenceCount,
            Result.StrategyEvidenceCount);
    }

    private void UpdateActionButtons()
    {
        IsReviewPresetEnabled = Result.CanUseConfig;
        _view.NotifyMenuStateChanged();
    }

    private void ShowActionError(string message) => ShowActionMessage(
        BlockCheck2NoticeSeverity.Error,
        Text("BlockCheck2ActionFailedTitle"),
        message);

    private void ShowActionMessage(BlockCheck2NoticeSeverity severity, string title, string message) =>
        _view.ShowStatus(severity, title, message);

    private string Text(string key) => _localizer.GetLocalizedString(key);
}
