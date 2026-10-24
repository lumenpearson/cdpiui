using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.Commands;
using CDPIUI.Controls.Dialogs.Store;
using CDPIUI.Controls.Store;
using CDPIUI.Controls.Universal;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.Data;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.Queue;
using CDPIUI.Core.Store.Repository.Localization;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Core.System;
using CDPIUI.Default;
using CDPIUI.Extensions;
using CDPIUI.Helper;
using CDPIUI.Helper.Migration;
using CDPIUI.Shared;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.Shared.PrettyErrorConversionService;
using CDPIUI.ViewModels;
using CommunityToolkit.Labs.WinUI.MarkdownTextBlock;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;


namespace CDPIUI;

public sealed partial class WelcomeWindow : TemplateWindow
{
    public const string MigrationWindowId = "GoodbyeDpiMigrationWelcome";

    private ILocalizer _localizer = Localizer.Get();
    private GoodbyeDpiMigrationSession? _migrationSession;
    private CancellationTokenSource _migrationCancellation = new();
    private List<RepoItemModel> _migrationComponents = [];
    private readonly Dictionary<string, double> _migrationComponentProgress = new(StringComparer.OrdinalIgnoreCase);
    private bool _migrationFlowStarted;
    private bool _migrationImportInProgress;
    private bool _migrationReadyToContinue;
    private bool _migrationHandlersConnected;
    private Action<Tuple<string, string>>? _migrationStageHandler;
    private Action<Tuple<string, double>>? _migrationProgressHandler;
    private Action<Tuple<string, ErrorModel>>? _migrationErrorHandler;
    private Action<string>? _migrationStoppedHandler;

    private MarkdownConfig _config;

    public MarkdownConfig MarkdownConfig
    {
        get => _config;
        set => _config = value;
    }

    public ObservableCollection<StoreViewBundleItem> Kits = [];

    public WelcomeWindow()
    {
        InitializeComponent();

        WindowTitle = _localizer.GetLocalizedString("WelcomeWindowTitle");
        IconUri = @"Assets/favicon.ico";
        this.CustomTitleBarUserControl = TitleBarUserControl;

        DisableResizeFeature();

        _config = new MarkdownConfig();
        Closed += WelcomeWindow_Closed;
        _localizer.LanguageChanged += Localizer_LanguageChanged;

        StoreViewBundles.ItemsSource = Kits;
    }

    internal void SetMigrationSession(GoodbyeDpiMigrationSession session)
    {
        if (ReferenceEquals(_migrationSession, session))
        {
            ApplyMigrationSessionState();
            return;
        }

        if (_migrationSession != null)
            _migrationSession.Changed -= MigrationSession_Changed;
        DisconnectMigrationQueueHandlers();
        _migrationCancellation.Cancel();
        _migrationCancellation.Dispose();
        _migrationCancellation = new CancellationTokenSource();

        _migrationSession = session;
        _migrationSession.Changed += MigrationSession_Changed;
        _migrationFlowStarted = false;
        _migrationImportInProgress = false;
        _migrationReadyToContinue = false;
        _migrationComponents = [];
        _migrationComponentProgress.Clear();

        ReadyBundlesContent.Visibility = Visibility.Collapsed;
        MigrationContent.Visibility = Visibility.Visible;
        UpdateMigrationLocalizedText();
        ApplyMigrationSessionState();
        CheckNavigation();
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (AnimatedHorizontalContentViewer.SelectedItem == LicenseItem)
        {
            if (LicenseAgreeCheckBox.IsChecked != true)
                return;
            SettingsManager.Instance.SetValue("WELCOMEWIZARD", "Shown", true);
        }

        if (sender is Button btn &&
            btn == NextButton &&
            AnimatedHorizontalContentViewer.SelectedItem == StoreItem &&
            StoreViewBundles.SelectedItems.Count > 0)
            _ = DownloadItems(); // DownloadItems catches and logs its own errors.

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
        NextButton.IsEnabled = true;
        UtilityButtonControls.IsLoading = false;
        var sel = AnimatedHorizontalContentViewer.SelectedItem;

        bool isComplete = sel == CompleteItem;
        UtilityButtonControls.SetButtonVisibilities(
            (BackButton, sel == WelcomeItem || isComplete
                ? Visibility.Collapsed
                : Visibility.Visible),
            (SkipButton, sel == StoreItem && _migrationSession == null ? Visibility.Visible : Visibility.Collapsed),
            (NextButton, isComplete ? Visibility.Collapsed : Visibility.Visible),
            (CompleteButton, isComplete ? Visibility.Visible : Visibility.Collapsed));

        if (sel == LicenseItem)
        {
            TryLoadLicense();
            NextButton.IsEnabled = LicenseAgreeCheckBox.IsChecked ?? false;
        }
        /*
        else if (sel == AdItem)
        {
            UtilityButtonControls.HelpUrl = "/Other/Ad";
        }
        */
        else if (sel == ProxyItem)
        {
            _ = CheckStoreStatus(); // CheckStoreStatus catches and logs its own errors.
        }
        else if (sel == StoreItem)
        {
            if (_migrationSession == null)
            {
                _ = TryLoadStore(); // TryLoadStore catches and logs its own errors.
            }
            else
            {
                UtilityButtonControls.SetButtonVisibilities(
                    (SkipButton, Visibility.Collapsed),
                    (NextButton, Visibility.Visible));
                NextButton.IsEnabled = _migrationReadyToContinue;
                _ = TryStartMigrationFlow(); // TryStartMigrationFlow catches and logs its own errors.
            }
        }
    }

    private string Localized(string key) =>
        _localizer.GetLocalizedString($"/WelcomeWizard/{key}");

    private void Localizer_LanguageChanged(object? sender, LanguageChangedEventArgs e)
    {
        UpdateMigrationLocalizedText();
        ApplyMigrationSessionState();
    }

    private void WelcomeWindow_Closed(object sender, WindowEventArgs args)
    {
        DisconnectMigrationQueueHandlers();
        _migrationCancellation.Cancel();
        _migrationCancellation.Dispose();
        _localizer.LanguageChanged -= Localizer_LanguageChanged;
        Closed -= WelcomeWindow_Closed;
        if (_migrationSession != null)
        {
            _migrationSession.Changed -= MigrationSession_Changed;
            if (!_migrationSession.IsTerminal)
                _ = _migrationSession.UpdateAsync(
                    MigrationSessionState.Failed,
                    MigrationProgressBar.Value,
                    "The migration wizard was closed before completion.",
                    "MIGRATION_WINDOW_CLOSED");
        }
    }

    private void TryLoadLicense()
    {
        string path = Path.Combine(Directories.ELUADirectory, StoreLocalizationHelper.GetStoreLikeLocale(), "ELUA.md");
        try
        {
            LicenseTextBlock.Text = ShellHelper.LoadAllTextFromFile(path);
            LicenseAgreeCheckBox.IsEnabled = true;
        }
        catch
        {
            LicenseAgreeCheckBox.IsEnabled = false;
            LicenseTextBlock.Text = string.Format(_localizer.GetLocalizedString("/WelcomeWizard/UnableLoadLicense"), path);
        }
    }

    private void LicenseAgreeCheckBox_Click(object sender, RoutedEventArgs e)
    {
        NextButton.IsEnabled = LicenseAgreeCheckBox.IsChecked ?? false;
        if (LicenseAgreeCheckBox.IsChecked == true)
            SettingsManager.Instance.SetValue("WELCOMEWIZARD", "Shown", true);
    }

    private void UtilityButtonControls_Loaded(object sender, RoutedEventArgs e)
    {
        CheckNavigation();
    }

    private async void CompleteButton_Click(object sender, RoutedEventArgs e)
    {
        bool showFeatures = ShowAppFeaturesCheckBox.IsChecked == true;
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
