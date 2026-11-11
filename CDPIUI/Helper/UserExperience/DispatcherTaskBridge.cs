using System;
using System.Threading.Tasks;

namespace CDPIUI.Helper.UserExperience;

/// <summary>Completes a task from an operation run directly or through a dispatcher.</summary>
internal static class DispatcherTaskBridge
{
    public static Task Run(Action operation, Func<bool> hasThreadAccess, Func<Action, bool> tryEnqueue)
    {
        var completion = CreateCompletion();
        void Execute()
        {
            try { operation(); completion.TrySetResult(true); }
            catch (Exception error) { completion.TrySetException(error); }
        }
        return Dispatch(Execute, completion, hasThreadAccess, tryEnqueue);
    }

    public static Task Run(Func<Task> operation, Func<bool> hasThreadAccess, Func<Action, bool> tryEnqueue)
    {
        var completion = CreateCompletion();
        async void Execute()
        {
            try
            {
                await operation();
                completion.TrySetResult(true);
            }
            catch (Exception error) { completion.TrySetException(error); }
        }
        return Dispatch(Execute, completion, hasThreadAccess, tryEnqueue);
    }

    private static TaskCompletionSource<bool> CreateCompletion() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static Task Dispatch(Action execute, TaskCompletionSource<bool> completion,
        Func<bool> hasThreadAccess, Func<Action, bool> tryEnqueue)
    {
        if (hasThreadAccess())
            execute();
        else if (!tryEnqueue(execute))
            completion.TrySetCanceled();
        return completion.Task;
    }
}
