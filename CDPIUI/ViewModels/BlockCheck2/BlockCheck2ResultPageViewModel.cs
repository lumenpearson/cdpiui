using System;
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
    private readonly BlockCheck2ResultSelection _selection;
    private readonly BlockCheck2ReportSummaryPresenter _reportSummary;
    private readonly BlockCheck2SelectedStrategyTest _selectedStrategyTest;
    private readonly BlockCheck2ReportExport _reportExport;
    private readonly BlockCheck2ManualTestSession _manualTest;
    private readonly BlockCheck2AssignmentActions _assignmentActions;
    private bool _languageHandlerConnected;
    private bool _strategyTestRunning;

    private string _strategyCountText = string.Empty;
    private bool _areStrategiesVisible = true;
    private bool _isNoIssuesVisible = true;
    private bool _areIssuesVisible = true;
    private bool _isStrategyTestProgressVisible;
    private bool _isReviewPresetEnabled = true;

    public BlockCheck2ResultPageViewModel(IBlockCheck2ResultPageView view, DispatcherQueue dispatcherQueue)
    {
        _view = view;
        _dispatcherQueue = dispatcherQueue;
        _assignmentActions = new BlockCheck2AssignmentActions(new AssignmentActionsHost(this));
        _manualTest = new BlockCheck2ManualTestSession(new ManualTestHost(this), GetManualTestTasks,
            GetManualTestComponentId, ComponentTasksManager_TaskStateUpdated);
        _selection = new BlockCheck2ResultSelection(() => _view.SelectedStrategy,
            () => Result.HasSession, () => _strategyTestRunning, () => _manualTest.IsRunning,
            item => Result.CanAddForSiteList(item), Text, () => _view.NotifyMenuStateChanged());
        _selection.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);
        _reportSummary = new BlockCheck2ReportSummaryPresenter(() => Result.HasReport,
            () => Result.NoBypassRequired, () => Result.Success, () => Result.IsBestEffort,
            () => Result.IsHttpOnly, Text, ShowActionMessage);
        _selectedStrategyTest = new BlockCheck2SelectedStrategyTest(() => _view.SelectedStrategy,
            () => _strategyTestRunning, value => _strategyTestRunning = value,
            value => IsStrategyTestProgressVisible = value, UpdateSelectedStrategy,
            item => Result.TestStrategyAsync(item), UpdateAssignmentView, Text,
            BlockCheck2IssueText.GetMessage, ShowActionError, ShowActionMessage);
        _reportExport = new BlockCheck2ReportExport(() => Result.Report,
            (title, name, filter) => _view.PickSaveFile(title, name, filter),
            (path, report) => _reportSerializer.SaveJsonAsync(path, report),
            (path, report) => _reportSerializer.SaveTextAsync(path, report), Text, ShowActionMessage, ShowActionError);
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
        get => _selection.IsDetailsHitTestVisible;
        private set => _selection.IsDetailsHitTestVisible = value;
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
        get => _selection.IsTestStrategyEnabled;
        private set => _selection.IsTestStrategyEnabled = value;
    }

    public bool IsManualTestEnabled
    {
        get => _selection.IsManualTestEnabled;
        private set => _selection.IsManualTestEnabled = value;
    }

    /// <summary>Two-way: clicking the toggle button changes it before the click is handled.</summary>
    public bool? IsManualTestChecked
    {
        get => _selection.IsManualTestChecked;
        set => _selection.IsManualTestChecked = value;
    }

    public string ManualTestLabel
    {
        get => _selection.ManualTestLabel;
        private set => _selection.ManualTestLabel = value;
    }

    public string ManualTestGlyph
    {
        get => _selection.ManualTestGlyph;
        private set => _selection.ManualTestGlyph = value;
    }

    public bool IsAddForSiteEnabled
    {
        get => _selection.IsAddForSiteEnabled;
        private set => _selection.IsAddForSiteEnabled = value;
    }

    public bool IsAddForListEnabled
    {
        get => _selection.IsAddForListEnabled;
        private set => _selection.IsAddForListEnabled = value;
    }

    public bool IsReviewPresetEnabled
    {
        get => _isReviewPresetEnabled;
        private set => SetProperty(ref _isReviewPresetEnabled, value);
    }

    public string SelectedStrategyName
    {
        get => _selection.SelectedStrategyName;
        private set => _selection.SelectedStrategyName = value;
    }

    public string SelectedStrategyId
    {
        get => _selection.SelectedStrategyId;
        private set => _selection.SelectedStrategyId = value;
    }

    public string SelectedStrategyArguments
    {
        get => _selection.SelectedStrategyArguments;
        private set => _selection.SelectedStrategyArguments = value;
    }

    public string SelectedSite
    {
        get => _selection.SelectedSite;
        private set => _selection.SelectedSite = value;
    }

    public string SelectedConnection
    {
        get => _selection.SelectedConnection;
        private set => _selection.SelectedConnection = value;
    }

    public string SelectedEvidence
    {
        get => _selection.SelectedEvidence;
        private set => _selection.SelectedEvidence = value;
    }

    public string SelectedBaseline
    {
        get => _selection.SelectedBaseline;
        private set => _selection.SelectedBaseline = value;
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

    private void ShowReportSummary() => _reportSummary.Show();

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

    public void UpdateSelectedStrategy() => _selection.Update();

    public Task TestSelectedStrategyAsync() => _selectedStrategyTest.TestAsync();

    private void ShowStrategyTestResult(BlockCheckManualStrategyTestResult result) =>
        _selectedStrategyTest.ShowResult(result);

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
