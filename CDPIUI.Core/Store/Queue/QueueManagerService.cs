namespace CDPIUI.Core.Store.Queue;

internal class QueueManagerService : IQueueManagerService
{
    private readonly Queue<QueueItemModel> Queue = new();
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
            Queue.Enqueue(new QueueItemModel(itemId, opId, version, cleanDirectoryBeforeInstalling, packFilePath: packFile, isSignedPack: isSignedPack));
        }
        TryProcessNext();
    }

    public bool RemoveItemFromQueue(string itemId)
    {
        bool removed;
        lock (_queueLock)
        {
            var items = Queue.ToList();
            removed = items.RemoveAll(i => i.ItemId == itemId) > 0;
            if (removed)
            {
                Queue.Clear();
                foreach (var i in items)
                    Queue.Enqueue(i);
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
            return new Queue<QueueItemModel>(Queue);
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
            return Queue.FirstOrDefault(item => item.ItemId == storeId)?.OperationId;
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
            return Queue.FirstOrDefault(item => item.OperationId == operationId);
        }
    }

    public void TryProcessNext()
    {
        QueueItemModel? next = null;
        lock (_queueLock)
        {
            if (CurrentDownloadingItem == null && Queue.Count > 0)
            {
                next = Queue.Dequeue();
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


    private readonly List<QueueItemModel> FailedToInstallItems = new();
    private readonly object _failedToInstallItemsLock = new();

    public event Action? ErrorListUpdated;

    public void RemoveItemFromDownloadFailureList(string itemId)
    {
        lock (_failedToInstallItemsLock)
        {
            var extstItem = FailedToInstallItems.FirstOrDefault(x => x.ItemId == itemId);
            if (extstItem != null)
            {
                FailedToInstallItems.Remove(extstItem);
                ErrorListUpdated?.Invoke();
            }
        }
    }
    public List<QueueItemModel> GetFailedToInstallItems()
    {
        return FailedToInstallItems;
    }

    public void AddItemToDownloadFailureList(string itemId, string operationId, string? version, string errorCode)
    {
        lock (_failedToInstallItemsLock)
        {
            var extstItem = FailedToInstallItems.FirstOrDefault(x => x.ItemId == itemId);
            if (extstItem != null)
                FailedToInstallItems.Remove(extstItem);
            FailedToInstallItems.Add(new(itemId, operationId, version, false)
            {
                ErrorCode = errorCode
            });
        }
        ErrorListUpdated?.Invoke();
    }
}
