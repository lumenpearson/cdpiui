using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Dialogs.BlockCheck2;
using CDPIUI.Core.Data;
using CDPIUI.Core.System;
using CDPIUI.Helper.AddOns.BlockCheck2;
using CDPIUI.Shared;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUI3Localizer;

namespace CDPIUI.Views.BlockCheck2;

public sealed partial class MainPage : TemplatePage
{
    private readonly ILocalizer _localizer = Localizer.Get();
    private readonly BlockCheck2DurationText _durationText;
    private bool _handlersConnected;
    private BlockCheck2PreparationPreview? _preparationPreview;

    public BlockCheck2ViewModel ViewModel { get; } = new();

    public MainPage()
    {
        _durationText = new BlockCheck2DurationText(_localizer);
        InitializeComponent();
        RadioButton selectedModeButton = ViewModel.SelectedRunPreset switch
        {
            BlockCheckRunPreset.Quick => QuickModeRadioButton,
            BlockCheckRunPreset.Exhaustive => ExhaustiveModeRadioButton,
            _ => BalancedModeRadioButton,
        };
        selectedModeButton.IsChecked = true;
        Loaded += MainPage_Loaded;
        Unloaded += MainPage_Unloaded;

        IsForwardAnimationToPageAvailable = true;
        ElementToAnimateForwardConnectedAnimation = UtilityButtons;
    }

    public void CancelRunningSession() => ViewModel.Cancel();

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_handlersConnected)
        {
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            _localizer.LanguageChanged += Localizer_LanguageChanged;
            _handlersConnected = true;
        }

        UpdateLocalizedText();
        UpdateNavigationButtons();
        await ViewModel.LoadSiteListsAsync();
        if (!string.IsNullOrWhiteSpace(ViewModel.SiteListLoadError))
        {
            InputInfoBar.Title = Text("BlockCheck2SiteListsLoadFailedTitle");
            InputInfoBar.Message = ViewModel.SiteListLoadError;
            InputInfoBar.IsOpen = true;
        }
    }

    private void MainPage_Unloaded(object sender, RoutedEventArgs e)
    {
        bool wasRunning = ViewModel.IsRunning;
        CancelRunningSession();
        if (!wasRunning)
        {
            ViewModel.CleanupGeneratedSiteListsIfUnviewable();
        }
        if (!_handlersConnected)
        {
            return;
        }

        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _localizer.LanguageChanged -= Localizer_LanguageChanged;
        _handlersConnected = false;
    }

    private void Localizer_LanguageChanged(object? sender, LanguageChangedEventArgs e) =>
        UpdateLocalizedText();

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
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
    }

    private void UpdateLocalizedText()
    {
        ProgressViewMoreText.Text = Text(AdditionalProgressInfo.Visibility == Visibility.Visible
            ? "ViewLess"
            : "ViewMore");
        UpdateNavigationButtons();
        UpdateProgressText();
        if (MainContent.SelectedItem == ReviewStep && _preparationPreview != null)
        {
            UpdateReviewSummary(_preparationPreview);
        }
    }

    private void RunModeRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, QuickModeRadioButton))
        {
            ViewModel.SelectedRunPreset = BlockCheckRunPreset.Quick;
        }
        else if (ReferenceEquals(sender, ExhaustiveModeRadioButton))
        {
            ViewModel.SelectedRunPreset = BlockCheckRunPreset.Exhaustive;
        }
        else
        {
            ViewModel.SelectedRunPreset = BlockCheckRunPreset.Balanced;
        }
    }

    private async void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (MainContent.SelectedItem == SiteListsStep)
        {
            await ContinueFromSiteListsAsync();
            return;
        }

        if (MainContent.SelectedItem == ModeStep)
        {
            MainContent.GoTo(AdvancedStep);
            UpdateNavigationButtons();
            return;
        }

        if (MainContent.SelectedItem == AdvancedStep)
        {
            await ContinueFromAdvancedAsync();
            return;
        }

        if (MainContent.SelectedItem != ReviewStep || !ViewModel.CanStart)
        {
            return;
        }

        MainContent.GoTo(ProgressStep);
        ProgressInfoBar.IsOpen = false;
        UpdateNavigationButtons();

        await ViewModel.StartAsync();
        HandleRunCompleted();
    }

    private async Task ContinueFromSiteListsAsync()
    {
        if (!ViewModel.CanContinueFromTargets)
        {
            ShowInputIssue(Text("BlockCheck2ConnectionSelectionRequired"));
            return;
        }
        if (!ViewModel.HasTargetSources)
        {
            ShowInputIssue(Text("BlockCheck2TargetSourceRequired"));
            return;
        }

        _preparationPreview = await ViewModel.BuildPreparationPreviewAsync();
        if (!_preparationPreview.Success)
        {
            ShowInputIssue(FormatIssues(_preparationPreview.Issues));
            return;
        }

        InputInfoBar.IsOpen = false;
        MainContent.GoTo(ModeStep);
        UpdateNavigationButtons();
    }

    private async Task ContinueFromAdvancedAsync()
    {
        _preparationPreview = await ViewModel.BuildPreparationPreviewAsync();
        if (!_preparationPreview.Success)
        {
            MainContent.GoTo(SiteListsStep);
            ShowInputIssue(FormatIssues(_preparationPreview.Issues));
            UpdateNavigationButtons();
            return;
        }

        UpdateReviewSummary(_preparationPreview);
        MainContent.GoTo(ReviewStep);
        UpdateNavigationButtons();
    }

    private void ResetAdvancedButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ResetAdvancedOptions();
        UpdateNavigationButtons();
    }


    private void ShowInputIssue(string message)
    {
        InputInfoBar.Title = Text("BlockCheck2InputIssueTitle");
        InputInfoBar.Message = message;
        InputInfoBar.IsOpen = true;
    }

    private string FormatIssues() => FormatIssues(ViewModel.Issues);

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

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsRunning)
        {
            return;
        }

        if (MainContent.SelectedItem == SiteListsStep)
        {
            Debug.WriteLine(Frame.BackStackDepth);
            if (Frame.CanGoBack)
            {
                PrepareToConnectedBackwardAnimate(UtilityButtons);
                Frame.GoBack();
            }
            else
            {
                CreateConfigUtilWindow.Instance.Close();
            }
            return;
        }
        else if (MainContent.SelectedItem == ModeStep)
        {
            MainContent.GoTo(SiteListsStep);
        }
        else if (MainContent.SelectedItem == AdvancedStep)
        {
            MainContent.GoTo(ModeStep);
        }
        else if (MainContent.SelectedItem == ReviewStep)
        {
            MainContent.GoTo(AdvancedStep);
        }
        else if (MainContent.SelectedItem == ProgressStep || MainContent.SelectedItem == WorkComplete)
        {
            ViewModel.Reset();
            ProgressInfoBar.IsOpen = false;
            MainContent.GoTo(SiteListsStep);
        }

        UpdateNavigationButtons();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Cancel();
        CancelButton.IsEnabled = false;
    }

    private async void ViewResultButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.LastReport == null)
        {
            return;
        }

        BlockCheck2ResultWindow window = await ((App)Application.Current)
            .SafeCreateNewWindow<BlockCheck2ResultWindow>();
        if (ViewModel.LastSession != null)
        {
            window.ShowSession(ViewModel.LastSession);
        }
        else
        {
            window.ShowReport(ViewModel.LastReport);
        }

        CreateConfigUtilWindow.Instance?.Close();
    }

    private void UpdateNavigationButtons()
    {
        bool running = ViewModel.IsRunning;
        bool onSiteLists = MainContent.SelectedItem == null || MainContent.SelectedItem == SiteListsStep;
        bool onMode = MainContent.SelectedItem == ModeStep;
        bool onAdvanced = MainContent.SelectedItem == AdvancedStep;
        bool onReview = MainContent.SelectedItem == ReviewStep;
        bool onProgress = MainContent.SelectedItem == ProgressStep;
        bool onComplete = MainContent.SelectedItem == WorkComplete;

        UtilityButtons.SetButtonVisibilities(
            (ExitButton, onComplete ? Visibility.Visible : Visibility.Collapsed),
            (BackButton, !running ? Visibility.Visible : Visibility.Collapsed),
            (CancelButton, running ? Visibility.Visible : Visibility.Collapsed),
            (ViewResultButton, !running && onComplete && ViewModel.LastReport != null
                ? Visibility.Visible
                : Visibility.Collapsed),
            (NextButton, !running && !onProgress && !onComplete
                ? Visibility.Visible
                : Visibility.Collapsed));

        CancelButton.IsEnabled = ViewModel.CanCancel;
        NextButton.IsEnabled = onSiteLists
            ? ViewModel.HasTargetSources
            : onAdvanced
                ? ViewModel.AdvancedOptionsValid && ViewModel.CanContinueFromTargets
                : onMode || (onReview && ViewModel.CanStart);
        NextButton.Content = onReview
            ? Text("BlockCheck2StartButton")
            : Text("BlockCheck2NextButton");
    }

    private string Text(string key) => _localizer.GetLocalizedString(key);

    private void RadioButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is RadioButton radioButton)
        {
            radioButton.IsChecked = true;
        }
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        CreateConfigUtilWindow.Instance.Close();
    }
}
