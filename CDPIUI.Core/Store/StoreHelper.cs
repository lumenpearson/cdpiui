using CDPIUI.Core.Basic;
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
    private readonly StoreItemRegistrar _itemRegistrar;
    private readonly StoreUpdateChecker _updateChecker;

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
        _itemRegistrar = new(_onlineItemsInstallationService);
        _updateChecker = new(_apiWorker, GetReadyToUseRepoUrl);

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
            await StoreItemFolders.DeleteItemDirectoryAsync(item.Directory);
        DatabaseHelper.Instance.DeleteItemById(itemId);

        ItemRemoved?.Invoke(itemId);
    }

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

            exceptionHappens = await _updateChecker.FindUpdatesAsync(UpdatesAvailableList);
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
}
