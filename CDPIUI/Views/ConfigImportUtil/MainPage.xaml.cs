using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.AddOns.ConfigImport;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Dialogs.Universal;
using CDPIUI.Controls.Navigation;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Helper.AddOns.ConfigImport;
using CDPIUI.Helper.AddOns.ConfigShare;
using CDPIUI.ViewModels;
using CDPIUI.ViewModels.ConfigImport;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using WinUI3Localizer;

namespace CDPIUI.Views.ConfigImportUtil;

public sealed partial class MainPage : TemplatePage, IConfigImportPageView
{
    private readonly ILocalizer _localizer = Localizer.Get();

    public ConfigImportPageViewModel ViewModel { get; }

    public MainPage()
    {
        ViewModel = new ConfigImportPageViewModel(this, DispatcherQueue);
        InitializeComponent();
    }

    public bool IsViewLoaded => IsLoaded;

    public static InfoBarSeverity ToInfoBarSeverity(ConfigImportCompletionState state) => state switch
    {
        ConfigImportCompletionState.Success => InfoBarSeverity.Success,
        ConfigImportCompletionState.Partial => InfoBarSeverity.Warning,
        ConfigImportCompletionState.Failed => InfoBarSeverity.Error,
        _ => InfoBarSeverity.Informational,
    };

    public void QueueDroppedFiles(string[] paths) => ViewModel.QueueDroppedFiles(paths);

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.OnNavigatedTo(Parameter.Get("componentId"));
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e) => await ViewModel.OnLoadedAsync();

    private async void MainPage_Unloaded(object sender, RoutedEventArgs e) => await ViewModel.OnUnloadedAsync();

    public void GoToStep(ConfigImportStep step)
    {
        AnimatedHorizontalContentItem item = step switch
        {
            ConfigImportStep.Selection => SelectionStep,
            ConfigImportStep.Progress => ProgressStep,
            ConfigImportStep.MissingFiles => MissingFilesStep,
            ConfigImportStep.Completed => CompletedStep,
            _ => WorkDoneStep,
        };
        MainContent.GoTo(item);
    }

    public string[] PickImportFiles()
    {
        var result = ConfigImportHelper.OpenFileSelectionDialog(true);
        return result.Success ? result.Result : null;
    }

    public string PickReplacementFile(string missingPath)
    {
        string extension = Path.GetExtension(missingPath);
        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            Title = _localizer.GetLocalizedString("ConfigImportChooseReplacementTextBlock.Text"),
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Filter = string.IsNullOrWhiteSpace(extension)
                ? $"{_localizer.GetLocalizedString("AllSupported")} (*.*)|*.*"
                : $"{extension.TrimStart('.').ToUpperInvariant()} (*{extension})|*{extension}|{_localizer.GetLocalizedString("AllSupported")} (*.*)|*.*",
            FilterIndex = 1,
            RestoreDirectory = true,
        };
        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.FileName : null;
    }

    public Task OfferComponentInstallAsync(ConfigItem config) => ConfigShareUI.OfferComponentInstallAsync(XamlRoot, config);

    public async Task ShowErrorAsync(string message) =>
        await new ErrorContentDialog().ShowErrorDialogAsync(
            _localizer.GetLocalizedString("ConfigShareError"), message, XamlRoot);

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        ((App)Application.Current).OpenWindows.OfType<ConfigImportUtilWindow>()
            .FirstOrDefault(window => window.Content?.XamlRoot == XamlRoot)?.Close();
    }

    private void UseSuggestedFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ConfigImportMissingFileViewModel item })
            ViewModel.UseSuggestedFile(item);
    }

    private void ChooseReplacementButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ConfigImportMissingFileViewModel item })
            ViewModel.ChooseReplacementFile(item);
    }

    private void UseEmptyFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ConfigImportMissingFileViewModel item })
            ViewModel.UseEmptyFile(item);
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ConfigImportPresetViewModel item })
            await ViewModel.SaveAsync(item);
    }

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ConfigImportPresetViewModel item })
            await ViewModel.ToggleTestAsync(item);
    }

    private void DetailsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not HyperlinkButton { Tag: ConfigImportPresetViewModel item } button)
            return;

        var detailsPanel = new StackPanel { Spacing = 10, Width = 350 };

        string titleText = item.HasDetails
            ? string.Format(_localizer.GetLocalizedString("ConfigImportErrorView"), item.FileName)
            : _localizer.GetLocalizedString("ConfigImportSuccess");

        detailsPanel.Children.Add(new TextBlock
        {
            Text = titleText,
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
            TextWrapping = TextWrapping.Wrap,
        });

        foreach (ConfigImportIssue issue in item.Result.Issues)
        {
            string location = issue.LineNumber.HasValue
                ? string.Format(_localizer.GetLocalizedString("ConfigImportIssueLine"), issue.LineNumber.Value)
                : string.Empty;
            detailsPanel.Children.Add(new TextBlock
            {
                Text = $"[{issue.Code}]{location} {ConfigImportIssueText.GetMessage(issue)}",
                TextWrapping = TextWrapping.Wrap,
            });
        }

        if (!string.IsNullOrWhiteSpace(item.OperationError))
        {
            detailsPanel.Children.Add(new TextBlock
            {
                Text = item.OperationError,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        var flyout = new Flyout
        {
            Placement = FlyoutPlacementMode.BottomEdgeAlignedRight,
            Content = new ScrollViewer
            {
                MaxHeight = 420,
                Content = detailsPanel,
            },
        };
        flyout.ShowAt(button);
    }
}
