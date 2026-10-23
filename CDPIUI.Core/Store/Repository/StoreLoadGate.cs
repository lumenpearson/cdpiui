namespace CDPIUI.Core.Store.Repository
{
    /// <summary>
    /// Runs Store loads one after another. A caller that asks for a load with the same key as the
    /// one still running gets that running task instead of starting a second download.
    /// </summary>
    internal sealed class StoreLoadGate<TKey> where TKey : notnull
    {
        private readonly object _lock = new();
        private readonly Dictionary<TKey, Task<bool>> _running = [];
        private Task _last = Task.CompletedTask;

        public Task<bool> Run(TKey key, Func<Task<bool>> load)
        {
            lock (_lock)
            {
                if (_running.TryGetValue(key, out Task<bool>? running))
                    return running;

                Task<bool> task = RunAfter(_last, key, load);

                // A load that finished at once is not kept: the next call must start a new one.
                if (!task.IsCompleted)
                {
                    _running[key] = task;
                    _last = task;
                }
                return task;
            }
        }

        private async Task<bool> RunAfter(Task previous, TKey key, Func<Task<bool>> load)
        {
            try
            {
                // Two different loads must not extract into the same folder at once.
                if (!previous.IsCompleted)
                {
                    try
                    {
                        await previous;
                    }
                    catch
                    {
                        // The previous caller sees its own error.
                    }
                }
                return await load();
            }
            finally
            {
                lock (_lock)
                {
                    _running.Remove(key);
                }
            }
        }
    }
}
