using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.AddOns.BlockCheck2.Reporting;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Universal;
using CDPIUI.Core.Store.Data;
using CDPIUI.ViewModels;
using CDPIUI.ViewModels.BlockCheck2;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Win32;
using Windows.ApplicationModel.DataTransfer;
using WinUI3Localizer;

namespace CDPIUI.Views.BlockCheck2;

public sealed partial class ResultPage : TemplatePage, IStatusNotificationSource, INotifyPropertyChanged, IBlockCheck2ResultPageView
{
    private readonly ILocalizer _localizer = Localizer.Get();

    public BlockCheck2ResultPageViewModel ViewModel { get; }
    public event EventHandler<StatusNotificationRequestedEventArgs>? StatusNotificationRequested;
    public event PropertyChangedEventHandler? PropertyChanged;

    public ConfigMakerUserControl PresetPreviewMenuTarget => PresetPreviewEditor;

    public bool CanOpenPresetReview => ViewModel.Result.CanUseConfig;
    public bool CanCopySelectedStrategy => SelectedStrategy != null;
    public bool CanTestSelectedStrategy => ViewModel.IsTestStrategyEnabled;
    public bool CanToggleComponentManualTest => ViewModel.IsManualTestEnabled;
    public bool IsComponentManualTestRunning => ViewModel.IsManualTestRunning;
    public bool CanAddSelectedStrategyForSite => ViewModel.IsAddForSiteEnabled;
    public bool CanAddSelectedStrategyForList => ViewModel.IsAddForListEnabled;
    public bool IsStrategyOverviewViewActive => !StrategySemanticZoom.IsZoomedInViewActive;
    public bool IsStrategyDetailsViewActive => StrategySemanticZoom.IsZoomedInViewActive;
    public bool CanSavePresetPreviewText => ViewModel.Result.CanUseConfig;
    public bool IsPresetPreviewCommandPanelVisible => PresetPreviewEditor.IsCommandPanelVisible;
    public bool IsPresetPreviewBottomPanelVisible => PresetPreviewEditor.IsBottomPanelVisible;
    public bool IsPresetPreviewFilesPanelVisible => PresetPreviewEditor.IsPresetFilesPanelVisible;
    public bool HasPresetPreviewFiles => PresetPreviewEditor.HasPresetFiles;
    public bool HasPresetPreviewGroups => PresetPreviewEditor.HasPresetGroups;
    public bool CanStartPresetPreviewTest => !PresetPreviewEditor.IsTesting;
    public bool CanStopPresetPreviewTest => PresetPreviewEditor.IsTesting;

    public ResultPage()
    {
        ViewModel = new BlockCheck2ResultPageViewModel(this, DispatcherQueue);
        InitializeComponent();
        DataContext = this;
        PresetPreviewEditor.ComponentId = HardcodedItemIds.ComponentIds[Components.Zapret2];
        PresetPreviewEditor.SetCommandPanelVisible(false);
        PresetPreviewEditor.SetBottomPanelVisible(false);
        PresetPreviewEditor.UseInlineStatusMessages = false;
        PresetPreviewEditor.StatusNotificationRequested += PresetPreviewEditor_StatusNotificationRequested;
        PresetPreviewEditor.TestStateChanged += PresetPreviewEditor_MenuStateChanged;
        PresetPreviewEditor.EditorReadOnlyChanged += PresetPreviewEditor_MenuStateChanged;
        PresetPreviewEditor.PanelStateChanged += PresetPreviewEditor_MenuStateChanged;
        BindViewModelCollections();
        Loaded += ResultPage_Loaded;
        Unloaded += ResultPage_Unloaded;
    }

    public BlockCheck2StrategyEvidenceItem? SelectedStrategy =>
        StrategyListView.SelectedItem as BlockCheck2StrategyEvidenceItem;

    public int SelectedAssignmentIndex => ManualAssignmentsListView.SelectedIndex;

    public BlockCheck2ManualAssignmentItem? SelectedAssignment
    {
        get => ManualAssignmentsListView.SelectedItem as BlockCheck2ManualAssignmentItem;
        set => ManualAssignmentsListView.SelectedItem = value;
    }

    public static InfoBarSeverity ToInfoBarSeverity(BlockCheck2NoticeSeverity severity) => severity switch
    {
        BlockCheck2NoticeSeverity.Success => InfoBarSeverity.Success,
        BlockCheck2NoticeSeverity.Warning => InfoBarSeverity.Warning,
        BlockCheck2NoticeSeverity.Error => InfoBarSeverity.Error,
        _ => InfoBarSeverity.Informational,
    };

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        switch (e.Parameter)
        {
            case BlockCheck2ResultViewModel viewModel:
                LoadViewModel(viewModel);
                break;
            case BlockCheckResultSession session:
                LoadSession(session);
                break;
            case BlockCheckReport report:
                LoadReport(report);
                break;
        }
    }

    public void LoadReport(BlockCheckReport report) => ViewModel.LoadReport(report);

    public void LoadViewModel(BlockCheck2ResultViewModel viewModel)
    {
        ViewModel.LoadResult(viewModel);
        BindViewModelCollections();
        ViewModel.UpdateView();
        if (ViewModel.Result.StrategyEvidence.Count > 0 && StrategyListView.SelectedIndex < 0)
        {
            StrategyListView.SelectedIndex = 0;
        }
    }

    public void LoadSession(BlockCheckResultSession session)
    {
        ViewModel.LoadSession(session);
        if (ViewModel.Result.StrategyEvidence.Count > 0)
        {
            StrategyListView.SelectedIndex = 0;
        }
    }

    public Task StopTestAsync(bool showCompletionMessage = false) => ViewModel.StopTestAsync(showCompletionMessage);

    public Task StartTestAsync() => ViewModel.StartTestAsync();

    public void NavigateToReview()
    {
        if (ViewModel.Result.CanUseConfig)
        {
            Frame.Navigate(typeof(PresetReviewPage), ViewModel.Result);
        }
    }

    public void OpenPresetBuilder() => ResultSelector.SelectIndex(1);

    public void SaveJsonReportFromMenu() => _ = ViewModel.SaveJsonReportAsync();

    public void SaveTextReportFromMenu() => _ = ViewModel.SaveTextReportAsync();

    public void SelectFirstStrategy() => StrategyListView.SelectedIndex = 0;

    public void SetPresetCommandText(string commandText) => PresetPreviewEditor.CommandText = commandText;

    public void NotifyMenuStateChanged() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));

    public void ShowStatus(BlockCheck2NoticeSeverity severity, string title, string message) =>
        StatusNotificationRequested?.Invoke(
            this,
            new StatusNotificationRequestedEventArgs(ToInfoBarSeverity(severity), title, message));

    public async Task<string?> ChooseSiteListAsync(IReadOnlyList<string> paths)
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
            return null;
        }
        return path;
    }

    public async Task<bool> ConfirmReplaceRunningComponentAsync()
    {
        ContentDialog confirmation = new()
        {
            XamlRoot = XamlRoot,
            Title = Text("ConfigMakerReplaceRunningComponentTitle"),
            Content = Text("ConfigMakerReplaceRunningComponentMessage"),
            PrimaryButtonText = Text("Continue"),
            CloseButtonText = Text("Cancel"),
            DefaultButton = ContentDialogButton.Close,
        };
        return await confirmation.ShowQueuedAsync() == ContentDialogResult.Primary;
    }

    public string? PickSaveFile(string title, string fileName, string filter)
    {
        SaveFileDialog dialog = new()
        {
            Title = title,
            FileName = fileName,
            Filter = filter,
            OverwritePrompt = true,
            RestoreDirectory = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public void CopyToClipboard(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }

    private void ResultPage_Loaded(object sender, RoutedEventArgs e) => ViewModel.OnLoaded();

    private void ResultPage_Unloaded(object sender, RoutedEventArgs e) => ViewModel.OnUnloaded();

    private void BindViewModelCollections()
    {
        GroupedStrategiesSource.Source = ViewModel.Result.StrategyGroups;
        ManualAssignmentsListView.ItemsSource = ViewModel.Result.ManualAssignments;
        ManualIssuesListView.ItemsSource = ViewModel.Result.ManualIssues;
        IssuesListView.ItemsSource = ViewModel.Result.Issues;
        ProbesListView.ItemsSource = ViewModel.Result.Probes;
    }

    private void StrategySearchBox_TextChanged(
        AutoSuggestBox sender,
        AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            ApplyStrategyView();
        }
    }

    private void StrategyViewOption_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ApplyStrategyView();

    private void ApplyStrategyView()
    {
        if (StrategyListView == null || StrategyCountTextBlock == null || NoStrategiesPanel == null)
        {
            return;
        }

        ViewModel.ApplyStrategyView(
            StrategySearchBox?.Text,
            Math.Max(0, StrategySortComboBox?.SelectedIndex ?? 0),
            Math.Max(0, StrategyFilterComboBox?.SelectedIndex ?? 0));
    }

    private void StrategiesPanel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (StrategyTableBorder == null || StrategyToolbar == null || e.NewSize.Height <= 0)
        {
            return;
        }

        const double minimumTableHeight = 180;
        const double minimumDetailsHeight = 150;
        const double splitterHeight = 16;
        double maximumTableHeight = Math.Max(
            minimumTableHeight,
            e.NewSize.Height - StrategyToolbar.ActualHeight - 8 - splitterHeight - minimumDetailsHeight);
        StrategyTableBorder.MaxHeight = maximumTableHeight;
        if (StrategyTableBorder.Height > maximumTableHeight)
        {
            StrategyTableBorder.Height = maximumTableHeight;
        }
        else
        {
            StrategyTableBorder.Height = maximumTableHeight - 100;
        }
    }

    private void StrategyGroupHeader_Click(object sender, RoutedEventArgs e)
    {
        StrategySemanticZoom.IsZoomedInViewActive = false;
        NotifyMenuStateChanged();
    }

    private void SitesOverviewListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not ICollectionViewGroup collectionGroup ||
            collectionGroup.Group is not BlockCheck2SiteStrategyGroup siteGroup ||
            siteGroup.Count == 0)
        {
            return;
        }

        BlockCheck2StrategyEvidenceItem firstItem = siteGroup[0];
        StrategySemanticZoom.IsZoomedInViewActive = true;
        NotifyMenuStateChanged();
        DispatcherQueue.TryEnqueue(() =>
        {
            StrategyListView.SelectedItem = firstItem;
            StrategyListView.ScrollIntoView(firstItem, ScrollIntoViewAlignment.Leading);
        });
    }

    private void StrategyListView_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ViewModel.UpdateSelectedStrategy();

    private async void TestStrategyButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.TestSelectedStrategyAsync();

    private async void ComponentManualTestButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.ToggleManualTestFromButtonAsync();

    private async void ComponentManualTestMenuItem_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.ToggleManualTestAsync();

    private void AddForSiteButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.AddSelectedStrategyForSite();

    private async void AddForListButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.AddSelectedStrategyForListAsync();

    private void ManualAssignmentsListView_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ViewModel.UpdateAssignmentButtons();

    private void MoveAssignmentUpButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.MoveSelectedAssignment(-1);

    private void MoveAssignmentDownButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.MoveSelectedAssignment(1);

    private void RemoveAssignmentButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.RemoveSelectedAssignment();

    private void ClearAssignmentsButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.ClearAssignments();

    private void ManualIssueQuickFixButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: BlockCheck2ManualIssueItem issue })
        {
            ViewModel.ApplyManualIssueQuickFix(issue);
        }
    }

    private void ReviewPresetButton_Click(object sender, RoutedEventArgs e) => NavigateToReview();

    private void ShowStrategyOverviewMenuItem_Click(object sender, RoutedEventArgs e)
    {
        StrategySemanticZoom.IsZoomedInViewActive = false;
        NotifyMenuStateChanged();
    }

    private void ShowStrategyDetailsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        StrategySemanticZoom.IsZoomedInViewActive = true;
        NotifyMenuStateChanged();
    }

    private async void PresetPreviewStartTestMenuItem_Click(object sender, RoutedEventArgs e) =>
        await PresetPreviewEditor.StartTestAsync();

    private async void PresetPreviewStopTestMenuItem_Click(object sender, RoutedEventArgs e) =>
        await PresetPreviewEditor.StopTestAsync(showCompletionMessage: true);

    private void CopyViewAppBarButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.CopySelectedStrategyArguments();

    private void CopyFullAppBarButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.CopyFullSelectedStrategyArguments();

    private void PresetPreviewEditor_StatusNotificationRequested(
        object? sender,
        StatusNotificationRequestedEventArgs e) =>
        StatusNotificationRequested?.Invoke(this, e);

    private void PresetPreviewEditor_MenuStateChanged(object? sender, EventArgs e) =>
        NotifyMenuStateChanged();

    private string Text(string key) => _localizer.GetLocalizedString(key);
}
