using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.Queue;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Shared;
using CDPIUI.Shared.Models;
using CDPIUI.Shared.PrettyErrorConversionService;

namespace CDPIUI.Core.Store;

// The installation of one queue item, from the download or the local pack to the item database.
public partial class StoreHelper
{
    private async Task ProcessAsync(QueueItemModel qi)
    {
        using var operationSource = new CancellationTokenSource();
        _cancellationTokenSource = operationSource;
        _cancellationToken = operationSource.Token;

        bool installed = false;
        try
        {
            qi.Status = "GETR";
            qi.DownloadStage = qi.Status;
            ItemDownloadStageChanged?.Invoke(Tuple.Create(qi.OperationId, qi.Status));
            installed = await InstallItem(qi);
            qi.Status = operationSource.IsCancellationRequested ? "CANC" : "END";
            qi.DownloadStage = qi.Status;
            ItemDownloadStageChanged?.Invoke(Tuple.Create(qi.OperationId, qi.Status));
        }
        catch (Exception ex)
        {
            // The exception was swallowed and the item stayed in GETR/WORK with no error shown.
            Logger.Instance.CreateErrorLog(nameof(StoreHelper), $"Installation of {qi.ItemId} failed: {ex}");
            qi.Status = "END";
            qi.DownloadStage = qi.Status;
            ItemInstallingErrorHappens?.Invoke(Tuple.Create(qi.OperationId, ErrorModel.OnlyErrorCode(StoreInstallErrors.FromException(ex))));
            ItemDownloadStageChanged?.Invoke(Tuple.Create(qi.OperationId, qi.Status));
        }
        finally
        {
            _onlineItemsInstallationService.CompleteWork();
            _cancellationTokenSource = null;
            _cancellationToken = null;
        }
        ItemActionsStopped?.Invoke(qi.ItemId);

        // Only an installed update leaves the list; a failed one is still available.
        var _item = installed ? UpdatesAvailableList.FirstOrDefault(x => x.StoreId == qi.ItemId) : null;
        if (_item != null)
        {
            UpdatesAvailableList.Remove(_item);
        }

        lock (SyncRoot)
        {
            _queueManagerService.CurrentDownloadingItem = null;
            _queueManagerService.TryProcessNext();
        }
    }

    private async Task<bool> InstallItem(QueueItemModel qi)
    {
        bool wasInstalled = DatabaseHelper.Instance.IsItemInstalled(qi.ItemId);
        var result = await InstallItemWorker(qi);

        if (_cancellationToken?.IsCancellationRequested == true)
            return false;

        if (!result.Success)
        {
            ErrorModel errorModel;
            if (!result.ErrorHappens || result.Error == null)
            {
                errorModel = StoreInstallErrors.FromCode(PrettyErrorCode.UNKNOWN);
            }
            else
            {
                errorModel = result.Error;
            }

            ItemInstallingErrorHappens?.Invoke(Tuple.Create(qi.OperationId, errorModel));
            if (string.IsNullOrEmpty(qi.PackFilePath))
                AddItemToDownloadFailureList(qi.ItemId, qi.OperationId, qi.Version, errorModel.ErrorCode);

            try
            {
                // A failed update keeps the installed version; only a failed first
                // installation is cleaned up.
                if (!wasInstalled)
                {
                    await RemoveItemAsync(qi.ItemId);
                    StoreItemFolders.DeleteFolderWithoutRecord(qi.ItemId);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateWarningLog(nameof(StoreHelper), $"Cannot clean up {qi.ItemId} after a failed installation: {ex.Message}");
            }
            return false;
        }
        return true;
    }

    private async Task<OperationResultModel<EmptyResult>> InstallItemWorker(QueueItemModel qi)
    {
        string id = qi.ItemId;
        string packFilePath = qi.PackFilePath;
        NowProcessItemActions?.Invoke(id);

        if (!StoreItemIds.IsValid(id))
            return OperationResultModel<EmptyResult>
                .FailureResult(ErrorModel.OnlyErrorCode(StoreInstallErrors.FromException(new InvalidDataException($"Invalid store item id: {id}"))));

        List<Tuple<string, string>> requiredItems = [];
        bool restartAfterUpdate = false;
        if (DatabaseHelper.Instance.IsItemInstalled(id))
        {
            DatabaseStoreItem installedItem = DatabaseHelper.Instance.GetItemById(id);
            requiredItems = installedItem.RequiredItemIds;
            // Running executables cannot be overwritten (issue #37).
            restartAfterUpdate = await StopComponentForFileChanges(installedItem);
        }

        string itemFolder = Path.Combine(Directories.StoreItemsDirectory, qi.ItemId);

        var createDirResult = StoreItemFolders.CreateItemDirectory(id, itemFolder, qi.CleanDirectoryBeforeInstalling);
        if (createDirResult.ErrorHappens)
            return createDirResult;

        // For testing only
        // await Task.Delay(10000);

        RepoItemModel item;
        string tag;
        string downloadUrl;

        if (string.IsNullOrEmpty(packFilePath))
        {
            var onlineDownloadResult = await DownloadItemOnline(id, qi);

            if (!onlineDownloadResult.Success)
                return onlineDownloadResult.ToEmptyResult();

            item = onlineDownloadResult.Result.Item3;
            tag = onlineDownloadResult.Result.Item1;
            downloadUrl = onlineDownloadResult.Result.Item2;
        }
        else
        {
            _onlineItemsInstallationService.CreateDownloadWorker(qi.OperationId, _cancellationTokenSource!);

            var offlineCopyResult = LocalPackItemReader.Prepare(qi, GetItemInfoFromStoreId);

            if (!offlineCopyResult.Success)
                return offlineCopyResult.ToEmptyResult();

            item = offlineCopyResult.Result.Item3;
            tag = offlineCopyResult.Result.Item1;
            downloadUrl = offlineCopyResult.Result.Item2;
        }

        if (_cancellationToken?.IsCancellationRequested ?? false)
            return OperationResultModel<EmptyResult>
                .UnSuccessResult();

        if (id == SharedConstants.ApplicationStoreId)
            return await PrepareApplicationUpdate(itemFolder);

        var addResult = await _itemRegistrar.AddItemToDatabase(tag, downloadUrl, itemFolder, item, requiredItems, _cancellationToken);
        if (restartAfterUpdate)
            await ComponentTasksManager.Instance.CreateAndRunNewTask(id);
        return addResult;
    }

    private async Task<OperationResultModel<EmptyResult>> PrepareApplicationUpdate(string itemFolder)
    {
        if (State.IsApplicationBuildAsMsi)
            return OperationResultModel<EmptyResult>.SuccessResult();

        _applicationUpdateService = new();
        var gettingPatchReadyResult = await _applicationUpdateService
            .GetPatchReadyToInstall(
            Path.Combine(itemFolder, "patch.cdpipatch"),
            _onlineItemsInstallationService.DownloadWorker);

        _applicationUpdateService = null;
        return gettingPatchReadyResult;
    }

    private async Task<OperationResultModel<Tuple<string, string, RepoItemModel>>> DownloadItemOnline(string itemId, QueueItemModel qi)
    {
        RepoItemModel item;
        string tag;
        string downloadUrl;

        item = GetItemInfoFromStoreId(itemId);

        if (item == null)
        {
            return OperationResultModel<Tuple<string, string, RepoItemModel>>
                .FailureResult(StoreInstallErrors.FromCode(PrettyErrorCode.ITEM_NOT_FOUND));
        }

        // Set by ProcessAsync for the whole installation.
        var result = await _onlineItemsInstallationService.DownloadAndInstallItemFromOnlineStore(qi, item, _cancellationTokenSource!);

        if (!result.Success && result.ErrorHappens)
        {
            return OperationResultModel<Tuple<string, string, RepoItemModel>>.FailureResult(result.Error!);
        }

        if (!result.Success && !result.ErrorHappens)
        {
            return OperationResultModel<Tuple<string, string, RepoItemModel>>
                .UnSuccessResult();
        }

        if (result.Result == null || result.Result.version == null || result.Result.link == null)
            return OperationResultModel<Tuple<string, string, RepoItemModel>>
                .FailureResult(ErrorModel.OnlyErrorCode(PrettyErrorCode.NULL_REFERENCE));

        tag = result.Result.version;
        downloadUrl = result.Result.link;

        return OperationResultModel<Tuple<string, string, RepoItemModel>>
            .SuccessResult(Tuple.Create(tag, downloadUrl, item));
    }

    /// <summary>
    /// Stops the component before its files are replaced or deleted, together with the
    /// driver service it may hold open. Returns whether the component was running.
    /// </summary>
    private static async Task<bool> StopComponentForFileChanges(DatabaseStoreItem item)
    {
        if (item.Type != "component")
            return false;

        bool wasRunning = false;
        try
        {
            var task = await ComponentTasksManager.Instance.GetTaskFromId(item.Id!);
            wasRunning = task?.ProcessManager?.IsProcessRunning ?? false;
            await ComponentTasksManager.Instance.StopTask(item.Id!);
            await ProcessService.StopService();
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateWarningLog(nameof(StoreHelper), $"Cannot stop {item.Id}: {ex.Message}");
        }
        return wasRunning;
    }
}
