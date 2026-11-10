using System;
using System.Threading;
using System.Threading.Tasks;

namespace CDPIUI.Helper.Basic;

/// <summary>Publishes a pending readiness task before initialization can create or activate any window.</summary>
internal sealed class StartupInitialization
{
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _started;

    internal Task Ready => _ready.Task;

    internal void Start(Func<Task> initialize)
    {
        ArgumentNullException.ThrowIfNull(initialize);
        if (Interlocked.CompareExchange(ref _started, 1, 0) == 0)
            _ = CompleteAsync(initialize);
    }

    private async Task CompleteAsync(Func<Task> initialize)
    {
        try
        {
            await initialize();
            _ready.TrySetResult();
        }
        catch (OperationCanceledException exception)
        { _ready.TrySetCanceled(exception.CancellationToken); }
        catch (Exception exception)
        { _ready.TrySetException(exception); }
    }
}
