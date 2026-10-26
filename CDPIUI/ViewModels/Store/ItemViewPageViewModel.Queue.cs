using System;
using CDPIUI.Core.Store;
using CDPIUI.Helper.Parsers;
using CDPIUI.Shared.PrettyErrorConversionService;

namespace CDPIUI.ViewModels.Store;

public sealed partial class ItemViewPageViewModel
{
    private bool _handlersConnected;
    private int _handlerVersion;
    private Action<string> _itemRemovedHandler;

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
            // Resolve the operation before it is removed from the queue.
            if (StoreHelper.Instance.GetItemIdFromOperationId(data.Item1) != _storeId)
                return;
            int version = _handlerVersion;
            _dispatcherQueue.TryEnqueue(() =>
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
        _itemDownloadStageChangedHandler = ForCurrentOperation<string>(data => OnStageChanged(data.Item2));
        StoreHelper.Instance.ItemDownloadStageChanged += _itemDownloadStageChangedHandler;

        _itemDownloadProgressChangedHandler = ForCurrentOperation<double>(data => OnProgressChanged(data.Item2));
        StoreHelper.Instance.ItemDownloadProgressChanged += _itemDownloadProgressChangedHandler;

        _itemTimeRemainingChangedHandler = ForCurrentOperation<TimeSpan>(data => OnTimeRemainingChanged(data.Item2));
        StoreHelper.Instance.ItemTimeRemainingChanged += _itemTimeRemainingChangedHandler;

        _itemDownloadSpeedChangedHandler = ForCurrentOperation<double>(data =>
            OnSpeedChanged($"{UnitsParser.FormatSpeed(data.Item2)}, "));
        StoreHelper.Instance.ItemDownloadSpeedChanged += _itemDownloadSpeedChangedHandler;

        _itemInstallingErrorHappensHandler = ForCurrentOperation<ErrorModel>(data => OnInstallingError(data.Item2.ErrorCode));
        StoreHelper.Instance.ItemInstallingErrorHappens += _itemInstallingErrorHappensHandler;
    }

    private void ConnectCompletionHandlers()
    {
        _itemActionsStoppedHandler = (id) =>
        {
            if (id != _storeId)
                return;
            int version = _handlerVersion;
            _dispatcherQueue.TryEnqueue(() =>
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
            _dispatcherQueue.TryEnqueue(() =>
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

    private void DisconnectHandlers()
    {
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
    }
}
