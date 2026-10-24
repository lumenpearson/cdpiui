using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Dialogs.Store;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.Repository.Localization;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Core.System;
using CDPIUI.Helper.LScript;
using CDPIUI.Helper.Parsers;
using CDPIUI.Shared;
using CDPIUI.Shared.PrettyErrorConversionService;
using CDPIUI.Views.Main.Components;
using CommunityToolkit.Labs.WinUI.MarkdownTextBlock;
using CommunityToolkit.WinUI.Animations.Expressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinRT.CDPIUIGenericHelpers;
using WinUI3Localizer;

namespace CDPIUI.Views.Store;

public sealed partial class ItemViewPage
{
    private void GetReadyUI()
    {
        StopActionButton.IsEnabled = true;
        StatusProgressbar.IsIndeterminate = true;
        ErrorStatusGrid.Visibility = Visibility.Collapsed;
        ItemActionButton.Visibility = Visibility.Collapsed;
        DownloadStatusGrid.Visibility = Visibility.Visible;
    }

    private void CheckCurrentStatus()
    {
        string operationId = StoreHelper.Instance.GetOperationIdFromItemId(_storeId);
        if (!string.IsNullOrEmpty(operationId))
        {
            GetReadyUI();

            string status = StoreHelper.Instance.GetQueueItemFromOperationId(operationId)?.DownloadStage;
            PreferItemDownloadingStateActions(status ?? "QueueWaiting");
            StatusProgressbar.Value = StoreHelper.Instance.GetQueueItemFromOperationId(operationId)?.DownloadProgress ?? 0;
        }
    }

    private void PreferItemDownloadingStateActions(string state)
    {
        state ??= "QueueWaiting";
        string stageHeaderText = string.Empty;
        switch (state)
        {
            case "GETR":
                stageHeaderText = _localizer.GetLocalizedString("GettingReady");
                StatusProgressbar.IsIndeterminate = true;
                break;
            case "END":
                stageHeaderText = _localizer.GetLocalizedString("Finishing");
                StatusProgressbar.IsIndeterminate = true;
                break;
            case "Downloading":
                stageHeaderText = _localizer.GetLocalizedString("Downloading");
                StatusProgressbar.IsIndeterminate = false;
                CurrentStatusSpeedTextBlock.Visibility = Visibility.Visible;
                break;
            case "Extracting":
                stageHeaderText = _localizer.GetLocalizedString("Installing");
                StatusProgressbar.IsIndeterminate = true;
                break;
            case "ErrorHappens":
                stageHeaderText = _localizer.GetLocalizedString("ErrorHappens");
                break;
            case "Completed":
                stageHeaderText = _localizer.GetLocalizedString("Finishing");
                StatusProgressbar.IsIndeterminate = true;
                break;
            case "CANC":
                stageHeaderText = _localizer.GetLocalizedString("Cancel");
                StatusProgressbar.IsIndeterminate = true;
                break;
            case "ConnectingToService":
                stageHeaderText = _localizer.GetLocalizedString("ConnectingToService");
                CurrentStatusTipTextBlock.Text = _localizer.GetLocalizedString("ConnectingToServiceTip");
                StatusProgressbar.IsIndeterminate = true;
                break;
            default:
                stageHeaderText = _localizer.GetLocalizedString(state);
                StatusProgressbar.IsIndeterminate = true;
                break;
        }
        CurrentStatusTextBlock.Text = stageHeaderText;
    }

    private Action<Tuple<string, T>> ForCurrentOperation<T>(Action<Tuple<string, T>> action)
    {
        return data =>
        {
            // Resolve the operation before it is removed from the queue.
            if (StoreHelper.Instance.GetItemIdFromOperationId(data.Item1) != _storeId)
                return;
            int version = _handlerVersion;
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_handlersConnected && version == _handlerVersion)
                    action(data);
            });
        };
    }

    private void ConnectHandlers()
    {
        if (_handlersConnected)
            return;
        _handlersConnected = true;
        ++_handlerVersion;
        ConnectDownloadHandlers();
        ConnectCompletionHandlers();
    }

    private void ConnectDownloadHandlers()
    {
        _itemDownloadStageChangedHandler = ForCurrentOperation<string>((data) =>
        {
            if (_errorHappens)
                return;
            string operationId = data.Item1;
            string stage = data.Item2;

            GetReadyUI();

            CurrentStatusSpeedTextBlock.Visibility = Visibility.Collapsed;

            PreferItemDownloadingStateActions(stage);
        });
        StoreHelper.Instance.ItemDownloadStageChanged += _itemDownloadStageChangedHandler;

        _itemDownloadProgressChangedHandler = ForCurrentOperation<double>((data) =>
        {
            if (_errorHappens)
                return;
            string operationId = data.Item1;
            double progress = data.Item2;


            StatusProgressbar.IsIndeterminate = false;
            StatusProgressbar.Value = progress;
        });
        StoreHelper.Instance.ItemDownloadProgressChanged += _itemDownloadProgressChangedHandler;

        _itemTimeRemainingChangedHandler = ForCurrentOperation<TimeSpan>((data) =>
        {
            if (_errorHappens)
                return;
            string operationId = data.Item1;
            TimeSpan time = data.Item2;


            if (time.TotalMinutes >= 1)
                CurrentStatusTipTextBlock.Text = string.Format(_localizer.GetLocalizedString("/UIHelper/LeftMinutes"), (int)time.TotalMinutes);
            else
                CurrentStatusTipTextBlock.Text = _localizer.GetLocalizedString("/UIHelper/LeftSmall");
        });

        StoreHelper.Instance.ItemTimeRemainingChanged += _itemTimeRemainingChangedHandler;

        _itemDownloadSpeedChangedHandler = ForCurrentOperation<double>((data) =>
        {
            if (_errorHappens)
                return;
            string operationId = data.Item1;
            double speed = data.Item2;


            CurrentStatusSpeedTextBlock.Text = $"{UnitsParser.FormatSpeed(speed)}, ";
        });

        StoreHelper.Instance.ItemDownloadSpeedChanged += _itemDownloadSpeedChangedHandler;

        _itemInstallingErrorHappensHandler = ForCurrentOperation<ErrorModel>((data) =>
        {
            string operationId = data.Item1;
            string errorCode = data.Item2.ErrorCode;


            ErrorStatusGrid.Visibility = Visibility.Visible;
            ItemActionButton.Visibility = Visibility.Collapsed;
            DownloadStatusGrid.Visibility = Visibility.Collapsed;

            ErrorNameTextBlock.Text = errorCode;
            _errorHappens = true;
        });
        StoreHelper.Instance.ItemInstallingErrorHappens += _itemInstallingErrorHappensHandler;
    }

    private void ConnectCompletionHandlers()
    {
        _itemActionsStoppedHandler = (id) =>
        {
            if (id != _storeId)
                return;
            int version = _handlerVersion;
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_handlersConnected && version == _handlerVersion)
                    ShowItemAfterInstallActions(id);
            });
        };
        StoreHelper.Instance.ItemActionsStopped += _itemActionsStoppedHandler;
        _itemRemovedHandler = id =>
        {
            if (id != _storeId)
                return;
            int version = _handlerVersion;
            DispatcherQueue.TryEnqueue(() =>
            {
                // Reinstall removes the old item before immediately queuing the replacement.
                if (_handlersConnected && version == _handlerVersion &&
                    StoreHelper.Instance.GetOperationIdFromItemId(_storeId) == null)
                {
                    ShowItemAfterInstallActions(id);
                }
            });
        };
        StoreHelper.Instance.ItemRemoved += _itemRemovedHandler;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _handlersConnected = false;
        ++_handlerVersion;
        StoreHelper.Instance.ItemRemoved -= _itemRemovedHandler;

        if (_itemDownloadStageChangedHandler != null)
            StoreHelper.Instance.ItemDownloadStageChanged -= _itemDownloadStageChangedHandler;

        if (_itemDownloadProgressChangedHandler != null)
            StoreHelper.Instance.ItemDownloadProgressChanged -= _itemDownloadProgressChangedHandler;

        if (_itemTimeRemainingChangedHandler != null)
            StoreHelper.Instance.ItemTimeRemainingChanged -= _itemTimeRemainingChangedHandler;

        if (_itemInstallingErrorHappensHandler != null)
            StoreHelper.Instance.ItemInstallingErrorHappens -= _itemInstallingErrorHappensHandler;

        if (_itemDownloadSpeedChangedHandler != null)
            StoreHelper.Instance.ItemDownloadSpeedChanged -= _itemDownloadSpeedChangedHandler;

        if (_itemActionsStoppedHandler != null)
            StoreHelper.Instance.ItemActionsStopped -= _itemActionsStoppedHandler;

        this.DataContext = null;
        MarkdownConfig = null;
    }

    private void ShowItemAfterInstallActions(string id)
    {
        if (id != _storeId)
            return;
        StatusProgressbar.Value = 0;
        StatusProgressbar.IsIndeterminate = true;

        CurrentStatusSpeedTextBlock.Visibility = Visibility.Collapsed;
        CurrentStatusSpeedTextBlock.Text = "";
        CurrentStatusTipTextBlock.Text = _localizer.GetLocalizedString("WorkingOnIt");

        if (id == _storeId && !_errorHappens)
        {
            ItemActionButton.IsEnabled = IsItemSupported();
            ItemActionButtonText.Text = _localizer.GetLocalizedString("Install");
            ItemMoreButton.Visibility = Visibility.Collapsed;
            ItemActionButton.Visibility = Visibility.Visible;
            DownloadStatusGrid.Visibility = Visibility.Collapsed;

            if (DatabaseHelper.Instance.IsItemInstalled(id))
            {
                ItemActionButtonText.Text = _localizer.GetLocalizedString("Setup");
                ItemMoreButton.Visibility = Visibility.Visible;

                ItemActionButton.IsEnabled = _item.type == "component";
            }
        }
    }
}
