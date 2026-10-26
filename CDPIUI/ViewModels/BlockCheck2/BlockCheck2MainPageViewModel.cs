using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.Helper.AddOns.BlockCheck2;
using WinUI3Localizer;

namespace CDPIUI.ViewModels.BlockCheck2;

/// <summary>
/// State of the BlockCheck2 wizard page: the current step, the wizard buttons, the input and
/// result notices, the review summary and the progress texts. The check itself is run by
/// <see cref="Session"/>.
/// </summary>
public sealed partial class BlockCheck2MainPageViewModel : ObservableObject
{
    private readonly ILocalizer _localizer = Localizer.Get();
    private readonly BlockCheck2DurationText _durationText;
    private readonly IBlockCheck2MainPageView _view;
    private bool _handlersConnected;
    private BlockCheck2PreparationPreview? _preparationPreview;
    private BlockCheck2WizardStep _currentStep = BlockCheck2WizardStep.SiteLists;

    private bool _isNextEnabled = true;
    private string? _nextButtonText;
    private bool _isCancelEnabled = true;

    private bool _isInputNoticeOpen;
    private string _inputNoticeTitle = string.Empty;
    private string _inputNoticeMessage = string.Empty;

    private bool _isResultNoticeOpen;
    private BlockCheck2NoticeSeverity _resultNoticeSeverity = BlockCheck2NoticeSeverity.Informational;
    private string _resultNoticeTitle = string.Empty;
    private string _resultNoticeMessage = string.Empty;

    public BlockCheck2MainPageViewModel(IBlockCheck2MainPageView view)
    {
        _view = view;
        _durationText = new BlockCheck2DurationText(_localizer);
        CancelCommand = new RelayCommand(CancelFromButton, () => _isCancelEnabled);
    }

    public BlockCheck2ViewModel Session { get; } = new();

    public RelayCommand CancelCommand { get; }

    public bool? IsQuickModeSelected
    {
        get => Session.SelectedRunPreset == BlockCheckRunPreset.Quick;
        set => SelectRunPreset(value, BlockCheckRunPreset.Quick);
    }

    public bool? IsBalancedModeSelected
    {
        get => Session.SelectedRunPreset is not BlockCheckRunPreset.Quick and not BlockCheckRunPreset.Exhaustive;
        set => SelectRunPreset(value, BlockCheckRunPreset.Balanced);
    }

    public bool? IsExhaustiveModeSelected
    {
        get => Session.SelectedRunPreset == BlockCheckRunPreset.Exhaustive;
        set => SelectRunPreset(value, BlockCheckRunPreset.Exhaustive);
    }

    public bool IsNextEnabled
    {
        get => _isNextEnabled;
        private set => SetProperty(ref _isNextEnabled, value);
    }

    public string? NextButtonText
    {
        get => _nextButtonText;
        private set => SetProperty(ref _nextButtonText, value);
    }

    /// <summary>Two-way: the user can close the notice.</summary>
    public bool IsInputNoticeOpen
    {
        get => _isInputNoticeOpen;
        set => SetProperty(ref _isInputNoticeOpen, value);
    }

    public string InputNoticeTitle
    {
        get => _inputNoticeTitle;
        private set => SetProperty(ref _inputNoticeTitle, value);
    }

    public string InputNoticeMessage
    {
        get => _inputNoticeMessage;
        private set => SetProperty(ref _inputNoticeMessage, value);
    }

    public bool IsResultNoticeOpen
    {
        get => _isResultNoticeOpen;
        private set => SetProperty(ref _isResultNoticeOpen, value);
    }

    public BlockCheck2NoticeSeverity ResultNoticeSeverity
    {
        get => _resultNoticeSeverity;
        private set => SetProperty(ref _resultNoticeSeverity, value);
    }

    public string ResultNoticeTitle
    {
        get => _resultNoticeTitle;
        private set => SetProperty(ref _resultNoticeTitle, value);
    }

    public string ResultNoticeMessage
    {
        get => _resultNoticeMessage;
        private set => SetProperty(ref _resultNoticeMessage, value);
    }

    public async Task OnLoadedAsync()
    {
        if (!_handlersConnected)
        {
            Session.PropertyChanged += Session_PropertyChanged;
            _localizer.LanguageChanged += Localizer_LanguageChanged;
            _handlersConnected = true;
        }

        UpdateLocalizedText();
        UpdateNavigationButtons();
        await Session.LoadSiteListsAsync();
        if (!string.IsNullOrWhiteSpace(Session.SiteListLoadError))
        {
            ShowInputNotice(Text("BlockCheck2SiteListsLoadFailedTitle"), Session.SiteListLoadError);
        }
    }

    public void OnUnloaded()
    {
        bool wasRunning = Session.IsRunning;
        Session.Cancel();
        if (!wasRunning)
        {
            Session.CleanupGeneratedSiteListsIfUnviewable();
        }
        if (!_handlersConnected)
        {
            return;
        }

        Session.PropertyChanged -= Session_PropertyChanged;
        _localizer.LanguageChanged -= Localizer_LanguageChanged;
        _handlersConnected = false;
    }

    private void Localizer_LanguageChanged(object? sender, LanguageChangedEventArgs e) =>
        UpdateLocalizedText();

    private void Session_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BlockCheck2ViewModel.CurrentPhase) or
            nameof(BlockCheck2ViewModel.ProgressCurrent) or
            nameof(BlockCheck2ViewModel.ProgressTotal) or
            nameof(BlockCheck2ViewModel.EstimatedRemainingTime) or
            nameof(BlockCheck2ViewModel.EstimatedOverallRemainingTime) or
            nameof(BlockCheck2ViewModel.State))
        {
            UpdateProgressText();
        }

        if (e.PropertyName is nameof(BlockCheck2ViewModel.CanStart) or
            nameof(BlockCheck2ViewModel.CanContinueFromTargets) or
            nameof(BlockCheck2ViewModel.CanCancel) or
            nameof(BlockCheck2ViewModel.IsRunning) or
            nameof(BlockCheck2ViewModel.LastReport) or
            nameof(BlockCheck2ViewModel.State))
        {
            UpdateNavigationButtons();
        }

        if (e.PropertyName is nameof(BlockCheck2ViewModel.SelectedRunPreset))
        {
            OnPropertiesChanged(
                nameof(IsQuickModeSelected),
                nameof(IsBalancedModeSelected),
                nameof(IsExhaustiveModeSelected));
        }
    }

    private void UpdateLocalizedText()
    {
        UpdateProgressViewMoreText();
        UpdateNavigationButtons();
        UpdateProgressText();
        if (_currentStep == BlockCheck2WizardStep.Review && _preparationPreview != null)
        {
            UpdateReviewSummary(_preparationPreview);
        }
    }

    // Only a checked radio button selects its mode; the unchecked ones of the group are ignored.
    private void SelectRunPreset(bool? isChecked, BlockCheckRunPreset preset)
    {
        if (isChecked == true)
        {
            Session.SelectedRunPreset = preset;
        }
    }

    private void GoToStep(BlockCheck2WizardStep step)
    {
        _currentStep = step;
        _view.GoToStep(step);
    }

    public async Task NextAsync()
    {
        if (_currentStep == BlockCheck2WizardStep.SiteLists)
        {
            await ContinueFromSiteListsAsync();
            return;
        }

        if (_currentStep == BlockCheck2WizardStep.Mode)
        {
            GoToStep(BlockCheck2WizardStep.Advanced);
            UpdateNavigationButtons();
            return;
        }

        if (_currentStep == BlockCheck2WizardStep.Advanced)
        {
            await ContinueFromAdvancedAsync();
            return;
        }

        if (_currentStep != BlockCheck2WizardStep.Review || !Session.CanStart)
        {
            return;
        }

        GoToStep(BlockCheck2WizardStep.Progress);
        IsResultNoticeOpen = false;
        UpdateNavigationButtons();

        await Session.StartAsync();
        HandleRunCompleted();
    }

    private async Task ContinueFromSiteListsAsync()
    {
        if (!Session.CanContinueFromTargets)
        {
            ShowInputIssue(Text("BlockCheck2ConnectionSelectionRequired"));
            return;
        }
        if (!Session.HasTargetSources)
        {
            ShowInputIssue(Text("BlockCheck2TargetSourceRequired"));
            return;
        }

        _preparationPreview = await Session.BuildPreparationPreviewAsync();
        if (!_preparationPreview.Success)
        {
            ShowInputIssue(FormatIssues(_preparationPreview.Issues));
            return;
        }

        IsInputNoticeOpen = false;
        GoToStep(BlockCheck2WizardStep.Mode);
        UpdateNavigationButtons();
    }

    private async Task ContinueFromAdvancedAsync()
    {
        _preparationPreview = await Session.BuildPreparationPreviewAsync();
        if (!_preparationPreview.Success)
        {
            GoToStep(BlockCheck2WizardStep.SiteLists);
            ShowInputIssue(FormatIssues(_preparationPreview.Issues));
            UpdateNavigationButtons();
            return;
        }

        UpdateReviewSummary(_preparationPreview);
        GoToStep(BlockCheck2WizardStep.Review);
        UpdateNavigationButtons();
    }

    /// <summary>
    /// Goes one step back. Returns <see langword="true"/> when the first step is shown and the
    /// view must leave the wizard.
    /// </summary>
    public bool GoBack()
    {
        if (Session.IsRunning)
        {
            return false;
        }

        if (_currentStep == BlockCheck2WizardStep.SiteLists)
        {
            return true;
        }
        else if (_currentStep == BlockCheck2WizardStep.Mode)
        {
            GoToStep(BlockCheck2WizardStep.SiteLists);
        }
        else if (_currentStep == BlockCheck2WizardStep.Advanced)
        {
            GoToStep(BlockCheck2WizardStep.Mode);
        }
        else if (_currentStep == BlockCheck2WizardStep.Review)
        {
            GoToStep(BlockCheck2WizardStep.Advanced);
        }
        else if (_currentStep is BlockCheck2WizardStep.Progress or BlockCheck2WizardStep.WorkComplete)
        {
            Session.Reset();
            IsResultNoticeOpen = false;
            GoToStep(BlockCheck2WizardStep.SiteLists);
        }

        UpdateNavigationButtons();
        return false;
    }

    public void ResetAdvancedOptions()
    {
        Session.ResetAdvancedOptions();
        UpdateNavigationButtons();
    }

    private void CancelFromButton()
    {
        Session.Cancel();
        SetCancelEnabled(false);
    }

    private void SetCancelEnabled(bool value)
    {
        _isCancelEnabled = value;
        CancelCommand.NotifyCanExecuteChanged();
    }

    private void ShowInputIssue(string message) =>
        ShowInputNotice(Text("BlockCheck2InputIssueTitle"), message);

    private void ShowInputNotice(string title, string? message)
    {
        InputNoticeTitle = title;
        InputNoticeMessage = message ?? string.Empty;
        IsInputNoticeOpen = true;
    }

    private string FormatIssues() => FormatIssues(Session.Issues);

    private string FormatIssues(IEnumerable<BlockCheckIssue> sourceIssues)
    {
        string[] messages = sourceIssues
            .Select(issue => BlockCheck2IssueText.GetMessage(issue))
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.CurrentCulture)
            .Take(4)
            .ToArray();
        return messages.Length == 0
            ? Text("BlockCheck2UnknownFailure")
            : string.Join(Environment.NewLine, messages);
    }

    private void UpdateNavigationButtons()
    {
        bool running = Session.IsRunning;
        bool onSiteLists = _currentStep == BlockCheck2WizardStep.SiteLists;
        bool onMode = _currentStep == BlockCheck2WizardStep.Mode;
        bool onAdvanced = _currentStep == BlockCheck2WizardStep.Advanced;
        bool onReview = _currentStep == BlockCheck2WizardStep.Review;
        bool onProgress = _currentStep == BlockCheck2WizardStep.Progress;
        bool onComplete = _currentStep == BlockCheck2WizardStep.WorkComplete;

        _view.ShowNavigationButtons(new BlockCheck2NavigationButtons(
            Exit: onComplete,
            Back: !running,
            Cancel: running,
            ViewResult: !running && onComplete && Session.LastReport != null,
            Next: !running && !onProgress && !onComplete));

        SetCancelEnabled(Session.CanCancel);
        IsNextEnabled = onSiteLists
            ? Session.HasTargetSources
            : onAdvanced
                ? Session.AdvancedOptionsValid && Session.CanContinueFromTargets
                : onMode || (onReview && Session.CanStart);
        NextButtonText = onReview
            ? Text("BlockCheck2StartButton")
            : Text("BlockCheck2NextButton");
    }

    private string Text(string key) => _localizer.GetLocalizedString(key);
}
