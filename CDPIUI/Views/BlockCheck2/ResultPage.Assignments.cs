using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.AddOns.BlockCheck2.Reporting;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Universal;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.AddOns.BlockCheck2;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Helper.UserExperience;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Win32;
using Windows.ApplicationModel.DataTransfer;
using WinUI3Localizer;

namespace CDPIUI.Views.BlockCheck2;

public sealed partial class ResultPage
{
    private void AddForSiteButton_Click(object sender, RoutedEventArgs e)
    {
        BlockCheck2StrategyEvidenceItem? item = SelectedStrategy;
        if (item == null)
        {
            return;
        }
        ShowAssignmentResult(ViewModel.AddForSite(item));
    }

    private async void AddForListButton_Click(object sender, RoutedEventArgs e)
    {
        BlockCheck2StrategyEvidenceItem? item = SelectedStrategy;
        if (item == null)
        {
            return;
        }
        string[] paths = ViewModel.GetSiteListPaths(item).ToArray();
        if (paths.Length == 0)
        {
            return;
        }

        string selectedPath = paths[0];
        if (paths.Length > 1)
        {
            ComboBox selector = new()
            {
                ItemsSource = paths,
                SelectedIndex = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            ContentDialog dialog = new()
            {
                XamlRoot = XamlRoot,
                Title = Text("BlockCheck2ChooseSiteListTitle"),
                Content = selector,
                PrimaryButtonText = Text("BlockCheck2AddButtonText"),
                CloseButtonText = Text("BlockCheck2CancelDialogButtonText"),
                DefaultButton = ContentDialogButton.Primary,
            };
            if (await dialog.ShowQueuedAsync() != ContentDialogResult.Primary || selector.SelectedItem is not string path)
            {
                return;
            }
            selectedPath = path;
        }
        ShowAssignmentResult(ViewModel.AddForSiteList(item, selectedPath));
    }

    private void ShowAssignmentResult(bool added)
    {
        if (added)
        {
            ShowActionMessage(
                InfoBarSeverity.Success,
                Text("BlockCheck2AssignmentAddedTitle"),
                Text("BlockCheck2AssignmentAddedMessage"));
        }
        else
        {
            ShowActionMessage(
                InfoBarSeverity.Warning,
                Text("BlockCheck2AssignmentNotAddedTitle"),
                Text("BlockCheck2AssignmentDuplicateMessage"));
        }
        UpdateAssignmentView();
        UpdateActionButtons();
    }

    private void ManualAssignmentsListView_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateAssignmentButtons();

    private void MoveAssignmentUpButton_Click(object sender, RoutedEventArgs e)
    {
        BlockCheck2ManualAssignmentItem? selected = SelectedAssignment;
        if (selected != null && ViewModel.MoveAssignment(selected, -1))
        {
            UpdateAssignmentView();
            ManualAssignmentsListView.SelectedItem = selected;
        }
    }

    private void MoveAssignmentDownButton_Click(object sender, RoutedEventArgs e)
    {
        BlockCheck2ManualAssignmentItem? selected = SelectedAssignment;
        if (selected != null && ViewModel.MoveAssignment(selected, 1))
        {
            UpdateAssignmentView();
            ManualAssignmentsListView.SelectedItem = selected;
        }
    }

    private void RemoveAssignmentButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedAssignment == null)
        {
            return;
        }
        ViewModel.RemoveAssignment(SelectedAssignment);
        UpdateAssignmentView();
    }

    private void ClearAssignmentsButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearManualAssignments();
        UpdateAssignmentView();
    }

    private void ManualIssueQuickFixButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            element.DataContext is not BlockCheck2ManualIssueItem issue ||
            !ViewModel.ApplyManualIssueQuickFix(issue))
        {
            return;
        }

        ShowActionMessage(
            InfoBarSeverity.Success,
            Text("BlockCheck2ManualIssueQuickFixCompletedTitle"),
            Text("BlockCheck2ManualScopeOverlapAcceptedMessage"));
        UpdateAssignmentView();
    }

    private void UpdateAssignmentView()
    {
        bool hasAssignments = ViewModel.ManualAssignments.Count > 0;
        EmptyAssignmentsPanel.Visibility = hasAssignments ? Visibility.Collapsed : Visibility.Visible;
        ManualAssignmentsListView.Visibility = hasAssignments ? Visibility.Visible : Visibility.Collapsed;
        ManualIssuesListView.Visibility = ViewModel.ManualIssues.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        NoManualIssuesTextBlock.Visibility = ViewModel.ManualIssues.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        PresetPreviewEditor.CommandText = ComponentCommandLineFormatter.FormatByFlags(
            ViewModel.EffectivePresetArguments);
        UpdateAssignmentButtons();
        UpdateActionButtons();
    }

    private void UpdateAssignmentButtons()
    {
        int index = ManualAssignmentsListView.SelectedIndex;
        MoveAssignmentUpButton.IsEnabled = index > 0;
        MoveAssignmentDownButton.IsEnabled = index >= 0 && index < ViewModel.ManualAssignments.Count - 1;
        RemoveAssignmentButton.IsEnabled = index >= 0;
    }
}
