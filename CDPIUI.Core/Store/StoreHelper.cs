using System.Runtime.ExceptionServices;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.Data;
using CDPIUI.Core.JSON;
using CDPIUI.Core.LScript;
using CDPIUI.Core.Store.Application;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.Network;
using CDPIUI.Core.Store.Network.Models;
using CDPIUI.Core.Store.Queue;
using CDPIUI.Core.Store.Repository;
using CDPIUI.Core.Store.Repository.Localization;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Shared;
using CDPIUI.Shared.Models;
using CDPIUI.Shared.PrettyErrorConversionService;
using static CDPIUI.Core.Basic.ErrorsHelper;
using TimeSpan = System.TimeSpan;

namespace CDPIUI.Core.Store;

public partial class StoreHelper :
    IUserExperienceService, IRepositoryLoaderService, ILocalizationService, IQueueManagerService, IAPIWorker
{
    private readonly UserExperienceService _userExperienceService;
    private readonly RepositoryLoaderService _repositoryLoaderService = new();
    private readonly LocalizationService _localizationService = new();
    private readonly QueueManagerService _queueManagerService = new();
    private readonly APIWorker _apiWorker = new();
    private readonly OnlineItemsInstallationService _onlineItemsInstallationService;

    private ApplicationUpdateService? _applicationUpdateService;




    private CancellationTokenSource? _cancellationTokenSource;
    private CancellationToken? _cancellationToken;

    private static StoreHelper? _instance;
    private static readonly object SyncRoot = new object();

    public static StoreHelper Instance
    {
        get
        {
            lock (SyncRoot)
            {
                _instance ??= new StoreHelper();
                return _instance;
            }
        }
    }

    public event Action<string>? StoreInternalErrorHappens;
    public event Action<string>? NowProcessItemActions;
    public event Action<string>? ItemActionsStopped;

    public event Action<Tuple<string, ErrorModel>>? ItemInstallingErrorHappens;
    public event Action<Tuple<string, double>>? ItemDownloadSpeedChanged;
    public event Action<Tuple<string, double>>? ItemDownloadProgressChanged;
    public event Action<Tuple<string, TimeSpan>>? ItemTimeRemainingChanged;
    public event Action<Tuple<string, string>>? ItemDownloadStageChanged;

    public event Action<string>? ItemRemoved;

    public event Action? QueueUpdated;
    public event Action? ErrorListUpdated;

    public bool IsNowUpdatesChecked { get; private set; } = false;
    public bool IsExceptonHappensWhileCheckingUpdates { get; private set; } = false;
    public List<ItemUpdateAvailableModel> UpdatesAvailableList { get; private set; } = [];

    public Action? UpdateCheckStarted;
    public Action? UpdateCheckStopped;

    private StoreHelper()
    {
        _onlineItemsInstallationService = new() { APIWorker = _apiWorker };
        _userExperienceService = new(this);

        _onlineItemsInstallationService.DownloadStageChanged += OnlineItemsInstallationService_DownloadStageChanged;
        _onlineItemsInstallationService.DownloadProgressChanged += OnlineItemsInstallationService_DownloadProgressChanged;
        _onlineItemsInstallationService.TimeRemainingChanged += OnlineItemsInstallationService_TimeRemainingChanged;
        _onlineItemsInstallationService.DownloadSpeedChanged += OnlineItemsInstallationService_DownloadSpeedChanged;

        _onlineItemsInstallationService.DownloadWorkerErrorHappens += OnlineItemsInstallationService_DownloadWorkerErrorHappens;

        _repositoryLoaderService.InternalErrorHappens += RepositoryLoaderService_InternalErrorHappens;

        _queueManagerService.CurrentItemRemovedFromQueue += QueueManagerService_CurrentItemRemovedFromQueue;
        _queueManagerService.ProcessItem += QueueManagerService_ProcessItem;
        _queueManagerService.QueueUpdated += QueueUpdated; // Possible action issue.
        _queueManagerService.ErrorListUpdated += ErrorListUpdated; // Possible action issue.

        _apiWorker.VersionControl = _repositoryLoaderService.VersionControl;
        _apiWorker.Token = _repositoryLoaderService.GetToken();
    }

    private void RepositoryLoaderService_InternalErrorHappens(Shared.PrettyErrorConversionService.ErrorModel obj)
    {
        StoreInternalErrorHappens?.Invoke(obj.FriendlyDescription ?? obj.ErrorCode);
    }

    #region API
    public async Task<OperationResultModel<ReleaseInfoModel>> GetLastVersionAndVersionNotes(string url) =>
        await _apiWorker.GetLastVersionAndVersionNotes(url);

    #endregion

    #region UserExperience

    public List<Tuple<string, string>>? GetItemRequiredItemsById(string storeId) =>
        _userExperienceService.GetItemRequiredItemsById(storeId);

    public List<RepoItemModel> GetSimilarItemsForStoreId(string storeId) =>
        _userExperienceService.GetSimilarItemsForStoreId(storeId);

    /// <summary>
    /// Is current version control available.
    /// Proxy settings applied for this check.
    /// </summary>
    /// <returns><see cref="true"/> if server is available, otherwise <see cref="false"/></returns>
    public async Task<bool> IsServerAvailable()
    {
        var resilt = await GetLastVersionAndVersionNotes(
            VersionControl == SupportedVersionControls.GitHub
            ? SharedConstants.ApplicationCheckUpdatesUrl
            : SharedConstants.ApplicationGitLabCheckUpdatesUrl);

        return resilt.Success;
    }

    #endregion

    #region Database

    public SupportedVersionControls VersionControl => _repositoryLoaderService.VersionControl;
    public List<RepoCategoryModel>? FormattedStoreDatabase => _repositoryLoaderService.FormattedStoreDatabase;
    public Dictionary<string, string>? StoreLocalizationPaths => _repositoryLoaderService.StoreLocalizationPaths;
    public List<RepoItemModel> ItemsList => _repositoryLoaderService.ItemsList;

    public List<ReadyKitModel> ReadyKits => _repositoryLoaderService.ReadyKits;

    public static async Task<bool> TryLoadDatabaseForVersionControl(SupportedVersionControls versionControl) =>
        await RepositoryLoaderService.TryLoadDatabaseForVersionControl(versionControl);

    public async Task<bool> LoadAllStoreDatabase(bool forseSync = true, SupportedVersionControls versionControl = SupportedVersionControls.None) =>
        await _repositoryLoaderService.LoadAllStoreDatabase(forseSync, versionControl);


    public RepoItemModel? GetItemInfoFromStoreId(string? storeId) =>
        _repositoryLoaderService.GetItemInfoFromStoreId(storeId);

    public ReadyKitModel? GetReadyKitFromStoreId(string? storeId) =>
        string.IsNullOrWhiteSpace(storeId) ? null : _repositoryLoaderService.GetReadyKitFromStoreId(storeId);

    public RepoCategoryModel? GetCategoryFromStoreId(string storeId) =>
        _repositoryLoaderService.GetCategoryFromStoreId(storeId);

    public static void ClearRepoCache() =>
        RepositoryLoaderService.ClearRepoCache();

    #endregion

    #region Localization
    private static readonly Dictionary<string, string> NoLocalizationPaths = [];

    public string GetLocalizedStoreItemName(string? name, string langCode)
    {
        // The setter clears the cached strings, so it runs only after a repository sync
        // replaced the paths; before, every item name re-read the localization file.
        var paths = _repositoryLoaderService.StoreLocalizationPaths ?? NoLocalizationPaths;
        if (!ReferenceEquals(_localizationService.StoreLocalizationPaths, paths))
            _localizationService.StoreLocalizationPaths = paths;
        return _localizationService.GetLocalizedStoreItemName(name, langCode);
    }
    #endregion

    #region Failure items list
    public List<QueueItemModel> GetFailedToInstallItems() =>
        _queueManagerService.GetFailedToInstallItems();

    public void RemoveItemFromDownloadFailureList(string itemId) =>
        _queueManagerService.RemoveItemFromDownloadFailureList(itemId);

    private void AddItemToDownloadFailureList(string itemId, string operationId, string? version, string errorCode) =>
        _queueManagerService.AddItemToDownloadFailureList(itemId, operationId, version, errorCode);
    #endregion

    #region Queue
    public void AddItemToQueue(string itemId, string? version = null, bool cleanDirectoryBeforeInstalling = false, string? packFile = null, bool isSignedPack = false) =>
        _queueManagerService.AddItemToQueue(itemId, version, cleanDirectoryBeforeInstalling, packFile, isSignedPack);

    public bool RemoveItemFromQueue(string itemId) =>
        _queueManagerService.RemoveItemFromQueue(itemId);

    public string GetCurrentQueueOperationId() =>
        _queueManagerService.GetCurrentQueueOperationId();

    public string? GetItemIdFromOperationId(string operationId) =>
        _queueManagerService.GetItemIdFromOperationId(operationId);

    public string? GetOperationIdFromItemId(string storeId) =>
        _queueManagerService.GetOperationIdFromItemId(storeId);

    public Queue<QueueItemModel> GetQueue() =>
        _queueManagerService.GetQueue();

    public QueueItemModel? GetQueueItemFromOperationId(string operationId) =>
        _queueManagerService.GetQueueItemFromOperationId(operationId);

    private void QueueManagerService_CurrentItemRemovedFromQueue()
    {
        ItemDownloadStageChanged?.Invoke(
            Tuple.Create(_queueManagerService.CurrentDownloadingItem.OperationId, _queueManagerService.CurrentDownloadingItem.Status));
        _cancellationTokenSource?.Cancel();
    }

    private void QueueManagerService_ProcessItem(QueueItemModel obj)
    {
        _ = ProcessAsync(obj);
    }



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
            ItemInstallingErrorHappens?.Invoke(Tuple.Create(qi.OperationId, ErrorModel.OnlyErrorCode(HandleException(ex))));
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

    #endregion

    public void RemoveItem(string itemId)
    {
        _ = RemoveItemAsync(itemId);
    }

    public async Task RemoveItemAsync(string itemId)
    {
        var item = DatabaseHelper.Instance.GetItemById(itemId);

        if (item == null ||
            item.Id == SharedConstants.ApplicationStoreId ||
            item.Id == SharedConstants.LocalUserItemsId)
            return;

        // The component has to exit before its files can be deleted (issue #37).
        await StopComponentForFileChanges(item);

        // The directory comes from the item database; never delete anything outside the item folders.
        if (Path.Exists(item.Directory) && StoreItemIds.IsItemFolder(item.Directory))
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    Directory.Delete(item.Directory, recursive: true);
                    break;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    if (attempt == 5)
                    {
                        Logger.Instance.CreateErrorLog(nameof(StoreHelper), $"Cannot delete \"{item.Directory}\": {ex.Message}");
                        break;
                    }
                    await Task.Delay(500 * attempt);
                }
            }
        }
        DatabaseHelper.Instance.DeleteItemById(itemId);

        ItemRemoved?.Invoke(itemId);
    }

    /// <summary>
    /// RemoveItemAsync works from the database record, which a failed first installation does
    /// not have yet; its downloaded files stayed in the item folder.
    /// </summary>
    private static void DeleteFolderWithoutRecord(string itemId)
    {
        if (!StoreItemIds.IsValid(itemId) ||
            itemId == SharedConstants.ApplicationStoreId ||
            itemId == SharedConstants.LocalUserItemsId ||
            DatabaseHelper.Instance.GetItemById(itemId) != null)
            return;

        string itemFolder = Path.Combine(Directories.StoreItemsDirectory, itemId);
        if (!Directory.Exists(itemFolder) || !StoreItemIds.IsItemFolder(itemFolder))
            return;
        try
        {
            Directory.Delete(itemFolder, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Instance.CreateWarningLog(nameof(StoreHelper), $"Cannot delete \"{itemFolder}\": {ex.Message}");
        }
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

    // Update checks sent at the same time; more would hit the API rate limits sooner.
    private const int UpdateCheckConcurrency = 4;

    public async Task CheckUpdates()
    {
        if (IsNowUpdatesChecked)
            return;
        IsNowUpdatesChecked = true;
        bool exceptionHappens = false;
        // The flag must be reset whatever happens, otherwise the Store shows "checking" forever.
        try
        {
            UpdatesAvailableList.Clear();
            UpdateCheckStarted?.Invoke();

            List<DatabaseStoreItem> storeItems = DatabaseHelper.Instance.GetAllInstalledItems();

            List<(DatabaseStoreItem Item, string RepoUrl)> itemsToCheck = [];
            ExceptionDispatchInfo? prepareError = null;
            foreach (var item in storeItems)
            {
                if (item.Id == SharedConstants.LocalUserItemsId || item.VersionControlType == "local")
                    continue;

                string? repoUrl;
                try
                {
                    repoUrl = GetReadyToUseRepoUrl(item.UpdateCheckUrl, item.Id);
                }
                catch (Exception ex)
                {
                    // The items before this one are still checked, as when they were checked one by one.
                    prepareError = ExceptionDispatchInfo.Capture(ex);
                    break;
                }
                // Local packs have no update source; that is not an error.
                if (string.IsNullOrEmpty(repoUrl))
                    continue;

                itemsToCheck.Add((item, repoUrl));
            }

            // The requests run a few at a time; the results are handled below in the item order.
            using SemaphoreSlim requestSlots = new(UpdateCheckConcurrency);
            var versionRequests = itemsToCheck
                .Select(async x =>
                {
                    await requestSlots.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        return await _apiWorker.GetLastVersionAndVersionNotes(x.RepoUrl).ConfigureAwait(false);
                    }
                    finally
                    {
                        requestSlots.Release();
                    }
                })
                .ToList();
            try
            {
                await Task.WhenAll(versionRequests);
            }
            catch
            {
                // Each failed request is handled below at its place in the list.
            }

            for (int i = 0; i < itemsToCheck.Count; i++)
            {
                var (item, repoUrl) = itemsToCheck[i];
                var versionDataCheckResult = await versionRequests[i];

                if (versionDataCheckResult.ErrorHappens || !versionDataCheckResult.Success)
                {
                    Logger.Instance.CreateWarningLog(
                        $"{nameof(StoreHelper)}/{nameof(CheckUpdates)}",
                        $"Cannot check updates for {item.Id}, with version control type {item.VersionControlType}. Uri used to check {repoUrl} " +
                        $"Exception information: {versionDataCheckResult.Error.ErrorCode}");
                    exceptionHappens = true;
                    continue;
                }

                var versionData = versionDataCheckResult.Result;

                try
                {
                    string curV = item.CurrentVersion;
                    string serV = versionData.ReleaseTag;

                    if (VersionHelper.CompareVersionStrings(curV, serV) == -1)
                    {
                        UpdatesAvailableList.Add(new()
                        {
                            StoreId = item.Id,
                            CurrentVersion = item.CurrentVersion,
                            ServerVersion = versionData.ReleaseTag,
                            VersionInfo = versionData.ReleaseNotes,
                        });
                    }
                }
                catch
                {
                    Logger.Instance.CreateWarningLog(
                        $"{nameof(StoreHelper)}/{nameof(CheckUpdates)}",
                        $"Cannot compare versions {item.CurrentVersion}&&{versionData.ReleaseNotes} for {item.Id}");
                    exceptionHappens = true;
                    continue;
                }
            }

            prepareError?.Throw();
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog($"{nameof(StoreHelper)}/{nameof(CheckUpdates)}", ex.ToString());
            exceptionHappens = true;
        }
        finally
        {
            IsNowUpdatesChecked = false;
            IsExceptonHappensWhileCheckingUpdates = exceptionHappens;

            UpdateCheckStopped?.Invoke();
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
                errorModel = HandleException(PrettyErrorCode.UNKNOWN);
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
                    DeleteFolderWithoutRecord(qi.ItemId);
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
        string version = qi.Version;
        string packFilePath = qi.PackFilePath;
        NowProcessItemActions?.Invoke(id);


        if (!StoreItemIds.IsValid(id))
            return OperationResultModel<EmptyResult>
                .FailureResult(ErrorModel.OnlyErrorCode(HandleException(new InvalidDataException($"Invalid store item id: {id}"))));

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

        var createDirResult = CreateItemDirectory(id, itemFolder, qi.CleanDirectoryBeforeInstalling);
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

            var offlineCopyResult = GetReadyLocalItem(qi);

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

        var addResult = await AddItemToDatabase(tag, downloadUrl, itemFolder, item, requiredItems);
        if (restartAfterUpdate)
            await ComponentTasksManager.Instance.CreateAndRunNewTask(id);
        return addResult;
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
                .FailureResult(HandleException(PrettyErrorCode.ITEM_NOT_FOUND));
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

    private OperationResultModel<Tuple<string?, string?, RepoItemModel>> GetReadyLocalItem(QueueItemModel qi)
    {
        RepoItemModel item;
        string? tag;
        string downloadUrl;

        string id = qi.ItemId;
        string packFilePath = qi.PackFilePath!;

        string itemFolder = Path.Combine(Directories.StoreItemsDirectory, qi.ItemId);

        var copyResult = CopyLocalPack(id, itemFolder, packFilePath);
        if (!copyResult.Success)
            return OperationResultModel<Tuple<string?, string?, RepoItemModel>>.FailureResult(copyResult.Error);

        if (qi.ItemId == SharedConstants.ApplicationStoreId)
        {
            item = GetItemInfoFromStoreId(qi.ItemId);
            tag = string.Empty;
            downloadUrl = null;
        }
        else
        {
            string initFilePath = Path.Combine(itemFolder, "init.json");

            if (!File.Exists(initFilePath))
                return OperationResultModel<Tuple<string?, string?, RepoItemModel>>
                    .FailureResult(HandleException(PrettyErrorCode.PACK_NOT_SUPPORTED_TYPE));

            LocalItemInitModel? localItemInitModel;
            try
            {
                localItemInitModel = JSONConvertor.LoadJson<LocalItemInitModel>(initFilePath);
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(StoreHelper), $"Cannot read \"{initFilePath}\": {ex.Message}");
                localItemInitModel = null;
            }
            if (localItemInitModel == null)
                return OperationResultModel<Tuple<string?, string?, RepoItemModel>>
                    .FailureResult(HandleException(PrettyErrorCode.PACK_NOT_SUPPORTED_TYPE));

            item = new()
            {
                store_id = qi.ItemId,
                type = localItemInitModel.Type ?? "configlist",
                name = localItemInitModel.Name ?? localItemInitModel.ShortName ?? qi.ItemId,
                short_name = localItemInitModel.ShortName ?? qi.ItemId,
                // An unsigned pack is only a set of presets and lists: it cannot name the executable
                // the tray starts elevated, and it cannot run install actions.
                target_executable_file = qi.IsSignedPack ? localItemInitModel.ExecutableFile : null,
                filetype = "localPack",
                version_control = "local",
                icon = localItemInitModel.Icon,
                developer = localItemInitModel.Developer,
                background = localItemInitModel.Color,
                dependencies = localItemInitModel.Requirements,
                before_install_actions = qi.IsSignedPack ? localItemInitModel.BeforeInstallActions : null,
                after_install_actions = qi.IsSignedPack ? localItemInitModel.AfterInstallActions : null,
            };
            tag = localItemInitModel.Version;
            downloadUrl = null;
        }

        if (item == null)
            return OperationResultModel<Tuple<string?, string?, RepoItemModel>>
                .FailureResult(HandleException(PrettyErrorCode.NULL_REFERENCE));

        return OperationResultModel<Tuple<string?, string?, RepoItemModel>>
            .SuccessResult(Tuple.Create(tag, downloadUrl, item));
    }

    // A local pack names its icon as $DYNAMICIMAGE(icon.png) relative to the pack. Evaluated later
    // without the pack folder, the path pointed into the repository cache, so the icon of a local
    // pack is stored as a full path, and only when the file is inside the item folder.
    private static string? GetIconPath(RepoItemModel item, string itemFolder)
    {
        const string prefix = "$DYNAMICIMAGE(";
        string? icon = item.icon;
        if (item.filetype != "localPack" || icon == null ||
            !icon.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !icon.EndsWith(')'))
            return icon;

        try
        {
            string root = Path.GetFullPath(itemFolder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(itemFolder, icon[prefix.Length..^1]));
            return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(path) ? path : icon;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return icon;
        }
    }

    private static OperationResultModel<EmptyResult> CreateItemDirectory(string itemId, string itemFolder, bool clean)
    {
        if (itemId == SharedConstants.ApplicationStoreId || itemId == SharedConstants.LocalUserItemsId)
            clean = false;

        try
        {
            if (Path.Exists(itemFolder) && !string.Equals(itemFolder, Directories.DataDirectory, StringComparison.OrdinalIgnoreCase))
            {
                if (clean)
                    Directory.Delete(itemFolder, recursive: true);
            }
            Directory.CreateDirectory(itemFolder);
        }
        catch (Exception ex)
        {
            return OperationResultModel<EmptyResult>
                .FailureResult(ErrorModel.OnlyErrorCode(HandleException(ex)));
        }

        return OperationResultModel<EmptyResult>
            .SuccessResult();
    }

    private static OperationResultModel<EmptyResult> CopyLocalPack(string itemId, string itemFolder, string packFilePath)
    {
        try
        {
            if (itemId != SharedConstants.ApplicationStoreId)
            {
                // The installed version is replaced only by a pack that can be installed.
                if (!File.Exists(Path.Combine(packFilePath, "init.json")))
                    return OperationResultModel<EmptyResult>
                        .FailureResult(HandleException(PrettyErrorCode.PACK_NOT_SUPPORTED_TYPE));

                string? previous = null;
                if (Directory.Exists(itemFolder))
                {
                    previous = $"{itemFolder}.previous-{Guid.NewGuid():N}";
                    Directory.Move(itemFolder, previous);
                }
                try
                {
                    Directory.Move(packFilePath, itemFolder);
                }
                catch
                {
                    if (previous != null && !Directory.Exists(itemFolder))
                        Directory.Move(previous, itemFolder);
                    throw;
                }
                if (previous != null)
                {
                    try
                    { Directory.Delete(previous, recursive: true); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        Logger.Instance.CreateWarningLog(nameof(StoreHelper), $"Cannot delete the previous version \"{previous}\": {ex.Message}");
                    }
                }
            }
            else
            {
                File.Copy(packFilePath, Path.Combine(itemFolder, "patch.cdpipatch"), overwrite: true);
            }
        }
        catch (Exception ex)
        {
            return OperationResultModel<EmptyResult>
                .FailureResult(ErrorModel.OnlyErrorCode(HandleException(ex)));
        }

        return OperationResultModel<EmptyResult>
            .SuccessResult();
    }

    private async Task<OperationResultModel<EmptyResult>> AddItemToDatabase(
        string? version,
        string? downloadUrl,
        string itemFolder,
        RepoItemModel item,
        List<Tuple<string, string>>? requiredItems)
    {
        List<Tuple<string, string>> _dependencies = [];

        // A one-element entry in init.json threw here and failed the installation silently.
        foreach (string[] dependency in item.dependencies ?? [])
        {
            if (dependency is { Length: >= 2 })
                _dependencies.Add(Tuple.Create(dependency[0], dependency[1]));
        }

        DatabaseStoreItem databaseStoreItem = new()
        {
            Id = item.store_id,
            Type = item.type,
            Name = item.name,
            ShortName = item.short_name,
            CurrentVersion = version,
            Directory = itemFolder,
            Executable = item.target_executable_file,
            DownloadUrl = downloadUrl,
            DownloadFileType = item.filetype,
            IconPath = GetIconPath(item, itemFolder),
            UpdateCheckUrl = item.version_control_link,
            VersionControlType = item.version_control,
            DependentItemIds = _dependencies,
            RequiredItemIds = requiredItems,
            Developer = item.developer,
            BackgroundColor = item.background,
        };


        // Both action lists run with the item folder: install_msi of a "before" action had none
        // and always failed.
        Dictionary<string, string> extraArgs = new()
        {
            { "CurrentDirectory", itemFolder }
        };

        try
        {
            if (!await _onlineItemsInstallationService.LaunchBeforeInstallActions(
                    await LScriptCore.RunScript(item.before_install_actions, extraArgs: extraArgs, cancellationToken: _cancellationToken),
                    itemFolder))
                return OperationResultModel<EmptyResult>
                    .UnSuccessResult();


            AddDependencies(item.dependencies, item.store_id!);

            // TODO: add RequiredItemIds to new installed item from store (foreach)

            Logger.Instance.CreateDebugLog(nameof(StoreHelper), $"Adding item {item.name} to database...");
            if (!DatabaseHelper.Instance.AddOrUpdateItem(databaseStoreItem))
                return OperationResultModel<EmptyResult>
                    .FailureResult(ErrorModel.OnlyErrorCode(HandleException(new IOException($"Cannot save {item.store_id} to the item database."))));

            // download_easy_designer_annotation_file returns a download request; it was dropped.
            string? afterDownloads = await LScriptCore.RunScript(item.after_install_actions, extraArgs: extraArgs, cancellationToken: _cancellationToken);
            if (!await _onlineItemsInstallationService.LaunchBeforeInstallActions(afterDownloads, itemFolder))
                Logger.Instance.CreateWarningLog(nameof(StoreHelper), $"Downloads after the installation of {item.store_id} failed.");

            Logger.Instance.CreateDebugLog(nameof(StoreHelper), $"Adding item {item.name}... COMPLETE");

            return OperationResultModel<EmptyResult>.SuccessResult();
        }
        catch (Exception ex)
        {
            return OperationResultModel<EmptyResult>
                .FailureResult(ErrorModel.OnlyErrorCode(HandleException(ex)));
        }
    }

    private static void AddDependencies(List<string[]>? dependencies, string id)
    {
        foreach (var dependency in dependencies ?? [])
        {
            if (dependency.Length < 2 || !DatabaseHelper.Instance.IsItemInstalled(dependency[0]))
                continue;

            DatabaseStoreItem dependencyItem = DatabaseHelper.Instance.GetItemById(dependency[0]);
            if (dependencyItem == null)
                continue;
            // A reinstall added the same entry again every time.
            dependencyItem.RequiredItemIds ??= [];
            if (dependencyItem.RequiredItemIds.Any(r => r.Item1 == id))
                continue;
            dependencyItem.RequiredItemIds.Add(Tuple.Create(id, dependency[1]));
            DatabaseHelper.Instance.AddOrUpdateItem(dependencyItem);
        }

    }

    /// <summary>
    /// The URL to check for updates of an item: the stored one when it is on the host of the
    /// Store in use, otherwise the link from the Store catalog (the item was installed from the
    /// other mirror or has no stored URL). Null when the item has nothing to check, such as a
    /// local pack.
    /// </summary>
    private string? GetReadyToUseRepoUrl(string? repoUrl, string? storeId)
    {
        string? expectedHost = VersionControl switch
        {
            SupportedVersionControls.GitHub => "github.com",
            SupportedVersionControls.GitLab => "gitlab.com",
            _ => null
        };

        if (Uri.TryCreate(repoUrl, UriKind.Absolute, out var repoUri) &&
            (expectedHost == null || repoUri.Host.Equals(expectedHost, StringComparison.OrdinalIgnoreCase)))
        {
            return repoUrl;
        }

        return GetItemInfoFromStoreId(storeId)?.version_control_link ?? repoUrl;
    }



    #region Handlers
    private void OnlineItemsInstallationService_DownloadWorkerErrorHappens(Tuple<string, ErrorModel> obj)
    {
        AddItemToDownloadFailureList(
                    GetItemIdFromOperationId(obj.Item1) ?? string.Empty, obj.Item1, null, obj.Item2.ErrorCode);

        ItemInstallingErrorHappens?.Invoke(Tuple.Create(obj.Item1, obj.Item2));
    }

    private void OnlineItemsInstallationService_DownloadSpeedChanged(Tuple<string, double> obj)
    {
        ItemDownloadSpeedChanged?.Invoke(obj);
    }

    private void OnlineItemsInstallationService_TimeRemainingChanged(Tuple<string, TimeSpan> obj)
    {
        ItemTimeRemainingChanged?.Invoke(obj);
    }

    private void OnlineItemsInstallationService_DownloadProgressChanged(Tuple<string, double> obj)
    {
        QueueItemModel? queueItem = GetQueueItemFromOperationId(obj.Item1);
        if (queueItem != null)
            queueItem.DownloadProgress = obj.Item2;

        ItemDownloadProgressChanged?.Invoke(obj);
    }

    private void OnlineItemsInstallationService_DownloadStageChanged(Tuple<string, string> obj)
    {
        QueueItemModel? queueItem = GetQueueItemFromOperationId(obj.Item1);
        if (queueItem != null)
            queueItem.DownloadStage = obj.Item2;

        ItemDownloadStageChanged?.Invoke(obj);
    }
    #endregion

    #region Exception Handlers
    private static ErrorModel HandleException(PrettyErrorCode error)
    {
        return new() { ErrorCode = $"ERR_STORE_{error}" };
    }

    private static string HandleException(Exception ex)
    {
        string errorCode = Convertor.GetPrettyErrorCode("ERR_ITEM_INSTALLING", ex);
        return errorCode;
    }

    #endregion
}
