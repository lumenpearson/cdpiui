using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.Commands;
using CDPIUI.Controls.Dialogs.Store;
using CDPIUI.Controls.Navigation;
using CDPIUI.Controls.Store;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Default;
using CDPIUI.Extensions;
using CDPIUI.Helper.Migration;
using CDPIUI.ViewModels.Welcome;
using CommunityToolkit.Labs.WinUI.MarkdownTextBlock;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinUI3Localizer;

namespace CDPIUI;

public sealed partial class WelcomeWindow : TemplateWindow, IWelcomeView
{
    public const string MigrationWindowId = "GoodbyeDpiMigrationWelcome";

    private readonly ILocalizer _localizer = Localizer.Get();

    private MarkdownConfig _config;

    public MarkdownConfig MarkdownConfig
    {
        get => _config;
        set => _config = value;
    }

    public WelcomeViewModel ViewModel { get; }

    public WelcomeWindow()
    {
        ViewModel = new WelcomeViewModel(this, DispatcherQueue);
        InitializeComponent();

        WindowTitle = _localizer.GetLocalizedString("WelcomeWindowTitle");
        IconUri = @"Assets/favicon.ico";
        this.CustomTitleBarUserControl = TitleBarUserControl;

        DisableResizeFeature();

        _config = new MarkdownConfig();
        Closed += WelcomeWindow_Closed;
        _localizer.LanguageChanged += Localizer_LanguageChanged;
    }

    public WelcomeStep CurrentStep
    {
        get
        {
            AnimatedHorizontalContentItem item = AnimatedHorizontalContentViewer.SelectedItem;
            if (item == null)
                return WelcomeStep.None;
            if (item == WelcomeItem)
                return WelcomeStep.Welcome;
            if (item == LicenseItem)
                return WelcomeStep.License;
            if (item == PersonalizationItem)
                return WelcomeStep.Personalization;
            if (item == ProxyItem)
                return WelcomeStep.Proxy;
            if (item == StoreItem)
                return WelcomeStep.Store;
            if (item == CompleteItem)
                return WelcomeStep.Complete;
            return WelcomeStep.None;
        }
    }

    internal void SetMigrationSession(GoodbyeDpiMigrationSession session)
    {
        if (!ViewModel.SetMigrationSession(session))
            return;
        UpdateMigrationHeader();
        CheckNavigation();
    }

    void IWelcomeView.RefreshNavigation() => CheckNavigation();

    void IWelcomeView.ShowStoreLoadingButtons() =>
        UtilityButtonControls.SetButtonVisibilities(
            (NextButton, Visibility.Collapsed),
            (SkipButton, Visibility.Visible));

    async Task<bool> IWelcomeView.AcceptLicensesAsync(List<ItemLicenseModel> licenses)
    {
        AcceptLicenseContentDialog dialog = new()
        {
            Licenses = licenses,
            XamlRoot = ContentRoot.XamlRoot
        };
        return await dialog.ShowQueuedAsync() == ContentDialogResult.Primary;
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentStep == WelcomeStep.License && !ViewModel.TryAcceptLicenseStep())
            return;

        if (sender is Button btn &&
            btn == NextButton &&
            CurrentStep == WelcomeStep.Store &&
            StoreViewBundles.SelectedItems.Count > 0)
            _ = ViewModel.DownloadKitsAsync(StoreViewBundles.SelectedItems.Cast<StoreViewBundleItem>().ToList()); // DownloadKitsAsync catches and logs its own errors.

        AnimatedHorizontalContentViewer.GoNext();
        CheckNavigation();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        AnimatedHorizontalContentViewer.GoPrevious();
        CheckNavigation();
    }

    private void CheckNavigation()
    {
        UtilityButtonControls.HelpUrl = string.Empty;
        ViewModel.IsNextEnabled = true;
        ViewModel.IsLoading = false;
        WelcomeStep step = CurrentStep;

        bool isComplete = step == WelcomeStep.Complete;
        UtilityButtonControls.SetButtonVisibilities(
            (BackButton, step == WelcomeStep.Welcome || isComplete
                ? Visibility.Collapsed
                : Visibility.Visible),
            (SkipButton, step == WelcomeStep.Store && !ViewModel.IsMigrationMode ? Visibility.Visible : Visibility.Collapsed),
            (NextButton, isComplete ? Visibility.Collapsed : Visibility.Visible),
            (CompleteButton, isComplete ? Visibility.Visible : Visibility.Collapsed));

        if (step == WelcomeStep.License)
        {
            ViewModel.EnterLicenseStep();
        }
        else if (step == WelcomeStep.Proxy)
        {
            _ = ViewModel.CheckStoreStatusAsync(); // CheckStoreStatusAsync catches and logs its own errors.
        }
        else if (step == WelcomeStep.Store)
        {
            if (!ViewModel.IsMigrationMode)
            {
                UtilityButtonControls.LoadingStateText = _localizer.GetLocalizedString("UpdatingStoreDatabase");
                _ = ViewModel.LoadStoreAsync(); // LoadStoreAsync catches and logs its own errors.
            }
            else
            {
                UtilityButtonControls.SetButtonVisibilities(
                    (SkipButton, Visibility.Collapsed),
                    (NextButton, Visibility.Visible));
                ViewModel.EnterMigrationStep();
            }
        }
    }

    private void UpdateMigrationHeader()
    {
        if (!ViewModel.IsMigrationMode)
            return;
        StoreItem.Header = _localizer.GetLocalizedString("/WelcomeWizard/MigrationAnimatedHorizontalContentItemHeader");
        StoreItem.Description = _localizer.GetLocalizedString("/WelcomeWizard/MigrationAnimatedHorizontalContentItemDescription");
    }

    private void Localizer_LanguageChanged(object? sender, LanguageChangedEventArgs e)
    {
        UpdateMigrationHeader();
        ViewModel.OnLanguageChanged();
    }

    private void WelcomeWindow_Closed(object sender, WindowEventArgs args)
    {
        _localizer.LanguageChanged -= Localizer_LanguageChanged;
        Closed -= WelcomeWindow_Closed;
        ViewModel.OnWindowClosed();
    }

    private Brush StoreStatusBrush(bool isStoreAvailable) =>
        isStoreAvailable
            ? (SolidColorBrush)Application.Current.Resources["SystemFillColorSuccessBrush"]
            : (SolidColorBrush)Application.Current.Resources["SystemFillColorCautionBrush"];

    private void StoreViewBundles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UtilityButtonControls.SetButtonVisibilities(
            (NextButton, StoreViewBundles.SelectedItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed),
            (SkipButton, Visibility.Visible));
    }

    private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        _ = ViewModel.CheckStoreStatusAsync(); // CheckStoreStatusAsync catches and logs its own errors.
    }

    private void UtilityButtonControls_Loaded(object sender, RoutedEventArgs e)
    {
        CheckNavigation();
    }

    private async void CompleteButton_Click(object sender, RoutedEventArgs e)
    {
        bool showFeatures = ViewModel.ShowAppFeatures == true;
        var app = (App)Application.Current;
        // Release ownership and the migration gate before activating the main window.
        await app.MakeWindowNormal(this);
        Program.CompleteMigrationActivation();
        var mainWindow = await app.SafeCreateNewWindow<ModernMainWindow>();
        this.Close();
        App.ActivateWindow(mainWindow);
        if (showFeatures)
        {
            await CommandsHandler.HandleCommandAsync("cdpiui://AppFeatures");
        }
    }
}
