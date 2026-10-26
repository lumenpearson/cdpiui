using System;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.Parsers;
using CDPIUI.Shared.PrettyErrorConversionService;

namespace CDPIUI.ViewModels.Store;

public sealed partial class StoreLocalItemInstallingDialogViewModel
{
    private Action<Tuple<string, string>> _itemDownloadStageChangedHandler;
    private Action<Tuple<string, double>> _itemDownloadProgressChangedHandler;
    private Action<Tuple<string, double>> _itemDownloadSpeedChangedHandler;
    private Action<Tuple<string, TimeSpan>> _itemTimeRemainingChangedHandler;
    private Action<Tuple<string, ErrorModel>> _itemInstallingErrorHappensHandler;
    private Action<string> _itemActionsStoppedHandler;

    private Action<Tuple<string, T>> ForCurrentOperation<T>(Action<Tuple<string, T>> action)
    {
        return data =>
        {
            if (StoreHelper.Instance.GetItemIdFromOperationId(data.Item1) != _storeId)
                return;
            _dispatcherQueue.TryEnqueue(() =>
            {
                if (!_isClosed)
                    action(data);
            });
        };
    }

    private void ConnectHandlers()
    {
        _itemDownloadStageChangedHandler = ForCurrentOperation<string>((data) =>
        {
            string stage = data.Item2;

            if (_isErrorHappens)
                return;

            ShowDownloadState();

            IsSpeedVisible = false;

            ApplyStage(stage);
        });
        StoreHelper.Instance.ItemDownloadStageChanged += _itemDownloadStageChangedHandler;

        _itemDownloadProgressChangedHandler = ForCurrentOperation<double>((data) =>
        {
            double progress = data.Item2;

            if (_isErrorHappens)
                return;

            IsProgressIndeterminate = false;
            ProgressValue = progress;
        });
        StoreHelper.Instance.ItemDownloadProgressChanged += _itemDownloadProgressChangedHandler;

        _itemTimeRemainingChangedHandler = ForCurrentOperation<TimeSpan>((data) =>
        {
        });

        StoreHelper.Instance.ItemTimeRemainingChanged += _itemTimeRemainingChangedHandler;

        _itemDownloadSpeedChangedHandler = ForCurrentOperation<double>((data) =>
        {
            double speed = data.Item2;

            SpeedText = $"{UnitsParser.FormatSpeed(speed)}";
        });

        StoreHelper.Instance.ItemDownloadSpeedChanged += _itemDownloadSpeedChangedHandler;

        _itemInstallingErrorHappensHandler = ForCurrentOperation<ErrorModel>((data) =>
        {
            string errorCode = data.Item2.ErrorCode;

            _isErrorHappens = true;

            IsDescriptionVisible = false;
            IsDownloadProgressVisible = false;
            IsErrorVisible = true;

            ErrorCode = errorCode;

            CancelButtonText = _localizer.GetLocalizedString("OkButtonText");
        });
        StoreHelper.Instance.ItemInstallingErrorHappens += _itemInstallingErrorHappensHandler;

        _itemActionsStoppedHandler = (id) =>
        {
            if (id != _storeId)
                return;
            _dispatcherQueue.TryEnqueue(() =>
            {
                if (!_isClosed && !_isErrorHappens && DatabaseHelper.Instance.IsItemInstalled(_storeId))
                    ShowItemAfterInstallActions();
            });
        };
        StoreHelper.Instance.ItemActionsStopped += _itemActionsStoppedHandler;
    }

    private void DisconnectHandlers()
    {
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
    }
}
