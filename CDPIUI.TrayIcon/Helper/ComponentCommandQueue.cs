using CDPIUI.TrayIcon.Helper.Basic;

namespace CDPIUI.TrayIcon.Helper;

// Commands that start or stop one component, or change its proxy, run one at a time and in
// the order they arrived, whether they come from the pipe, the tray menu or a conditional
// task: a stop that ran after the proxy setup of a restart left the proxy off. Other work,
// such as a GoodCheck run that takes minutes, is not held back.
internal static class ComponentCommandQueue
{
    private static readonly Dictionary<string, Task> Queues = [];
    private static readonly object QueuesSyncRoot = new();

    // Takes its place in the queue before the first await, so it must be called before any
    // await of the caller; the pipe runs a handler synchronously up to its first await.
    // A command must not wait here for a command of the same component, or both wait forever.
    public static async Task Run(string componentId, Func<Task> action)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task previous;
        lock (QueuesSyncRoot)
        {
            previous = Queues.GetValueOrDefault(componentId) ?? Task.CompletedTask;
            Queues[componentId] = done.Task;
        }

        try
        {
            // Never faults: each command reports its own errors.
            await previous;
            await action();
        }
        finally
        {
            lock (QueuesSyncRoot)
            {
                if (Queues.TryGetValue(componentId, out var last) && last == done.Task)
                    Queues.Remove(componentId);
            }
            done.SetResult();
        }
    }

    // For UI handlers: joins the queue now and does not wait for the command. Errors are logged.
    public static void Post(string componentId, Func<Task> action, string source)
    {
        _ = RunAndLog(componentId, action, source);
    }

    private static async Task RunAndLog(string componentId, Func<Task> action, string source)
    {
        try
        {
            await Run(componentId, action);
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(source, $"Command for {componentId} failed: {ex}");
        }
    }
}
