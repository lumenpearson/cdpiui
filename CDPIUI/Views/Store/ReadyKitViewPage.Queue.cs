using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using CDPIUI.Commands;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Dialogs.Store;
using CDPIUI.Core;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.Queue;
using CDPIUI.Core.Store.Repository.Localization;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Helper;
using CDPIUI.Helper.LScript;
using CDPIUI.Helper.Parsers;
using CDPIUI.Shared.PrettyErrorConversionService;
using CommunityToolkit.Labs.WinUI.MarkdownTextBlock;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using WinUI3Localizer;

namespace CDPIUI.Views.Store;

public sealed partial class ReadyKitViewPage
{
    private void RefreshActionState()
    {
        if (!_isPageActive || _kit == null)
            return;

        UpdateContentItemStatuses();

        if (_hasInstallationError)
        {
            ErrorStatusGrid.Visibility = Visibility.Visible;
            InstallButton.Visibility = Visibility.Collapsed;
            DownloadStatusGrid.Visibility = Visibility.Collapsed;
            return;
        }

        bool isFullyInstalled = _items.Count > 0 &&
            _items.All(item => DatabaseHelper.Instance.IsItemInstalled(item.store_id));
        if (isFullyInstalled)
        {
            _activeItemId = string.Empty;
            InstallButtonTextBlock.Text = _localizer.GetLocalizedString("Installed");
            InstallButton.IsEnabled = false;
            InstallButton.Visibility = Visibility.Visible;
            DownloadStatusGrid.Visibility = Visibility.Collapsed;
            ErrorStatusGrid.Visibility = Visibility.Collapsed;
            StatusProgressBar.Value = 100;
            return;
        }

        RepoItemModel activeItem = null;
        QueueItemModel activeQueueItem = null;
        string currentOperationId = StoreHelper.Instance.GetCurrentQueueOperationId();
        bool hasPendingItems = false;

        foreach (RepoItemModel item in _items)
        {
            string operationId = StoreHelper.Instance.GetOperationIdFromItemId(item.store_id);
            if (string.IsNullOrEmpty(operationId))
                continue;

            hasPendingItems = true;
            QueueItemModel queueItem = StoreHelper.Instance.GetQueueItemFromOperationId(operationId);
            if (activeItem == null || string.Equals(operationId, currentOperationId, StringComparison.OrdinalIgnoreCase))
            {
                activeItem = item;
                activeQueueItem = queueItem;
            }

            if (string.Equals(operationId, currentOperationId, StringComparison.OrdinalIgnoreCase))
                break;
        }

        if (hasPendingItems)
        {
            _activeItemId = activeItem?.store_id ?? string.Empty;
            ShowDownloadState();
            ApplyDownloadStage(activeQueueItem?.DownloadStage, activeItem);
            UpdateAggregateProgress();
            return;
        }

        _activeItemId = string.Empty;
        DownloadStatusGrid.Visibility = Visibility.Collapsed;
        ErrorStatusGrid.Visibility = Visibility.Collapsed;
        InstallButton.Visibility = Visibility.Visible;
        InstallButtonTextBlock.Text = _localizer.GetLocalizedString("Get");
        InstallButton.IsEnabled = _canInstall;

        if (!_hasInstallationError)
            StatusProgressBar.Value = CalculateAggregateProgress();
    }

    private void ApplyDownloadStage(string stage, RepoItemModel activeItem)
    {
        CurrentStatusTextBlock.Text = GetStageText(stage);
        CurrentStatusSpeedTextBlock.Visibility = string.Equals(stage, "Downloading", StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (activeItem == null)
        {
            CurrentStatusTipTextBlock.Text = string.Empty;
            return;
        }

        int itemNumber = _items.FindIndex(item =>
            string.Equals(item.store_id, activeItem.store_id, StringComparison.OrdinalIgnoreCase)) + 1;
        CurrentStatusTipTextBlock.Text = string.Format(
            _localizer.GetLocalizedString("ReadyKitItemProgress"),
            itemNumber,
            _items.Count,
            GetItemDisplayName(activeItem));
    }

    private string GetStageText(string stage) => stage switch
    {
        "GETR" => _localizer.GetLocalizedString("GettingReady"),
        "END" => _localizer.GetLocalizedString("Finishing"),
        "Downloading" => _localizer.GetLocalizedString("Downloading"),
        "Extracting" => _localizer.GetLocalizedString("Installing"),
        "Completed" => _localizer.GetLocalizedString("Finishing"),
        "CANC" => _localizer.GetLocalizedString("Cancel"),
        "ConnectingToService" => _localizer.GetLocalizedString("ConnectingToService"),
        "ErrorHappens" => _localizer.GetLocalizedString("ErrorHappens"),
        _ => _localizer.GetLocalizedString("QueueWaiting")
    };

    private void UpdateAggregateProgress() =>
        StatusProgressBar.Value = CalculateAggregateProgress();

    private double CalculateAggregateProgress()
    {
        if (_items.Count == 0)
            return 0;

        double totalProgress = 0;
        foreach (RepoItemModel item in _items)
        {
            if (DatabaseHelper.Instance.IsItemInstalled(item.store_id))
            {
                totalProgress += 100;
                continue;
            }

            double progress = 0;
            string operationId = StoreHelper.Instance.GetOperationIdFromItemId(item.store_id);
            if (!string.IsNullOrEmpty(operationId))
            {
                QueueItemModel queueItem = StoreHelper.Instance.GetQueueItemFromOperationId(operationId);
                progress = queueItem?.DownloadProgress ?? 0;
            }

            if (_itemProgress.TryGetValue(item.store_id, out double observedProgress))
                progress = Math.Max(progress, observedProgress);

            totalProgress += Math.Clamp(progress, 0, 100);
        }

        return totalProgress / _items.Count;
    }

    private void UpdateContentItemStatuses()
    {
        foreach (ReadyKitContentItemViewModel contentItem in ContentItems)
        {
            if (DatabaseHelper.Instance.IsItemInstalled(contentItem.StoreId))
            {
                contentItem.Status = _localizer.GetLocalizedString("Installed");
                continue;
            }

            string operationId = StoreHelper.Instance.GetOperationIdFromItemId(contentItem.StoreId);
            if (string.IsNullOrEmpty(operationId))
            {
                contentItem.Status = _localizer.GetLocalizedString("Get");
                continue;
            }

            QueueItemModel queueItem = StoreHelper.Instance.GetQueueItemFromOperationId(operationId);
            contentItem.Status = GetStageText(queueItem?.DownloadStage);
        }
    }

    private void ConnectQueueHandlers()
    {
        if (_handlersConnected)
            return;

        ConnectDownloadProgressHandlers();
        ConnectInstallResultHandlers();

        _handlersConnected = true;
    }

    private void ConnectDownloadProgressHandlers()
    {
        _itemDownloadStageChangedHandler = data =>
        {
            string itemId = StoreHelper.Instance.GetItemIdFromOperationId(data.Item1);
            if (!ContainsItem(itemId))
                return;

            RunOnUIThread(() =>
            {
                _activeItemId = itemId;
                CurrentStatusSpeedTextBlock.Text = string.Empty;
                RefreshActionState();
            });
        };
        StoreHelper.Instance.ItemDownloadStageChanged += _itemDownloadStageChangedHandler;

        _itemDownloadProgressChangedHandler = data =>
        {
            string itemId = StoreHelper.Instance.GetItemIdFromOperationId(data.Item1);
            if (!ContainsItem(itemId))
                return;

            RunOnUIThread(() =>
            {
                _activeItemId = itemId;
                _itemProgress[itemId] = data.Item2;
                if (DownloadStatusGrid.Visibility != Visibility.Visible)
                    RefreshActionState();
                else
                    UpdateAggregateProgress();
            });
        };
        StoreHelper.Instance.ItemDownloadProgressChanged += _itemDownloadProgressChangedHandler;

        _itemDownloadSpeedChangedHandler = data =>
        {
            string itemId = StoreHelper.Instance.GetItemIdFromOperationId(data.Item1);
            if (!ContainsItem(itemId))
                return;

            RunOnUIThread(() =>
            {
                if (!string.Equals(_activeItemId, itemId, StringComparison.OrdinalIgnoreCase))
                    return;

                CurrentStatusSpeedTextBlock.Text = $"{UnitsParser.FormatSpeed(data.Item2)},";
                CurrentStatusSpeedTextBlock.Visibility = Visibility.Visible;
            });
        };
        StoreHelper.Instance.ItemDownloadSpeedChanged += _itemDownloadSpeedChangedHandler;
    }

    private void ConnectInstallResultHandlers()
    {
        _itemInstallingErrorHappensHandler = data =>
        {
            string itemId = StoreHelper.Instance.GetItemIdFromOperationId(data.Item1);
            if (!ContainsItem(itemId))
                return;

            RunOnUIThread(() =>
            {
                _hasInstallationError = true;
                ErrorNameTextBlock.Text = data.Item2.ErrorCode;
                ErrorStatusGrid.Visibility = Visibility.Visible;
                InstallButton.Visibility = Visibility.Collapsed;
                DownloadStatusGrid.Visibility = Visibility.Collapsed;
            });
        };
        StoreHelper.Instance.ItemInstallingErrorHappens += _itemInstallingErrorHappensHandler;

        _itemActionsStoppedHandler = itemId =>
        {
            if (!ContainsItem(itemId))
                return;

            DispatcherQueue.TryEnqueue(() =>
            {
                if (!_isPageActive)
                    return;

                _itemProgress[itemId] = DatabaseHelper.Instance.IsItemInstalled(itemId) ? 100 : 0;
                RefreshActionState();
            });
        };
        StoreHelper.Instance.ItemActionsStopped += _itemActionsStoppedHandler;
    }

    private void DisconnectQueueHandlers()
    {
        if (!_handlersConnected)
            return;

        StoreHelper.Instance.ItemDownloadStageChanged -= _itemDownloadStageChangedHandler;
        StoreHelper.Instance.ItemDownloadProgressChanged -= _itemDownloadProgressChangedHandler;
        StoreHelper.Instance.ItemDownloadSpeedChanged -= _itemDownloadSpeedChangedHandler;
        StoreHelper.Instance.ItemInstallingErrorHappens -= _itemInstallingErrorHappensHandler;
        StoreHelper.Instance.ItemActionsStopped -= _itemActionsStoppedHandler;
        _handlersConnected = false;
    }

    private void ShowDownloadState()
    {
        StopActionButton.IsEnabled = true;
        ErrorStatusGrid.Visibility = Visibility.Collapsed;
        InstallButton.Visibility = Visibility.Collapsed;
        DownloadStatusGrid.Visibility = Visibility.Visible;
    }

    private bool ContainsItem(string itemId) =>
        !string.IsNullOrEmpty(itemId) && _items.Any(item =>
            string.Equals(item.store_id, itemId, StringComparison.OrdinalIgnoreCase));

    private void RunOnUIThread(Action action)
    {
        if (!_isPageActive)
            return;

        if (DispatcherQueue.HasThreadAccess)
            action();
        else
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_isPageActive)
                    action();
            });
    }
}
