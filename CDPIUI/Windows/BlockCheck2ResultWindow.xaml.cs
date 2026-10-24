using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.AddOns.BlockCheck2.Reporting;
using CDPIUI.Controls.Universal;
using CDPIUI.Core.Store.Database;
using CDPIUI.Default;
using CDPIUI.Helper.UserExperience;
using CDPIUI.Helper.WindowHelper;
using CDPIUI.ViewModels;
using CDPIUI.Views.BlockCheck2;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using WinUI3Localizer;
using RoutedEventArgs = Microsoft.UI.Xaml.RoutedEventArgs;
using WpfSize = System.Windows.Size;

namespace CDPIUI;

public sealed partial class BlockCheck2ResultWindow : TemplateWindow
{
    private readonly ILocalizer _localizer = Localizer.Get();
    private readonly BlockCheck2ResultViewModel _viewModel = new();
    private readonly BlockCheckReportSerializer _reportSerializer = new();
    private bool _modeChoiceShown;
    private IStatusNotificationSource? _notificationSource;
    private readonly BlockCheckReportImportService _importService = new();

    private readonly ObservableCollection<string> _statusBarItems = [];

    public BlockCheck2ResultWindow()
    {
        InitializeComponent();

        WindowTitle = Localizer.Get().GetLocalizedString("BlockCheck2ResultWindowTitle");
        IconUri = @"Assets/Icons/GoodCheck.ico";
        CustomTitleBarUserControl = TitleBarUserControl;
        MainFrame = ContentFrame;
        WindowMinSize = new WpfSize(960, 640);

        WindowsPositionHelper.TrySetMicaBackdrop(true, this, MainGrid);

        ContentFrame.Navigated += ContentFrame_Navigated;
        ContentFrame.Loaded += ContentFrame_Loaded;
        _viewModel.ManualAssignments.CollectionChanged += (_, _) => UpdateChrome();
        _viewModel.Draft.PropertyChanged += (_, _) => UpdateChrome();
        ContentFrame.Navigate(typeof(ResultPage), _viewModel);

        StatusBarListView.ItemsSource = _statusBarItems;
    }

    public void ShowReport(BlockCheckReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        _viewModel.Load(report);
        OpenBuilder();
        QueueModeChoice();
    }

    private bool _reportOpenRequested = false;
    private string _reportUri = string.Empty;

    public void ShowReport(string filePath)
    {
        LoadingGrid.Visibility = Visibility.Visible;
        ContentFrame.Visibility = Visibility.Collapsed;
        _reportOpenRequested = true;
        _reportUri = filePath;
    }

    public void ShowSession(BlockCheckResultSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        DispatcherQueue.TryEnqueue(() =>
        {
            _viewModel.Load(session);
            OpenBuilder();
            QueueModeChoice();
        });
    }

    private void OpenBuilder()
    {
        _modeChoiceShown = false;
        ContentFrame.BackStack.Clear();
        if (ContentFrame.Content is ResultPage page)
        {
            page.LoadViewModel(_viewModel);
            UpdateChrome();
            return;
        }
        ContentFrame.Navigate(typeof(ResultPage), _viewModel);
    }

    private void QueueModeChoice()
    {
        DispatcherQueue.TryEnqueue(async () => await ShowModeChoiceAsync());
    }

    private void ContentFrame_Loaded(object sender, RoutedEventArgs e)
    {
        if (_reportOpenRequested)
        {
            _ = Load(); // Load catches and logs its own errors.
        }

        if (!_modeChoiceShown && _viewModel.HasReport)
        {
            QueueModeChoice();
        }
    }

    private async Task Load()
    {
        string filePath = _reportUri;
        _reportOpenRequested = false;
        try
        {
            // Only the import runs on the pool; the error is shown back on the UI thread,
            // NotificationCenter.Show from the pool thread threw a COMException.
            BlockCheckReportImportResult imported = await Task.Run(() => _importService.LoadAsync(filePath));
            ShowSession(imported.Session);
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(BlockCheck2ResultWindow), exception.ToString());
            NotificationCenter.Show(InfoBarSeverity.Error, Text("BlockCheck2ReportOpenFailedTitle"), exception.Message);
        }
        finally
        {
            LoadingGrid.Visibility = Visibility.Collapsed;
            ContentFrame.Visibility = Visibility.Visible;
        }
    }

    private async System.Threading.Tasks.Task ShowModeChoiceAsync()
    {
        if (_modeChoiceShown || ContentFrame.XamlRoot == null)
        {
            return;
        }
        _modeChoiceShown = true;
        ContentDialog dialog = new()
        {
            XamlRoot = ContentFrame.XamlRoot,
            Title = Text("BlockCheck2ResultModeDialogTitle"),
            Content = Text("BlockCheck2ResultModeDialogMessage"),
            PrimaryButtonText = Text("BlockCheck2ResultModeAutomaticButton"),
            SecondaryButtonText = Text("BlockCheck2ResultModeManualButton"),
            CloseButtonText = Text("BlockCheck2CancelDialogButtonText"),
            DefaultButton = _viewModel.CanUseConfig
                ? ContentDialogButton.Primary
                : ContentDialogButton.Secondary,
            IsPrimaryButtonEnabled = _viewModel.CanUseConfig,
        };
        ContentDialogResult result = await dialog.ShowQueuedAsync();
        if (result == ContentDialogResult.Primary)
        {
            OpenPresetReview();
        }
    }

    private void OpenPresetReview()
    {
        if (!_viewModel.CanUseConfig || ContentFrame.Content is PresetReviewPage)
        {
            return;
        }
        ContentFrame.Navigate(typeof(PresetReviewPage), _viewModel);
    }

    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        if (_notificationSource != null)
        {
            _notificationSource.StatusNotificationRequested -= StatusNotificationSource_Requested;
        }
        _notificationSource = e.Content as IStatusNotificationSource;
        if (_notificationSource != null)
        {
            _notificationSource.StatusNotificationRequested += StatusNotificationSource_Requested;
        }
        UpdateChrome();
    }

    private void StatusNotificationSource_Requested(
        object? sender,
        StatusNotificationRequestedEventArgs e) =>
        NotificationCenter.Show(e.Severity, e.Title, e.Message);

    private void UpdateChrome()
    {
        _statusBarItems.Clear();
        bool review = ContentFrame.Content is PresetReviewPage;
        SaveToApplicationMenuItem.Visibility = review ? Visibility.Visible : Visibility.Collapsed;
        SaveToApplicationMenuItem.IsEnabled = review && _viewModel.Draft.CanUseConfig;
        BackToBuilderMenuItem.Visibility = review ? Visibility.Visible : Visibility.Collapsed;

        _statusBarItems.Clear();
        if (review)
        {
            _statusBarItems.Add(Text("BlockCheck2ReviewStatusFormat"));
            _statusBarItems.Add(string.Format(Text("BlockCheck2ReviewStatusFormatGroups"), _viewModel.Draft.Groups.Count));
            _statusBarItems.Add(string.Format(Text("BlockCheck2ReviewStatusFormatFiles"), _viewModel.Draft.Files.Count));
        }
        else
        {
            _statusBarItems.Add(Text("BlockCheck2BuilderStatusFormat"));
            _statusBarItems.Add(RunPresetName());
            _statusBarItems.Add(string.Format(Text("BlockCheck2BuilderStatusTargetCount"), _viewModel?.TargetCount.ToString() ?? "%~"));
            _statusBarItems.Add(string.Format(Text("BlockCheck2BuilderStatusFormatStrategies"), _viewModel.StrategyEvidenceCount));
            if (!string.IsNullOrEmpty(_viewModel?.CreatedAtLocalText))
                _statusBarItems.Add(string.Format(Text("BlockCheck2BuilderStatusTime"), _viewModel.CreatedAtLocalText));
            _statusBarItems.Add(string.Format(Text("BlockCheck2BuilderStatusFormatSelected"), _viewModel.ManualAssignmentCount));

        }
    }

    private string RunPresetName() => _viewModel.Report?.RunPreset switch
    {
        BlockCheckRunPreset.Quick => Text("BlockCheck2QuickModeName"),
        BlockCheckRunPreset.Exhaustive => Text("BlockCheck2ExhaustiveModeName"),
        _ => Text("BlockCheck2BalancedModeName"),
    };

    private async void SaveJsonReportItem_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Report == null)
        {
            return;
        }
        Microsoft.Win32.SaveFileDialog dialog = CreateSaveDialog(
            "blockcheck2-report.json",
            Text("BlockCheck2JsonFileFilter"));
        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _reportSerializer.SaveJsonAsync(dialog.FileName, _viewModel.Report);
                ShowNotification(
                    InfoBarSeverity.Success,
                    Text("BlockCheck2ActionCompletedTitle"),
                    Text("BlockCheck2FileSavedMessage"));
            }
            catch (Exception exception)
            {
                ShowNotification(
                    InfoBarSeverity.Error,
                    Text("BlockCheck2ActionFailedTitle"),
                    exception.Message);
            }
        }
    }

    private async void SaveTextReportItem_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Report == null)
        {
            return;
        }
        Microsoft.Win32.SaveFileDialog dialog = CreateSaveDialog(
            "blockcheck2-report.txt",
            Text("BlockCheck2TextFileFilter"));
        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _reportSerializer.SaveTextAsync(dialog.FileName, _viewModel.Report);
                ShowNotification(
                    InfoBarSeverity.Success,
                    Text("BlockCheck2ActionCompletedTitle"),
                    Text("BlockCheck2FileSavedMessage"));
            }
            catch (Exception exception)
            {
                ShowNotification(
                    InfoBarSeverity.Error,
                    Text("BlockCheck2ActionFailedTitle"),
                    exception.Message);
            }
        }
    }

    private Microsoft.Win32.SaveFileDialog CreateSaveDialog(string fileName, string filter) => new()
    {
        Title = Text("BlockCheck2SaveReportDialogTitle"),
        FileName = fileName,
        Filter = filter,
        OverwritePrompt = true,
        RestoreDirectory = true,
    };

    private async void SaveToApplicationItem_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.CanUseConfig || ContentFrame.Content is not PresetReviewPage page)
        {
            return;
        }
        SaveToApplicationMenuItem.IsEnabled = false;
        try
        {
            await page.SaveToApplicationAsync();
        }
        finally
        {
            UpdateChrome();
        }
    }

    private async void BackToBuilderItem_Click(object sender, RoutedEventArgs e)
    {
        if (ContentFrame.Content is PresetReviewPage page)
        {
            await page.NavigateBackToBuilderAsync();
        }
    }

    private void CloseItem_Click(object sender, RoutedEventArgs e) => Close();

    private void ShowNotification(InfoBarSeverity severity, string title, string message) =>
        NotificationCenter.Show(severity, title, message);

    private string Text(string key) => _localizer.GetLocalizedString(key);
}
