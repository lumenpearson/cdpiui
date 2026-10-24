using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CDPIUI.AddOns.Troubleshooting;
using CDPIUI.Commands;
using CDPIUI.Helper.LScript;
using CDPIUI.Views.Troubleshooting;
using WinUI3Localizer;
using static CDPIUI.AddOns.Troubleshooting.TroubleshootingService;

namespace CDPIUI.ViewModels.Troubleshooting;

/// <summary>
/// Runs the basic or the Store diagnostic chosen by the navigation parameter, shows its
/// results, and runs the fix of the found errors.
/// </summary>
public sealed partial class WorkViewModel : ObservableObject
{
    private const string LogSource = "WorkPage";

    private readonly ILocalizer _localizer = Localizer.Get();
    private readonly IWorkView _view;
    private NavigationParameters _navigationParameter = NavigationParameters.None;
    private object? _diagnosticResults;
    // The services are singletons: a page that is not shown must not keep receiving their events.
    private bool _eventsConnected;

    private string _currentStateText = string.Empty;
    private string _resultTitle = string.Empty;
    private string _resultTip = string.Empty;
    private bool _isLoadingVisible = true;
    private bool _isViewResultsVisible = true;
    private bool _isFixVisible = true;
    private bool _isDiagnosticResultVisible;
    private bool _isFixResultVisible;
    private bool _isFixResultListVisible = true;
    private bool _isForwardVisible;
    private bool _isCancelVisible = true;
    private bool _isExitVisible;

    public WorkViewModel(IWorkView view)
    {
        _view = view;
        ForwardCommand = new RelayCommand(BeginFix);
        OpenHelpCommand = new RelayCommand(() =>
            CommandsHandler.HandleCommand("cdpiui://Help/Utils/TroubleshootingUtility/"));
    }

    public RelayCommand ForwardCommand { get; }

    public RelayCommand OpenHelpCommand { get; }

    public ObservableCollection<DiagnosticResultModel> DiagnosticResults { get; } = [];

    public ObservableCollection<DiagnosticResultModel> FixResults { get; } = [];

    public string CurrentStateText
    {
        get => _currentStateText;
        private set => SetProperty(ref _currentStateText, value);
    }

    public string ResultTitle
    {
        get => _resultTitle;
        private set => SetProperty(ref _resultTitle, value);
    }

    public string ResultTip
    {
        get => _resultTip;
        private set => SetProperty(ref _resultTip, value);
    }

    public bool IsLoadingVisible
    {
        get => _isLoadingVisible;
        private set => SetProperty(ref _isLoadingVisible, value);
    }

    public bool IsViewResultsVisible
    {
        get => _isViewResultsVisible;
        private set => SetProperty(ref _isViewResultsVisible, value);
    }

    public bool IsFixVisible
    {
        get => _isFixVisible;
        private set => SetProperty(ref _isFixVisible, value);
    }

    public bool IsDiagnosticResultVisible
    {
        get => _isDiagnosticResultVisible;
        private set => SetProperty(ref _isDiagnosticResultVisible, value);
    }

    public bool IsFixResultVisible
    {
        get => _isFixResultVisible;
        private set => SetProperty(ref _isFixResultVisible, value);
    }

    public bool IsFixResultListVisible
    {
        get => _isFixResultListVisible;
        private set => SetProperty(ref _isFixResultListVisible, value);
    }

    public bool IsForwardVisible
    {
        get => _isForwardVisible;
        private set => SetProperty(ref _isForwardVisible, value);
    }

    public bool IsCancelVisible
    {
        get => _isCancelVisible;
        private set => SetProperty(ref _isCancelVisible, value);
    }

    public bool IsExitVisible
    {
        get => _isExitVisible;
        private set => SetProperty(ref _isExitVisible, value);
    }

    public void ConnectEvents()
    {
        if (_eventsConnected)
            return;
        _eventsConnected = true;
        TroubleshootingService.Instance.CurrentStateChanged += TroubleshootingHelper_BasicDialogStateChanged;
    }

    public void DisconnectEvents()
    {
        if (!_eventsConnected)
            return;
        _eventsConnected = false;
        TroubleshootingService.Instance.CurrentStateChanged -= TroubleshootingHelper_BasicDialogStateChanged;
    }

    /// <summary>Starts the check that the navigation parameter names.</summary>
    public void Start(NavigationParameters navigationParameter)
    {
        _navigationParameter = navigationParameter;
        switch (_navigationParameter)
        {
            // The checks catch and log their own errors.
            case NavigationParameters.BeginBasicCheck:
                _ = BeginBasicCheck();
                break;
            case NavigationParameters.BeginStoreRepoCheck:
                _ = BeginStoreRepoCheck();
                break;
        }
    }

    private void BeginFix()
    {
        // The fixes catch and log their own errors.
        if (_navigationParameter == NavigationParameters.BeginBasicCheck)
        {
            _ = BeginBasicFix();
        }
        else if (_navigationParameter == NavigationParameters.BeginStoreRepoCheck)
        {
            _ = BeginStoreFix();
        }
    }

    private void TroubleshootingHelper_BasicDialogStateChanged(Enum state)
    {
        if (state.ToString() == "Preparing" || state.ToString() == "Completed")
        {
            CurrentStateText = _localizer.GetLocalizedString($"/Troubleshooting/{state}");
        }
        else
        {
            CurrentStateText = string.Format(_localizer.GetLocalizedString("/Troubleshooting/WorkingOn"), _localizer.GetLocalizedString($"/Troubleshooting/{state}"));
        }
    }

    private string GetHelpFor(string key)
    {
        string result = _localizer.GetLocalizedString($"/Flashlight/{key}");

        return LScriptLangHelper.ExecuteScript(result, scriptArgs: "/Flashlight/");
    }

    private void GetReady()
    {
        IsLoadingVisible = false;
        IsViewResultsVisible = false;
        IsDiagnosticResultVisible = false;
        IsFixVisible = false;
        IsFixResultVisible = false;

        DiagnosticResults.Clear();
        FixResults.Clear();

        IsForwardVisible = false;
        IsCancelVisible = true;

        _view.MakeWindowModal();
    }

    private void GetReadyForCheck()
    {
        GetReady();
        IsLoadingVisible = true;
    }

    private void GetReadyForFix()
    {
        GetReady();
        IsFixVisible = true;
    }

    private void CheckViewResultActions()
    {
        IsLoadingVisible = false;
        IsViewResultsVisible = true;
        IsDiagnosticResultVisible = true;
    }

    private void CheckCompleteActions()
    {
        IsForwardVisible = true;
        IsCancelVisible = false;
        _view.MakeWindowNormal();
    }

    private void FixViewResultActions()
    {
        IsFixVisible = false;
        IsViewResultsVisible = true;
        IsFixResultVisible = true;
    }

    private void FixCompleteActions()
    {
        IsExitVisible = true;
        IsCancelVisible = false;
        _view.MakeWindowNormal();
    }

    private async Task BeginBasicCheck()
    {
        try
        {
            GetReadyForCheck();

            _diagnosticResults = await TroubleshootingService.Instance.RunBasicDiagnostic();

            CheckViewResultActions();

            DisplayDiagnosticResults<RunBasicDialogStates>((Dictionary<RunBasicDialogStates, bool>)_diagnosticResults, TroubleshootingService.BasicDiagnosticStateModelPairs);

            CheckCompleteActions();
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    private async Task BeginBasicFix()
    {
        try
        {
            GetReadyForFix();

            var result = await TroubleshootingService.Instance.FixAllBasicErrors((Dictionary<RunBasicDialogStates, bool>)_diagnosticResults);

            FixViewResultActions();

            DisplayFixResult<RunBasicDialogStates>(result, BasicDiagnosticStateModelPairs);

            FixCompleteActions();
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    private async Task BeginStoreRepoCheck()
    {
        try
        {
            GetReadyForCheck();

            _diagnosticResults = await TroubleshootingService.Instance.RunStoreDiagnostic();

            CheckViewResultActions();

            DisplayDiagnosticResults<StoreCheckStates>((Dictionary<StoreCheckStates, bool>)_diagnosticResults, TroubleshootingService.StoreDiagnosticStateModelPairs);

            CheckCompleteActions();
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    private async Task BeginStoreFix()
    {
        try
        {
            GetReadyForFix();

            var result = await TroubleshootingService.Instance.FixAllStoreErrors((Dictionary<StoreCheckStates, bool>)_diagnosticResults);

            FixViewResultActions();

            DisplayFixResult<StoreCheckStates>(result, StoreDiagnosticStateModelPairs);

            FixCompleteActions();
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    private void DisplayDiagnosticResults<T>(Dictionary<T, bool> diagnosticResult, Dictionary<T, DiagnosticStateModel> diagnosticStatesPair) where T : Enum
    {
        bool isErrorHappens = false;
        foreach (var criterion in diagnosticResult)
        {
            bool isCorrect = diagnosticStatesPair.GetValueOrDefault(criterion.Key, null)?.CorrectState == criterion.Value;
            string correctText = diagnosticStatesPair.GetValueOrDefault(criterion.Key, null)?.CorrectValueDisplayText ?? string.Empty;
            string inCorrectText = diagnosticStatesPair.GetValueOrDefault(criterion.Key, null)?.InCorrectValueDisplayText ?? string.Empty;
            DiagnosticResults.Add(new()
            {
                IsHighlighted = !isCorrect,
                CriterionName = _localizer.GetLocalizedString($"/Troubleshooting/{criterion.Key}"),
                CurrentValue = isCorrect ? _localizer.GetLocalizedString($"/Troubleshooting/{correctText}") : _localizer.GetLocalizedString($"/Troubleshooting/{inCorrectText}"),
                TargetValue = _localizer.GetLocalizedString($"/Troubleshooting/{correctText}"),
                IconGlyph = isCorrect ? "" : "",
                IsFixAvailable = false,
                IsHelpAvailable = true,
                HelpText = GetHelpFor($"{criterion.Key}"),
                HelpUrl = GetUrl(criterion.Key.ToString())
            });

            if (!isErrorHappens)
                isErrorHappens = !isCorrect;
        }

        if (isErrorHappens)
        {
            ResultTitle = _localizer.GetLocalizedString("/Troubleshooting/DiagnosticResultErrorTitle");
            ResultTip = _localizer.GetLocalizedString("/Troubleshooting/DiagnosticResultErrorTip");
        }
        else
        {
            ResultTitle = _localizer.GetLocalizedString("/Troubleshooting/DiagnosticResultTitle");
            ResultTip = _localizer.GetLocalizedString("/Troubleshooting/DiagnosticResultTip");
        }
    }

    private void DisplayFixResult<T>(Dictionary<T, FixResultModel> fixResult, Dictionary<T, DiagnosticStateModel> diagnosticStatesPair) where T : Enum
    {
        bool isErrorHappens = false;

        foreach (var crit in fixResult)
        {
            bool isCorrect = crit.Value.IsFixed;
            if (isCorrect)
                continue;

            string correctText = diagnosticStatesPair.GetValueOrDefault(crit.Key, null)?.CorrectValueDisplayText ?? string.Empty;
            string inCorrectText = diagnosticStatesPair.GetValueOrDefault(crit.Key, null)?.InCorrectValueDisplayText ?? string.Empty;
            FixResults.Add(new()
            {
                IsHighlighted = !isCorrect,
                CriterionName = _localizer.GetLocalizedString($"/Troubleshooting/{crit.Key}"),
                CurrentValue = _localizer.GetLocalizedString($"/Troubleshooting/{inCorrectText}"),
                TargetValue = _localizer.GetLocalizedString($"/Troubleshooting/{correctText}"),
                IconGlyph = isCorrect ? "" : "",
                IsFixAvailable = false,
                IsHelpAvailable = true,
                HelpText = GetHelpFor($"{crit.Key}"),
                HelpUrl = GetUrl(crit.Key.ToString())
            });

            if (!isErrorHappens)
                isErrorHappens = !isCorrect;
        }

        if (isErrorHappens)
        {
            ResultTitle = _localizer.GetLocalizedString("/Troubleshooting/FixResultErrorTitle");
            ResultTip = _localizer.GetLocalizedString("/Troubleshooting/FixResultErrorTip");
        }
        else
        {
            ResultTitle = _localizer.GetLocalizedString("/Troubleshooting/FixResultTitle");
            ResultTip = _localizer.GetLocalizedString("/Troubleshooting/FixResultTip");
            IsFixResultListVisible = false;
        }
    }

    private string GetUrl(string key)
    {
        string url = _localizer.GetLocalizedString($"/Flashlight/URL_{key}");
        if (string.IsNullOrEmpty(url))
        {
            string name = _localizer.GetLocalizedString($"/Troubleshooting/{key}");
            url = string.Format(_localizer.GetLocalizedString("/Flashlight/NetSearchPlaceholder"), name);
        }
        if (url == "$EMPTY")
            return string.Empty;
        return url;
    }
}
