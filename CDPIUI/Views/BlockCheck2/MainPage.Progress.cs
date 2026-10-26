using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Dialogs.BlockCheck2;
using CDPIUI.Core.Data;
using CDPIUI.Core.System;
using CDPIUI.Helper.AddOns.BlockCheck2;
using CDPIUI.Shared;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WinUI3Localizer;
using TextDecorations = Windows.UI.Text.TextDecorations;

namespace CDPIUI.Views.BlockCheck2;

public sealed partial class MainPage
{
    private void UpdateProgressText()
    {
        ProgressPhaseTextBlock.Text = ViewModel.CurrentPhase switch
        {
            BlockCheckScanPhase.BaselineProbing => Text("BlockCheck2PhaseBaseline"),
            BlockCheckScanPhase.StartingStrategy => Text("BlockCheck2PhaseStartingStrategy"),
            BlockCheckScanPhase.Probing => Text("BlockCheck2PhaseProbing"),
            BlockCheckScanPhase.StrategyCompleted => Text("BlockCheck2PhaseStrategyCompleted"),
            BlockCheckScanPhase.ValidatingPreset => Text("BlockCheck2PhaseValidatingConfig"),
            BlockCheckScanPhase.RepairingPreset => Text("BlockCheck2PhaseRepairingConfig"),
            _ => Text("BlockCheck2PhasePreparing"),
        };
        ProgressCounterTextBlock.Text = string.Format(
            Text("BlockCheck2ProgressCounterFormat"),
            ViewModel.ProgressCurrent,
            ViewModel.ProgressTotal);
        StageRemainingTimeTextBlock.Text = FormatRemainingTime();
        OverallRemainingTimeTextBlock.Text = FormatOverallRemainingTime();
    }

    private string FormatRemainingTime()
    {
        if (!ViewModel.IsRunning && ViewModel.CurrentPhase.HasValue)
        {
            return "—";
        }

        return _durationText.FormatRemaining(
            ViewModel.EstimatedRemainingTime,
            "BlockCheck2RemainingTimeCalculating");
    }

    private string FormatOverallRemainingTime()
    {
        if (!ViewModel.IsRunning && ViewModel.CurrentPhase.HasValue)
        {
            return "—";
        }

        return _durationText.FormatRemaining(
            ViewModel.EstimatedOverallRemainingTime,
            ViewModel.CurrentPhase == BlockCheckScanPhase.BaselineProbing
                ? "BlockCheck2OverallTimeCalculating"
                : "BlockCheck2RemainingTimeCalculating");
    }

    private void ProgressViewMoreButton_Click(object sender, RoutedEventArgs e)
    {
        AdditionalProgressInfo.Visibility = AdditionalProgressInfo.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
        ProgressViewMoreText.Text = Text(AdditionalProgressInfo.Visibility == Visibility.Visible
            ? "ViewLess"
            : "ViewMore");
        ProgressViewMoreText.TextDecorations = TextDecorations.Underline;
    }

    private void ProgressViewMoreButton_PointerEntered(
        object sender,
        Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) =>
        ProgressViewMoreText.TextDecorations = TextDecorations.None;

    private void ProgressViewMoreButton_PointerExited(
        object sender,
        Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) =>
        ProgressViewMoreText.TextDecorations = TextDecorations.Underline;

    private void HandleRunCompleted()
    {
        if (ViewModel.State == BlockCheckSessionState.InputInvalid)
        {
            MainContent.GoTo(SiteListsStep);
            ShowInputIssue(FormatIssues());
            UpdateNavigationButtons();
            return;
        }

        MainContent.GoTo(WorkComplete);

        ProgressInfoBar.IsOpen = true;
        if (ViewModel.State == BlockCheckSessionState.Completed && ViewModel.NoBypassRequired)
        {
            ProgressInfoBar.Severity = InfoBarSeverity.Informational;
            ProgressInfoBar.Title = Text("BlockCheck2NoBypassRequiredTitle");
            ProgressInfoBar.Message = Text(ViewModel.LastReportIsHttpOnly
                ? "BlockCheck2HttpOnlyNoBypassRequiredMessage"
                : "BlockCheck2NoBypassRequiredMessage");
        }
        else if (ViewModel.State == BlockCheckSessionState.Completed)
        {
            ProgressInfoBar.Severity = InfoBarSeverity.Success;
            ProgressInfoBar.Title = Text("BlockCheck2CompletedTitle");
            ProgressInfoBar.Message = Text("BlockCheck2CompletedMessage");
        }
        else if (ViewModel.State == BlockCheckSessionState.CompletedWithWarnings)
        {
            ProgressInfoBar.Severity = InfoBarSeverity.Warning;
            ProgressInfoBar.Title = Text("BlockCheck2BestEffortCompletedTitle");
            ProgressInfoBar.Message = Text("BlockCheck2BestEffortCompletedMessage");
        }
        else if (ViewModel.State == BlockCheckSessionState.Canceled)
        {
            ProgressInfoBar.Severity = InfoBarSeverity.Informational;
            ProgressInfoBar.Title = Text("BlockCheck2CanceledTitle");
            ProgressInfoBar.Message = Text("BlockCheck2CanceledMessage");
        }
        else
        {
            ProgressInfoBar.Severity = InfoBarSeverity.Error;
            ProgressInfoBar.Title = Text("BlockCheck2FailedTitle");
            ProgressInfoBar.Message = string.IsNullOrWhiteSpace(ViewModel.OperationError)
                ? FormatIssues()
                : ViewModel.OperationError;
        }

        if (!string.IsNullOrWhiteSpace(ViewModel.HistorySaveError))
        {
            ProgressInfoBar.Message = string.Concat(
                ProgressInfoBar.Message,
                Environment.NewLine,
                Text("BlockCheck2HistorySaveFailedMessage"),
                " ",
                ViewModel.HistorySaveError);
        }

        UpdateNavigationButtons();
    }
}
