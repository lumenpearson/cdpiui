using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Helper.Items;
using CDPIUI.Helper.Migration;
using Microsoft.UI.Xaml.Controls;

namespace CDPIUI.ViewModels.Welcome;

public sealed partial class WelcomeViewModel
{
    private GoodbyeDpiMigrationSession? _migrationSession;
    private CancellationTokenSource _migrationCancellation = new();
    private List<RepoItemModel> _migrationComponents = [];
    private readonly Dictionary<string, double> _migrationComponentProgress = new(StringComparer.OrdinalIgnoreCase);
    private bool _migrationFlowStarted;
    private bool _migrationImportInProgress;
    private bool _migrationReadyToContinue;

    private bool _isMigrationMode;
    private string _migrationStatusTitle = string.Empty;
    private string _migrationStatusMessage = string.Empty;
    private InfoBarSeverity _migrationStatusSeverity = InfoBarSeverity.Informational;
    private double _migrationProgress;
    private IReadOnlyList<MigrationComponentDisplayItem>? _migrationComponentItems;
    private bool _isMigrationComponentsListVisible = true;
    private string _migrationSummaryText = string.Empty;
    private bool _isRetryMigrationVisible;

    /// <summary>True when the wizard runs the GoodbyeDPI migration instead of the ready kits.</summary>
    public bool IsMigrationMode
    {
        get => _isMigrationMode;
        private set
        {
            if (SetProperty(ref _isMigrationMode, value))
                OnPropertyChanged(nameof(IsReadyBundlesContentVisible));
        }
    }

    public bool IsReadyBundlesContentVisible => !_isMigrationMode;

    public bool IsMigrationReadyToContinue => _migrationReadyToContinue;

    public string MigrationStatusTitle
    {
        get => _migrationStatusTitle;
        private set => SetProperty(ref _migrationStatusTitle, value);
    }

    public string MigrationStatusMessage
    {
        get => _migrationStatusMessage;
        private set => SetProperty(ref _migrationStatusMessage, value);
    }

    public InfoBarSeverity MigrationStatusSeverity
    {
        get => _migrationStatusSeverity;
        private set => SetProperty(ref _migrationStatusSeverity, value);
    }

    /// <summary>Overall migration progress from 0 to 100.</summary>
    public double MigrationProgress
    {
        get => _migrationProgress;
        private set => SetProperty(ref _migrationProgress, value);
    }

    public IReadOnlyList<MigrationComponentDisplayItem>? MigrationComponentItems
    {
        get => _migrationComponentItems;
        private set => SetProperty(ref _migrationComponentItems, value);
    }

    public bool IsMigrationComponentsListVisible
    {
        get => _isMigrationComponentsListVisible;
        private set => SetProperty(ref _isMigrationComponentsListVisible, value);
    }

    public string MigrationSummaryText
    {
        get => _migrationSummaryText;
        private set => SetProperty(ref _migrationSummaryText, value);
    }

    public bool IsRetryMigrationVisible
    {
        get => _isRetryMigrationVisible;
        private set => SetProperty(ref _isRetryMigrationVisible, value);
    }

    /// <summary>
    /// Attaches the migration session. Returns false when the session was already attached;
    /// its state is then shown again.
    /// </summary>
    internal bool SetMigrationSession(GoodbyeDpiMigrationSession session)
    {
        if (ReferenceEquals(_migrationSession, session))
        {
            ApplyMigrationSessionState();
            return false;
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

        IsMigrationMode = true;
        UpdateMigrationComponentList();
        ApplyMigrationSessionState();
        return true;
    }

    /// <summary>Called when the user opens the Store step in migration mode.</summary>
    public void EnterMigrationStep()
    {
        IsNextEnabled = _migrationReadyToContinue;
        _ = TryStartMigrationFlowAsync(); // TryStartMigrationFlowAsync catches and logs its own errors.
    }

    public void OnLanguageChanged()
    {
        if (_migrationSession != null)
            UpdateMigrationComponentList();
        ApplyMigrationSessionState();
    }

    /// <summary>Stops the migration work and marks an unfinished session failed.</summary>
    public void OnWindowClosed()
    {
        DisconnectMigrationQueueHandlers();
        _migrationCancellation.Cancel();
        _migrationCancellation.Dispose();
        if (_migrationSession != null)
        {
            _migrationSession.Changed -= MigrationSession_Changed;
            if (!_migrationSession.IsTerminal)
                _ = _migrationSession.UpdateAsync(
                    MigrationSessionState.Failed,
                    MigrationProgress,
                    "The migration wizard was closed before completion.",
                    "MIGRATION_WINDOW_CLOSED");
        }
    }

    private string Localized(string key) =>
        _localizer.GetLocalizedString($"/WelcomeWizard/{key}");

    private void MigrationSession_Changed(object? sender, EventArgs e)
    {
        if (_dispatcherQueue.HasThreadAccess)
            ApplyMigrationSessionState();
        else
            _dispatcherQueue.TryEnqueue(ApplyMigrationSessionState);
    }

    private void ApplyMigrationSessionState()
    {
        if (_migrationSession == null)
            return;
        MigrationProgress = Math.Max(MigrationProgress, _migrationSession.Progress);
        switch (_migrationSession.State)
        {
            case MigrationSessionState.Accepted:
            case MigrationSessionState.Preparing:
                ShowMigrationStatus("MigrationPreparingTitle", "MigrationPreparingMessage", InfoBarSeverity.Informational);
                break;
            case MigrationSessionState.ReadyForComponents:
                ShowMigrationStatus("MigrationComponentsTitle", "MigrationComponentsReadyMessage", InfoBarSeverity.Informational);
                if (_view.CurrentStep == WelcomeStep.Store)
                    _ = TryStartMigrationFlowAsync(); // TryStartMigrationFlowAsync catches and logs its own errors.
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
                IsRetryMigrationVisible = false;
                MigrationProgress = 100;
                break;
            case MigrationSessionState.Failed:
                ShowMigrationError(
                    _migrationSession.Message ?? Localized("MigrationGenericError"),
                    _migrationSession.ErrorCode ?? "MIGRATION_FAILED");
                break;
        }
        if (_view.CurrentStep == WelcomeStep.Store)
            IsNextEnabled = _migrationReadyToContinue;
    }

    private async Task TryStartMigrationFlowAsync()
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
                MigrationSummaryText = Localized("MigrationAlreadyCompleted");
                await _migrationSession.UpdateAsync(MigrationSessionState.Completed, 100);
                return;
            }

            _migrationFlowStarted = true;
            IsRetryMigrationVisible = false;
            IsMigrationComponentsListVisible = true;
            await _migrationSession.UpdateAsync(MigrationSessionState.LoadingComponents, 22);
            if (!await LoadMigrationComponentsAsync(package))
                return;

            UpdateMigrationComponentList();
            List<RepoItemModel> itemsToInstall = _migrationComponents
                .Where(item => !DatabaseHelper.Instance.IsItemInstalled(item.store_id!))
                .ToList();
            if (itemsToInstall.Count == 0)
            {
                await BeginMigrationImportAsync();
                return;
            }

            if (!await AcceptMigrationLicensesAsync(itemsToInstall))
                return;

            await QueueMigrationComponentsAsync(itemsToInstall);
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    /// <summary>
    /// Loads the Store database and finds the Store items the package needs.
    /// Returns false after the migration was marked failed.
    /// </summary>
    private async Task<bool> LoadMigrationComponentsAsync(VerifiedMigrationPackage package)
    {
        bool storeLoaded = await StoreHelper.Instance.LoadAllStoreDatabase();
        if (!storeLoaded)
        {
            await FailMigrationAsync(
                Localized("MigrationStoreLoadFailed"),
                "MIGRATION_STORE_LOAD_FAILED");
            return false;
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
            return false;
        }
        if (_migrationComponents.Any(component => !IsItemSupported(component)))
        {
            await FailMigrationAsync(
                Localized("MigrationComponentUnsupported"),
                "MIGRATION_COMPONENT_UNSUPPORTED");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Asks the user to accept the licenses of the items. Returns false when the user declined.
    /// </summary>
    private async Task<bool> AcceptMigrationLicensesAsync(List<RepoItemModel> itemsToInstall)
    {
        List<ItemLicenseModel> licenses = StoreItemLicenses.CollectDistinct(itemsToInstall);
        if (licenses.Count == 0)
            return true;

        await _migrationSession!.UpdateAsync(MigrationSessionState.WaitingForLicense, 25);
        if (!await _view.AcceptLicensesAsync(licenses))
        {
            _migrationFlowStarted = false;
            IsRetryMigrationVisible = true;
            MigrationSummaryText = Localized("MigrationComponentLicenseDeclined");
            return false;
        }
        return true;
    }

    private async Task QueueMigrationComponentsAsync(List<RepoItemModel> itemsToInstall)
    {
        ConnectMigrationQueueHandlers();
        foreach (RepoItemModel item in itemsToInstall)
        {
            _migrationComponentProgress[item.store_id!] = 0;
            StoreHelper.Instance.RemoveItemFromQueue(item.store_id!);
            StoreHelper.Instance.AddItemToQueue(item.store_id!, string.Empty);
        }
        await _migrationSession!.UpdateAsync(MigrationSessionState.DownloadingComponents, 25);
        UpdateMigrationComponentList();
        UpdateMigrationComponentProgress();
    }

    private static bool IsItemSupported(RepoItemModel item)
    {
        Logger.Instance.CreateDebugLog(LogSource, $"Checking is item supported for {item.store_id} ({item.short_name})");

        return VersionHelper.IsVersionInRange(ApplicationInfo.Version, item.target_minversion, item.target_maxversion);
    }

    private async Task FailMigrationAsync(string message, string errorCode)
    {
        _migrationFlowStarted = false;
        _migrationReadyToContinue = false;
        DisconnectMigrationQueueHandlers();
        ShowMigrationError(message, errorCode);
        if (_migrationSession != null)
            await _migrationSession.UpdateAsync(MigrationSessionState.Failed,
                MigrationProgress, message, errorCode);
    }

    private async Task RetryMigrationAsync()
    {
        if (_migrationSession == null || _migrationImportInProgress)
            return;
        IsRetryMigrationVisible = false;
        MigrationSummaryText = string.Empty;
        _migrationFlowStarted = false;
        if (_migrationSession.Package == null)
            await _migrationSession.RetryPreparationAsync();
        if (_migrationSession.Package != null)
            await TryStartMigrationFlowAsync();
    }

    private void ShowMigrationStatus(
        string titleKey,
        string messageKey,
        InfoBarSeverity severity)
    {
        MigrationStatusTitle = Localized(titleKey);
        MigrationStatusMessage = Localized(messageKey);
        MigrationStatusSeverity = severity;
    }

    private void ShowMigrationError(string message, string errorCode)
    {
        // The migration session reports some failures with an English message; the code selects the localized text.
        string localizedMessage = _localizer.GetLocalizedString($"/WelcomeWizard/MigrationError_{errorCode}");
        if (!string.IsNullOrWhiteSpace(localizedMessage))
            message = localizedMessage;
        MigrationStatusTitle = Localized("MigrationFailedTitle");
        MigrationStatusMessage = $"{message}\n\n{errorCode}";
        MigrationStatusSeverity = InfoBarSeverity.Error;
        IsRetryMigrationVisible = true;
        IsNextEnabled = false;
    }
}
