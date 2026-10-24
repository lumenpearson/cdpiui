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

public sealed class MigrationComponentDisplayItem
{
    public string StoreId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}

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

    private async Task DownloadItems()
    {
        try
        {
            foreach (StoreViewBundleItem kit in StoreViewBundles.SelectedItems.Cast<StoreViewBundleItem>())
            {
                var _kit = StoreHelper.Instance.GetReadyKitFromStoreId(kit.KitId);
                if (_kit == null)
                    continue;

                var _items = (_kit.items ?? [])
                    .Select(StoreHelper.Instance.GetItemInfoFromStoreId)
                    .OfType<RepoItemModel>()
                    .ToList();

                List<RepoItemModel> itemsToInstall = _items
                    .Where(item => !DatabaseHelper.Instance.IsItemInstalled(item.store_id))
                    .ToList();
                if (itemsToInstall.Count == 0)
                    continue;

                List<ItemLicenseModel> licenses = itemsToInstall
                    .SelectMany(item => item.license ?? [])
                    .GroupBy(license => $"{license.name}\n{license.url}")
                    .Select(group => group.First())
                    .ToList();

                if (licenses.Count > 0)
                {
                    AcceptLicenseContentDialog dialog = new()
                    {
                        Licenses = licenses,
                        XamlRoot = ContentRoot.XamlRoot
                    };
                    if (await dialog.ShowQueuedAsync() != ContentDialogResult.Primary)
                        return;
                }

                HashSet<string> pendingIds = itemsToInstall
                    .Select(item => item.store_id)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (string itemId in _kit.items ?? [])
                {
                    if (pendingIds.Contains(itemId))
                        StoreHelper.Instance.AddItemToQueue(itemId, string.Empty);
                }
            }

        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(nameof(WelcomeWindow), exception.ToString());
        }
    }

    private async Task TryLoadStore()
    {
        try
        {
            ErrorStackPanel.Visibility = Visibility.Collapsed;
            StoreViewBundles.Visibility = Visibility.Collapsed;

            UtilityButtonControls.SetButtonVisibilities(
                (NextButton, Visibility.Collapsed),
                (SkipButton, Visibility.Visible));
            UtilityButtonControls.IsLoading = true;
            UtilityButtonControls.LoadingStateText = _localizer.GetLocalizedString("UpdatingStoreDatabase");

            bool result = await StoreHelper.Instance.LoadAllStoreDatabase();

            if (result)
                result = LoadReadyKits();

            ErrorStackPanel.Visibility = result ? Visibility.Collapsed : Visibility.Visible;
            StoreViewBundles.Visibility = result ? Visibility.Visible : Visibility.Collapsed;

            if (AnimatedHorizontalContentViewer.SelectedItem == StoreItem)
            {
                UtilityButtonControls.IsLoading = false;
                UtilityButtonControls.SetButtonVisibilities(
                    (NextButton, Visibility.Collapsed),
                    (SkipButton, Visibility.Visible));
            }
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(nameof(WelcomeWindow), exception.ToString());
        }
    }

    private bool LoadReadyKits()
    {
        Kits.Clear();
        List<ReadyKitModel> kits = StoreHelper.Instance.ReadyKits
            .OrderByDescending(kit => kit.IsRecommended)
            .ToList();

        if (kits.Count == 0)
        {
            Kits.Clear();
            return false;
        }
        foreach (var kit in kits.Select(ReadyKitViewModelFactory.Create))
            Kits.Add(kit);

        return true;
    }

    private void StoreViewBundles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UtilityButtonControls.SetButtonVisibilities(
            (NextButton, StoreViewBundles.SelectedItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed),
            (SkipButton, Visibility.Visible));
    }

    private void MigrationSession_Changed(object? sender, EventArgs e)
    {
        if (DispatcherQueue.HasThreadAccess)
            ApplyMigrationSessionState();
        else
            DispatcherQueue.TryEnqueue(ApplyMigrationSessionState);
    }

    private void ApplyMigrationSessionState()
    {
        if (_migrationSession == null)
            return;
        MigrationProgressBar.Value = Math.Max(MigrationProgressBar.Value, _migrationSession.Progress);
        switch (_migrationSession.State)
        {
            case MigrationSessionState.Accepted:
            case MigrationSessionState.Preparing:
                ShowMigrationStatus("MigrationPreparingTitle", "MigrationPreparingMessage", InfoBarSeverity.Informational);
                break;
            case MigrationSessionState.ReadyForComponents:
                ShowMigrationStatus("MigrationComponentsTitle", "MigrationComponentsReadyMessage", InfoBarSeverity.Informational);
                if (AnimatedHorizontalContentViewer.SelectedItem == StoreItem)
                    _ = TryStartMigrationFlow(); // TryStartMigrationFlow catches and logs its own errors.
                break;
            case MigrationSessionState.WaitingForLicense:
                ShowMigrationStatus("MigrationComponentLicenseTitle", "MigrationComponentLicenseMessage", InfoBarSeverity.Warning);
                break;
            case MigrationSessionState.LoadingComponents:
                ShowMigrationStatus("MigrationComponentsTitle", "MigrationLoadingStoreMessage", InfoBarSeverity.Informational);
                break;
            case MigrationSessionState.DownloadingComponents:
                ShowMigrationStatus("MigrationComponentsTitle", "MigrationDownloadingMessage", InfoBarSeverity.Informational);
                break;
            case MigrationSessionState.ReadyToImport:
            case MigrationSessionState.Importing:
                ShowMigrationStatus("MigrationImportTitle", "MigrationImportMessage", InfoBarSeverity.Informational);
                break;
            case MigrationSessionState.Completed:
                _migrationReadyToContinue = true;
                bool completedWithWarnings = string.Equals(
                    _migrationSession.ErrorCode,
                    "MIGRATION_IMPORT_INCOMPLETE",
                    StringComparison.Ordinal);
                ShowMigrationStatus(
                    completedWithWarnings ? "MigrationCompletedWithWarningsTitle" : "MigrationCompletedTitle",
                    completedWithWarnings ? "MigrationImportIncompleteMessage" : "MigrationCompletedMessage",
                    completedWithWarnings ? InfoBarSeverity.Warning : InfoBarSeverity.Success);
                RetryMigrationButton.Visibility = Visibility.Collapsed;
                MigrationProgressBar.Value = 100;
                break;
            case MigrationSessionState.Failed:
                ShowMigrationError(
                    _migrationSession.Message ?? Localized("MigrationGenericError"),
                    _migrationSession.ErrorCode ?? "MIGRATION_FAILED");
                break;
        }
        if (AnimatedHorizontalContentViewer.SelectedItem == StoreItem)
            NextButton.IsEnabled = _migrationReadyToContinue;
    }

    private async Task TryStartMigrationFlow()
    {
        try
        {
            if (_migrationSession == null || _migrationReadyToContinue || _migrationFlowStarted)
                return;
            if (_migrationSession.Package == null)
            {
                if (_migrationSession.State == MigrationSessionState.Failed)
                    ShowMigrationError(
                        _migrationSession.Message ?? Localized("MigrationGenericError"),
                        _migrationSession.ErrorCode ?? "MIGRATION_PREPARATION_FAILED");
                else
                    ShowMigrationStatus("MigrationPreparingTitle", "MigrationPreparingMessage", InfoBarSeverity.Informational);
                return;
            }

            VerifiedMigrationPackage package = _migrationSession.Package;
            if (MigrationImportService.IsAlreadyCompleted(package))
            {
                MigrationSummaryTextBlock.Text = Localized("MigrationAlreadyCompleted");
                await _migrationSession.UpdateAsync(MigrationSessionState.Completed, 100);
                return;
            }

            _migrationFlowStarted = true;
            RetryMigrationButton.Visibility = Visibility.Collapsed;
            MigrationComponentsListView.Visibility = Visibility.Visible;
            await _migrationSession.UpdateAsync(MigrationSessionState.LoadingComponents, 22);
            bool storeLoaded = await StoreHelper.Instance.LoadAllStoreDatabase();
            if (!storeLoaded)
            {
                await FailMigrationAsync(
                    Localized("MigrationStoreLoadFailed"),
                    "MIGRATION_STORE_LOAD_FAILED");
                return;
            }

            _migrationComponents = package.Components
                .Select(component => StoreHelper.Instance.GetItemInfoFromStoreId(component.InstallItemId))
                .OfType<RepoItemModel>()
                .GroupBy(component => component.store_id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            if (_migrationComponents.Count != package.Components.Count)
            {
                await FailMigrationAsync(
                    Localized("MigrationComponentMissingFromStore"),
                    "MIGRATION_COMPONENT_NOT_FOUND");
                return;
            }
            if (_migrationComponents.Any(component => !IsItemSupported(component)))
            {
                await FailMigrationAsync(
                    Localized("MigrationComponentUnsupported"),
                    "MIGRATION_COMPONENT_UNSUPPORTED");
                return;
            }

            UpdateMigrationComponentList();
            List<RepoItemModel> itemsToInstall = _migrationComponents
                .Where(item => !DatabaseHelper.Instance.IsItemInstalled(item.store_id!))
                .ToList();
            if (itemsToInstall.Count == 0)
            {
                await BeginMigrationImportAsync();
                return;
            }

            List<ItemLicenseModel> licenses = itemsToInstall
                .SelectMany(item => item.license ?? [])
                .GroupBy(license => $"{license.name}\n{license.url}")
                .Select(group => group.First())
                .ToList();
            if (licenses.Count > 0)
            {
                await _migrationSession.UpdateAsync(MigrationSessionState.WaitingForLicense, 25);
                AcceptLicenseContentDialog dialog = new()
                {
                    Licenses = licenses,
                    XamlRoot = ContentRoot.XamlRoot
                };
                if (await dialog.ShowQueuedAsync() != ContentDialogResult.Primary)
                {
                    _migrationFlowStarted = false;
                    RetryMigrationButton.Visibility = Visibility.Visible;
                    MigrationSummaryTextBlock.Text = Localized("MigrationComponentLicenseDeclined");
                    return;
                }
            }

            ConnectMigrationQueueHandlers();
            foreach (RepoItemModel item in itemsToInstall)
            {
                _migrationComponentProgress[item.store_id!] = 0;
                StoreHelper.Instance.RemoveItemFromQueue(item.store_id!);
                StoreHelper.Instance.AddItemToQueue(item.store_id!, string.Empty);
            }
            await _migrationSession.UpdateAsync(MigrationSessionState.DownloadingComponents, 25);
            UpdateMigrationComponentList();
            UpdateMigrationComponentProgress();
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(nameof(WelcomeWindow), exception.ToString());
        }
    }

    private bool IsItemSupported(RepoItemModel item)
    {
        Logger.Instance.CreateDebugLog(nameof(WelcomeWindow), $"Checking is item supported for {item.store_id} ({item.short_name})");

        return VersionHelper.IsVersionInRange(ApplicationInfo.Version, item.target_minversion, item.target_maxversion);
    }


    private void ConnectMigrationQueueHandlers()
    {
        if (_migrationHandlersConnected)
            return;
        _migrationStageHandler = data =>
        {
            string? itemId = StoreHelper.Instance.GetItemIdFromOperationId(data.Item1);
            if (!ContainsMigrationComponent(itemId))
                return;
            RunMigrationOnUiThread(() =>
            {
                UpdateMigrationComponentList();
                UpdateMigrationComponentProgress();
            });
        };
        _migrationProgressHandler = data =>
        {
            string? itemId = StoreHelper.Instance.GetItemIdFromOperationId(data.Item1);
            if (!ContainsMigrationComponent(itemId))
                return;
            RunMigrationOnUiThread(() =>
            {
                _migrationComponentProgress[itemId!] = data.Item2;
                UpdateMigrationComponentProgress();
            });
        };
        _migrationErrorHandler = data =>
        {
            string? itemId = StoreHelper.Instance.GetItemIdFromOperationId(data.Item1);
            if (!ContainsMigrationComponent(itemId))
                return;
            RunMigrationOnUiThread(() => _ = FailMigrationAsync(
                Localized("MigrationComponentInstallFailed"),
                string.IsNullOrWhiteSpace(data.Item2.ErrorCode)
                    ? "MIGRATION_COMPONENT_INSTALL_FAILED"
                    : data.Item2.ErrorCode));
        };
        _migrationStoppedHandler = itemId =>
        {
            if (!ContainsMigrationComponent(itemId))
                return;
            RunMigrationOnUiThread(() => _ = HandleMigrationQueueStoppedAsync(itemId));
        };
        StoreHelper.Instance.ItemDownloadStageChanged += _migrationStageHandler;
        StoreHelper.Instance.ItemDownloadProgressChanged += _migrationProgressHandler;
        StoreHelper.Instance.ItemInstallingErrorHappens += _migrationErrorHandler;
        StoreHelper.Instance.ItemActionsStopped += _migrationStoppedHandler;
        _migrationHandlersConnected = true;
    }

    private void DisconnectMigrationQueueHandlers()
    {
        if (!_migrationHandlersConnected)
            return;
        StoreHelper.Instance.ItemDownloadStageChanged -= _migrationStageHandler;
        StoreHelper.Instance.ItemDownloadProgressChanged -= _migrationProgressHandler;
        StoreHelper.Instance.ItemInstallingErrorHappens -= _migrationErrorHandler;
        StoreHelper.Instance.ItemActionsStopped -= _migrationStoppedHandler;
        _migrationHandlersConnected = false;
    }

    private async Task HandleMigrationQueueStoppedAsync(string itemId)
    {
        _migrationComponentProgress[itemId] = DatabaseHelper.Instance.IsItemInstalled(itemId) ? 100 : 0;
        UpdateMigrationComponentList();
        UpdateMigrationComponentProgress();
        if (_migrationComponents.Count > 0 && _migrationComponents.All(
                item => DatabaseHelper.Instance.IsItemInstalled(item.store_id!)))
        {
            DisconnectMigrationQueueHandlers();
            await BeginMigrationImportAsync();
            return;
        }

        bool hasQueuedItems = _migrationComponents.Any(item =>
            !string.IsNullOrWhiteSpace(StoreHelper.Instance.GetOperationIdFromItemId(item.store_id!)));
        if (!hasQueuedItems)
        {
            await FailMigrationAsync(
                Localized("MigrationComponentInstallStopped"),
                "MIGRATION_COMPONENT_INSTALL_STOPPED");
        }
    }

    private async Task BeginMigrationImportAsync()
    {
        if (_migrationSession?.Package == null || _migrationImportInProgress || _migrationReadyToContinue)
            return;
        _migrationImportInProgress = true;
        DisconnectMigrationQueueHandlers();
        RetryMigrationButton.Visibility = Visibility.Collapsed;
        MigrationComponentsListView.Visibility = _migrationComponents.Count == 0
            ? Visibility.Collapsed
            : Visibility.Visible;
        await _migrationSession.UpdateAsync(MigrationSessionState.ReadyToImport, 65);
        await _migrationSession.UpdateAsync(MigrationSessionState.Importing, 67);

        try
        {
            Progress<double> importProgress = new(value =>
            {
                double overall = 67 + Math.Clamp(value, 0, 100) * 0.31;
                MigrationProgressBar.Value = overall;
                _ = _migrationSession.UpdateAsync(MigrationSessionState.Importing, overall);
            });
            // Read before Task.Run: closing the window or a new session replaces the source.
            CancellationToken migrationToken = _migrationCancellation.Token;
            MigrationImportResult result = await Task.Run(() =>
                new MigrationImportService().Import(
                    _migrationSession.Package,
                    importProgress,
                    migrationToken));

            try
            {
                ComponentItemsLoaderHelper.Instance.Init();
                foreach (MigrationComponentRequirement component in _migrationSession.Package.Components)
                    ComponentItemsLoaderHelper.Instance
                        .GetComponentHelperFromId(component.InstallItemId)?
                        .ReInitConfigs();
            }
            catch (Exception exception)
            {
                Logger.Instance.CreateWarningLog(nameof(WelcomeWindow), exception.ToString());
                result.Issues.Add(new MigrationImportIssue(
                    "configuration-cache",
                    "CDPIUI",
                    exception.Message));
            }

            string summary = string.Format(
                Localized("MigrationImportSummary"),
                result.ImportedPresetCount,
                result.ImportedResourceCount,
                result.ImportedSettingCount,
                result.ReviewRequiredCount);
            if (result.Issues.Count > 0)
            {
                summary += "\n\n" + Localized("MigrationImportIncompleteMessage") + " " +
                    string.Format(Localized("MigrationImportWarningsSummary"), result.Issues.Count);
            }
            MigrationSummaryTextBlock.Text = summary;
            _migrationReadyToContinue = true;
            _migrationImportInProgress = false;
            await _migrationSession.UpdateAsync(
                MigrationSessionState.Completed,
                100,
                result.Issues.Count > 0 ? Localized("MigrationImportIncompleteMessage") : null,
                result.Issues.Count > 0 ? "MIGRATION_IMPORT_INCOMPLETE" : null);
            CheckNavigation();
        }
        catch (OperationCanceledException)
        {
            _migrationImportInProgress = false;
            await FailMigrationAsync(Localized("MigrationCanceled"), "MIGRATION_CANCELED");
        }
        catch (Exception exception)
        {
            _migrationImportInProgress = false;
            Logger.Instance.CreateWarningLog(nameof(WelcomeWindow), exception.ToString());
            MigrationSummaryTextBlock.Text = Localized("MigrationImportIncompleteMessage");
            _migrationReadyToContinue = true;
            await _migrationSession.UpdateAsync(
                MigrationSessionState.Completed,
                100,
                Localized("MigrationImportIncompleteMessage"),
                "MIGRATION_IMPORT_INCOMPLETE");
            CheckNavigation();
        }
    }

    private void UpdateMigrationComponentProgress()
    {
        if (_migrationComponents.Count == 0)
        {
            MigrationProgressBar.Value = 65;
            return;
        }
        double total = 0;
        foreach (RepoItemModel item in _migrationComponents)
        {
            if (DatabaseHelper.Instance.IsItemInstalled(item.store_id!))
            {
                total += 100;
                continue;
            }
            string? operationId = StoreHelper.Instance.GetOperationIdFromItemId(item.store_id!);
            double value = operationId == null
                ? _migrationComponentProgress.GetValueOrDefault(item.store_id!, 0)
                : StoreHelper.Instance.GetQueueItemFromOperationId(operationId)?.DownloadProgress ?? 0;
            total += Math.Clamp(value, 0, 100);
        }
        double aggregate = total / _migrationComponents.Count;
        double overall = 25 + aggregate * 0.4;
        MigrationProgressBar.Value = overall;
        if (_migrationSession != null)
            _ = _migrationSession.UpdateAsync(MigrationSessionState.DownloadingComponents, overall);
    }

    private void UpdateMigrationComponentList()
    {
        List<MigrationComponentDisplayItem> items = [];
        foreach (RepoItemModel component in _migrationComponents)
        {
            string id = component.store_id!;
            string status;
            if (DatabaseHelper.Instance.IsItemInstalled(id))
            {
                status = Localized("MigrationComponentInstalled");
            }
            else
            {
                string? operationId = StoreHelper.Instance.GetOperationIdFromItemId(id);
                QueueItemModel? queueItem = string.IsNullOrWhiteSpace(operationId)
                    ? null
                    : StoreHelper.Instance.GetQueueItemFromOperationId(operationId);
                status = queueItem?.DownloadStage switch
                {
                    "Downloading" => Localized("MigrationComponentDownloading"),
                    "Extracting" => Localized("MigrationComponentInstalling"),
                    "Completed" or "END" => Localized("MigrationComponentFinishing"),
                    "ErrorHappens" => Localized("MigrationComponentError"),
                    _ => Localized("MigrationComponentWaiting")
                };
            }
            string displayName = component.short_name ??
                StoreHelper.Instance.GetLocalizedStoreItemName(
                    component.name,
                    StoreLocalizationHelper.GetStoreLikeLocale());
            items.Add(new MigrationComponentDisplayItem
            {
                StoreId = id,
                DisplayName = displayName,
                Status = status
            });
        }
        MigrationComponentsListView.ItemsSource = null;
        MigrationComponentsListView.ItemsSource = items;
        MigrationSummaryTextBlock.Text = items.Count == 0
            ? Localized("MigrationNoComponentsRequired")
            : string.Format(Localized("MigrationComponentsCount"), items.Count);
    }

    private async Task FailMigrationAsync(string message, string errorCode)
    {
        _migrationFlowStarted = false;
        _migrationReadyToContinue = false;
        DisconnectMigrationQueueHandlers();
        ShowMigrationError(message, errorCode);
        if (_migrationSession != null)
            await _migrationSession.UpdateAsync(MigrationSessionState.Failed,
                MigrationProgressBar.Value, message, errorCode);
    }

    private async void RetryMigrationButton_Click(object sender, RoutedEventArgs e)
    {
        if (_migrationSession == null || _migrationImportInProgress)
            return;
        RetryMigrationButton.Visibility = Visibility.Collapsed;
        MigrationSummaryTextBlock.Text = string.Empty;
        _migrationFlowStarted = false;
        if (_migrationSession.Package == null)
            await _migrationSession.RetryPreparationAsync();
        if (_migrationSession.Package != null)
            await TryStartMigrationFlow();
    }

    private void ShowMigrationStatus(
        string titleKey,
        string messageKey,
        InfoBarSeverity severity)
    {
        MigrationStatusInfoBar.Title = Localized(titleKey);
        MigrationStatusInfoBar.Message = Localized(messageKey);
        MigrationStatusInfoBar.Severity = severity;
        MigrationStatusInfoBar.IsOpen = true;
    }

    private void ShowMigrationError(string message, string errorCode)
    {
        // The migration session reports some failures with an English message; the code selects the localized text.
        string localizedMessage = _localizer.GetLocalizedString($"/WelcomeWizard/MigrationError_{errorCode}");
        if (!string.IsNullOrWhiteSpace(localizedMessage))
            message = localizedMessage;
        MigrationStatusInfoBar.Title = Localized("MigrationFailedTitle");
        MigrationStatusInfoBar.Message = $"{message}\n\n{errorCode}";
        MigrationStatusInfoBar.Severity = InfoBarSeverity.Error;
        MigrationStatusInfoBar.IsOpen = true;
        RetryMigrationButton.Visibility = Visibility.Visible;
        NextButton.IsEnabled = false;
    }

    private bool ContainsMigrationComponent(string? itemId) =>
        !string.IsNullOrWhiteSpace(itemId) && _migrationComponents.Any(item =>
            string.Equals(item.store_id, itemId, StringComparison.OrdinalIgnoreCase));

    private void RunMigrationOnUiThread(Action action)
    {
        if (DispatcherQueue.HasThreadAccess)
            action();
        else
            DispatcherQueue.TryEnqueue(() => action());
    }

    private string Localized(string key) =>
        _localizer.GetLocalizedString($"/WelcomeWizard/{key}");

    private void UpdateMigrationLocalizedText()
    {
        if (_migrationSession == null)
            return;
        StoreItem.Header = Localized("MigrationAnimatedHorizontalContentItemHeader");
        StoreItem.Description = Localized("MigrationAnimatedHorizontalContentItemDescription");
        UpdateMigrationComponentList();
    }

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

    private void CheckStoreStatusButton_Click(object sender, RoutedEventArgs e)
    {
        _ = CheckStoreStatus(); // CheckStoreStatus catches and logs its own errors.
    }

    private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        _ = CheckStoreStatus(); // CheckStoreStatus catches and logs its own errors.
    }

    private async Task CheckStoreStatus()
    {
        try
        {
            CheckStoreStatusButton.IsEnabled = false;
            UtilityButtonControls.IsLoading = true;
            await Task.Delay(500);
            bool result = await StoreHelper.Instance.IsServerAvailable();
            UtilityButtonControls.IsLoading = false;
            CheckStoreStatusButton.IsEnabled = true;
            StoreStatusTextBlock.Text = result ? _localizer.GetLocalizedString("Available") : _localizer.GetLocalizedString("NotAvailable");
            StoreStatusTextBlock.Foreground = result
                ? (SolidColorBrush)Application.Current.Resources["SystemFillColorSuccessBrush"]
                : (SolidColorBrush)Application.Current.Resources["SystemFillColorCautionBrush"];
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(nameof(WelcomeWindow), exception.ToString());
        }
    }
}
