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
    private readonly BlockCheck2RunCompletionPresenter _completionPresenter;
    private readonly BlockCheck2ProgressPresentation _progressPresentation;
    private readonly BlockCheck2ReviewPresentation _reviewPresentation;
    private readonly IBlockCheck2MainPageView _view;
    private readonly BlockCheck2WizardNavigation _navigation;
    private bool _handlersConnected;
    private BlockCheck2PreparationPreview? _preparationPreview;


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
        _navigation = new BlockCheck2WizardNavigation(
            new BlockCheck2NavigationSource(Session), _view, Text, () => CancelCommand.NotifyCanExecuteChanged());
        _navigation.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);
        CancelCommand = new RelayCommand(CancelFromButton, () => _navigation.IsCancelEnabled);
        _completionPresenter = new BlockCheck2RunCompletionPresenter(
            () => Session.State, () => Session.NoBypassRequired, () => Session.LastReportIsHttpOnly,
            () => Session.OperationError, () => Session.HistorySaveError, Text, FormatIssues,
            (severity, title, message) =>
            {
                ResultNoticeSeverity = severity;
                ResultNoticeTitle = title;
                ResultNoticeMessage = message;
            });
        _progressPresentation = new BlockCheck2ProgressPresentation(
            () => Session.CurrentPhase, () => Session.IsRunning,
            () => Session.ProgressCurrent, () => Session.ProgressTotal,
            () => Session.EstimatedRemainingTime, () => Session.EstimatedOverallRemainingTime,
            Text, _durationText.FormatRemaining);
        _progressPresentation.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);
        _reviewPresentation = new BlockCheck2ReviewPresentation(
            new BlockCheck2ReviewSource(Session), Text, _durationText.FormatDuration);
        _reviewPresentation.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);
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
        get => _navigation.IsNextEnabled;
        private set => _navigation.IsNextEnabled = value;
    }

    public string? NextButtonText
    {
        get => _navigation.NextButtonText;
        private set => _navigation.NextButtonText = value;
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
        if (_navigation.CurrentStep == BlockCheck2WizardStep.Review && _preparationPreview != null)
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

    private void GoToStep(BlockCheck2WizardStep step) =>
        _navigation.GoToStep(step);

    public async Task NextAsync()
    {
        if (_navigation.CurrentStep == BlockCheck2WizardStep.SiteLists)
        {
            await ContinueFromSiteListsAsync();
            return;
        }

        if (_navigation.CurrentStep == BlockCheck2WizardStep.Mode)
        {
            GoToStep(BlockCheck2WizardStep.Advanced);
            UpdateNavigationButtons();
            return;
        }

        if (_navigation.CurrentStep == BlockCheck2WizardStep.Advanced)
        {
            await ContinueFromAdvancedAsync();
            return;
        }

        if (_navigation.CurrentStep != BlockCheck2WizardStep.Review || !Session.CanStart)
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

        if (_navigation.CurrentStep == BlockCheck2WizardStep.SiteLists)
        {
            return true;
        }
        else if (_navigation.CurrentStep == BlockCheck2WizardStep.Mode)
        {
            GoToStep(BlockCheck2WizardStep.SiteLists);
        }
        else if (_navigation.CurrentStep == BlockCheck2WizardStep.Advanced)
        {
            GoToStep(BlockCheck2WizardStep.Mode);
        }
        else if (_navigation.CurrentStep == BlockCheck2WizardStep.Review)
        {
            GoToStep(BlockCheck2WizardStep.Advanced);
        }
        else if (_navigation.CurrentStep is BlockCheck2WizardStep.Progress or BlockCheck2WizardStep.WorkComplete)
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

    private void SetCancelEnabled(bool value) =>
        _navigation.SetCancelEnabled(value);

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

    private void UpdateNavigationButtons() =>
        _navigation.UpdateNavigationButtons();

    private string Text(string key) => _localizer.GetLocalizedString(key);
}
