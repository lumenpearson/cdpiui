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
using CDPIUI.Helper.Items;
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

public sealed partial class WelcomeWindow
{
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
            Logger.Instance.CreateWarningLog(nameof(WelcomeWindow), exception.ToString());
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
        await _migrationSession.UpdateAsync(MigrationSessionState.DownloadingComponents, 25);
        UpdateMigrationComponentList();
        UpdateMigrationComponentProgress();
    }

    private bool IsItemSupported(RepoItemModel item)
    {
        Logger.Instance.CreateDebugLog(nameof(WelcomeWindow), $"Checking is item supported for {item.store_id} ({item.short_name})");

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

    private void UpdateMigrationLocalizedText()
    {
        if (_migrationSession == null)
            return;
        StoreItem.Header = Localized("MigrationAnimatedHorizontalContentItemHeader");
        StoreItem.Description = Localized("MigrationAnimatedHorizontalContentItemDescription");
        UpdateMigrationComponentList();
    }
}
