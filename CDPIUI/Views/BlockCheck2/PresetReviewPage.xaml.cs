using System;
using System.ComponentModel;
using System.Threading.Tasks;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Controls.Dialogs.CreateConfigHelper;
using CDPIUI.Controls.Universal;
using CDPIUI.ViewModels;
using CDPIUI.ViewModels.BlockCheck2;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using WinUI3Localizer;

namespace CDPIUI.Views.BlockCheck2;

public sealed partial class PresetReviewPage : CDPIUI.Controls.Default.TemplatePage, IStatusNotificationSource, INotifyPropertyChanged, IBlockCheck2PresetReviewPageView
{
    private readonly ILocalizer _localizer = Localizer.Get();

    public PresetReviewPage()
    {
        ViewModel = new BlockCheck2PresetReviewViewModel(this);
        InitializeComponent();
        DataContext = this;
        Editor.UseInlineStatusMessages = false;
        Editor.StatusNotificationRequested += Editor_StatusNotificationRequested;
        Editor.TestStateChanged += Editor_MenuStateChanged;
        Editor.EditorReadOnlyChanged += Editor_MenuStateChanged;
        Editor.PanelStateChanged += Editor_MenuStateChanged;
    }

    public BlockCheck2PresetReviewViewModel ViewModel { get; }
    public ConfigMakerUserControl Editor => PresetEditor.Editor;
    public event EventHandler<StatusNotificationRequestedEventArgs>? StatusNotificationRequested;
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool CanFormatEditor => !Editor.IsEditorReadOnly;
    public bool IsEditorCommandPanelVisible => Editor.IsCommandPanelVisible;
    public bool IsEditorBottomPanelVisible => Editor.IsBottomPanelVisible;
    public bool IsEditorFilesPanelVisible => Editor.IsPresetFilesPanelVisible;
    public bool HasEditorFiles => Editor.HasPresetFiles;
    public bool HasEditorGroups => Editor.HasPresetGroups;
    public bool CanStartEditorTest => !Editor.IsTesting;
    public bool CanStopEditorTest => Editor.IsTesting;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is BlockCheck2ResultViewModel viewModel)
        {
            Load(viewModel);
        }
    }

    public void Load(BlockCheck2ResultViewModel viewModel) => ViewModel.Load(viewModel);

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.OnNavigatedFrom();
    }

    public void LoadDraft(BlockCheck2PresetDraft draft) => PresetEditor.LoadDraft(draft);

    public void SetExpertEditing(bool enabled) => PresetEditor.SetExpertEditing(enabled);

    public async Task<bool> ConfirmExpertEditingAsync()
    {
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = Text("BlockCheck2EnableExpertEditingTitle"),
            Content = Text("BlockCheck2EnableExpertEditingMessage"),
            PrimaryButtonText = Text("BlockCheck2EnableExpertEditingButton"),
            CloseButtonText = Text("BlockCheck2CancelDialogButtonText"),
            DefaultButton = ContentDialogButton.Close,
        };
        return await dialog.ShowQueuedAsync() == ContentDialogResult.Primary;
    }

    public async Task<bool> ConfirmDiscardExpertChangesAsync()
    {
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = Text("BlockCheck2DiscardExpertChangesTitle"),
            Content = Text("BlockCheck2DiscardExpertChangesMessage"),
            PrimaryButtonText = Text("BlockCheck2DiscardExpertChangesButton"),
            CloseButtonText = Text("BlockCheck2CancelDialogButtonText"),
            DefaultButton = ContentDialogButton.Close,
        };
        return await dialog.ShowQueuedAsync() == ContentDialogResult.Primary;
    }

    private async void AllowEditingCheckBox_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.ApplyExpertEditingChoiceAsync();

    public async Task NavigateBackToBuilderAsync()
    {
        if (!await ViewModel.DiscardExpertChangesAsync())
        {
            return;
        }
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
            DispatcherQueue.TryEnqueue(() =>
            {
                if (Frame.Content is ResultPage resultPage)
                {
                    resultPage.OpenPresetBuilder();
                }
            });
        }
    }

    public Task SaveToApplicationAsync() => ViewModel.SaveToApplicationAsync();

    public async Task<string?> AskPresetNameAsync()
    {
        TextBox nameTextBox = new()
        {
            Header = Text("BlockCheck2PresetNameFieldHeader"),
            PlaceholderText = Text("BlockCheck2PresetNameFieldPlaceholder"),
            MaxLength = 120,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        TextBlock explanation = new()
        {
            Text = Text("BlockCheck2SaveToApplicationDialogMessage"),
            TextWrapping = TextWrapping.Wrap,
        };
        StackPanel content = new() { Spacing = 10 };
        content.Children.Add(explanation);
        content.Children.Add(nameTextBox);
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = Text("BlockCheck2SaveToApplicationDialogTitle"),
            Content = content,
            PrimaryButtonText = Text("BlockCheck2SaveToApplicationDialogButton"),
            CloseButtonText = Text("BlockCheck2CancelDialogButtonText"),
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false,
        };
        nameTextBox.TextChanged += (_, _) =>
            dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(nameTextBox.Text);
        if (await dialog.ShowQueuedAsync() != ContentDialogResult.Primary)
        {
            return null;
        }
        return nameTextBox.Text;
    }

    public async Task ShowSaveCompletedAsync()
    {
        try
        {
            CreateCompleteDialog dialog = new()
            {
                XamlRoot = this.XamlRoot
            };
            BlockCheck2ResultWindow window = ((App)Application.Current).GetCurrentWindowFromType<BlockCheck2ResultWindow>();
            if (await dialog.ShowQueuedAsync() == ContentDialogResult.Primary)
                window?.Close();
            else
            {
                if (Frame.CanGoBack)
                    Frame.GoBack();
                else
                    Frame.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
            }

        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(PresetReviewPage), exception.ToString());
        }
    }

    public void ShowStatus(BlockCheck2NoticeSeverity severity, string title, string message) =>
        StatusNotificationRequested?.Invoke(
            this,
            new StatusNotificationRequestedEventArgs(ResultPage.ToInfoBarSeverity(severity), title, message));

    private void Editor_StatusNotificationRequested(
        object? sender,
        StatusNotificationRequestedEventArgs e) =>
        StatusNotificationRequested?.Invoke(this, e);

    private async void BlockCheck2SaveToApplicationButton_Click(object sender, RoutedEventArgs e)
    {
        await SaveToApplicationAsync();
    }

    private async void SaveEditorTextMenuItem_Click(object sender, RoutedEventArgs e) =>
        await Editor.SaveTextAsync();

    private void FormatEditorMenuItem_Click(object sender, RoutedEventArgs e) =>
        Editor.FormatCommand();

    private void EditorFilesPanelMenuItem_Click(object sender, RoutedEventArgs e) =>
        Editor.SetPresetFilesPanelVisible(!Editor.IsPresetFilesPanelVisible);

    private void EditorCommandPanelMenuItem_Click(object sender, RoutedEventArgs e) =>
        Editor.SetCommandPanelVisible(!Editor.IsCommandPanelVisible);

    private void EditorBottomPanelMenuItem_Click(object sender, RoutedEventArgs e) =>
        Editor.SetBottomPanelVisible(!Editor.IsBottomPanelVisible);

    private void ShowEditorOutputMenuItem_Click(object sender, RoutedEventArgs e) =>
        Editor.ShowOutputTab();

    private void ShowEditorDiagnosticsMenuItem_Click(object sender, RoutedEventArgs e) =>
        Editor.ShowDiagnosticsTab();

    private void ShowEditorGroupsMenuItem_Click(object sender, RoutedEventArgs e) =>
        Editor.ShowPresetGroupsTab();

    private async void StartEditorTestMenuItem_Click(object sender, RoutedEventArgs e) =>
        await Editor.StartTestAsync();

    private async void StopEditorTestMenuItem_Click(object sender, RoutedEventArgs e) =>
        await Editor.StopTestAsync(showCompletionMessage: true);

    private async void RefreshEditorHelpMenuItem_Click(object sender, RoutedEventArgs e) =>
        await Editor.RefreshHelpAsync();

    private void Editor_MenuStateChanged(object? sender, EventArgs e) =>
        NotifyMenuStateChanged();

    public void NotifyMenuStateChanged() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));

    private string Text(string key) => _localizer.GetLocalizedString(key);
}
