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

public sealed partial class WelcomeWindow
{
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
}
