namespace CDPIUI.Core.Store.Queue;

internal class QueueManagerService : IQueueManagerService
{
    private readonly Queue<QueueItemModel> _queue = new();
    private readonly object _queueLock = new();

    public event Action? QueueUpdated;
    public event Action? CurrentItemRemovedFromQueue;
    public event Action<QueueItemModel>? ProcessItem;

    public QueueItemModel? CurrentDownloadingItem { get; set; }

    // Every access to Queue and CurrentDownloadingItem changes goes through _queueLock: the
    // queue is used from the UI thread, download callbacks and the install worker.
    // Events are raised outside the lock.
    public void AddItemToQueue(string itemId, string? version = null, bool cleanDirectoryBeforeInstalling = false, string? packFile = null, bool isSignedPack = false) // PUBLIC
    {
        RemoveItemFromDownloadFailureList(itemId);

        lock (_queueLock)
        {
            if (GetOperationIdFromItemId(itemId) != null)
                return;

            var opId = Guid.NewGuid().ToString();
            _queue.Enqueue(new QueueItemModel(itemId, opId, version, cleanDirectoryBeforeInstalling, packFilePath: packFile, isSignedPack: isSignedPack));
        }
        TryProcessNext();
    }

    public bool RemoveItemFromQueue(string itemId)
    {
        bool removed;
        lock (_queueLock)
        {
            var items = _queue.ToList();
            removed = items.RemoveAll(i => i.ItemId == itemId) > 0;
            if (removed)
            {
                _queue.Clear();
                foreach (var i in items)
                    _queue.Enqueue(i);
            }
        }
        if (removed)
            QueueUpdated?.Invoke();

        var currentItem = CurrentDownloadingItem;
        if (currentItem != null && currentItem.ItemId == itemId)
        {
            if (currentItem.Status != "CANC")
            {
                // A cancellation callback can finish this item and advance the queue.
                currentItem.Status = "CANC";
                currentItem.DownloadStage = "CANC";
                CurrentItemRemovedFromQueue?.Invoke();
                QueueUpdated?.Invoke();
            }
            return true;
        }
        return removed;
    }

    /// <summary>
    /// A copy of the waiting items.
    /// </summary>
    public Queue<QueueItemModel> GetQueue()
    {
        lock (_queueLock)
            return new Queue<QueueItemModel>(_queue);
    }

    public string GetCurrentQueueOperationId()
    {
        var current = CurrentDownloadingItem;
        return current != null ? current.OperationId : string.Empty;
    }

    public string? GetOperationIdFromItemId(string storeId)
    {
        lock (_queueLock)
        {
            var current = CurrentDownloadingItem;
            if (current != null && current.ItemId == storeId)
                return current.OperationId;
            return _queue.FirstOrDefault(item => item.ItemId == storeId)?.OperationId;
        }
    }

    public string? GetItemIdFromOperationId(string operationId)
    {
        return GetQueueItemFromOperationId(operationId)?.ItemId;
    }

    public QueueItemModel? GetQueueItemFromOperationId(string operationId)
    {
        lock (_queueLock)
        {
            var current = CurrentDownloadingItem;
            if (current != null && current.OperationId == operationId)
                return current;
            return _queue.FirstOrDefault(item => item.OperationId == operationId);
        }
    }

    public void TryProcessNext()
    {
        QueueItemModel? next = null;
        lock (_queueLock)
        {
            if (CurrentDownloadingItem == null && _queue.Count > 0)
            {
                next = _queue.Dequeue();
                CurrentDownloadingItem = next;
            }
        }

        QueueUpdated?.Invoke();
        if (next != null)
        {
            ProcessItem?.Invoke(next);
            QueueUpdated?.Invoke();
        }
    }


    private readonly List<QueueItemModel> _failedToInstallItems = new();
    private readonly object _failedToInstallItemsLock = new();

    public event Action? ErrorListUpdated;

    public void RemoveItemFromDownloadFailureList(string itemId)
    {
        lock (_failedToInstallItemsLock)
        {
            var extstItem = _failedToInstallItems.FirstOrDefault(x => x.ItemId == itemId);
            if (extstItem != null)
            {
                _failedToInstallItems.Remove(extstItem);
                ErrorListUpdated?.Invoke();
            }
        }
    }
    public List<QueueItemModel> GetFailedToInstallItems()
    {
        return _failedToInstallItems;
    }

    public void AddItemToDownloadFailureList(string itemId, string operationId, string? version, string errorCode)
    {
        lock (_failedToInstallItemsLock)
        {
            var extstItem = _failedToInstallItems.FirstOrDefault(x => x.ItemId == itemId);
            if (extstItem != null)
                _failedToInstallItems.Remove(extstItem);
            _failedToInstallItems.Add(new(itemId, operationId, version, false)
            {
                ErrorCode = errorCode
            });
        }
        ErrorListUpdated?.Invoke();
    }
}
