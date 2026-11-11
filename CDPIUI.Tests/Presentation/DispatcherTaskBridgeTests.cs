using CDPIUI.Helper.UserExperience;

namespace CDPIUI.Tests.Presentation;

public class DispatcherTaskBridgeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunsDirectlyOrQueuesWithoutEagerOperationAndRetainsTheTrueResult(bool queued)
    {
        var f = new Fixture { Queue = queued };
        var task = f.Run(() => Task.CompletedTask);
        Assert.Equal(queued ? ["access", "enqueue"] : ["access", "operation"], f.Trace);
        Assert.Equal(!queued, task.IsCompleted);
        if (queued) f.RunQueued();
        Assert.True(task.IsCompletedSuccessfully);
        Assert.True(await Assert.IsAssignableFrom<Task<bool>>(task));
        Assert.Equal(1, f.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletionWaitsForTheOperationOnEitherDispatchPath(bool queued)
    {
        var pending = new TaskCompletionSource<bool>();
        var f = new Fixture { Queue = queued };
        var task = f.Run(() => pending.Task);
        if (queued) f.RunQueued();
        Assert.False(task.IsCompleted); Assert.Equal(1, f.Calls);
        pending.SetResult(true);
        await task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(task.IsCompletedSuccessfully);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ThrownOperationErrorsAreFaultsWithExactIdentityIncludingCancellation(bool queued, bool cancellation)
    {
        Exception expected = cancellation ? new OperationCanceledException("owned cancellation") : new InvalidOperationException("owned fault");
        var f = new Fixture { Queue = queued };
        var task = f.Run(() => throw expected);
        if (queued) f.RunQueued();
        Assert.True(task.IsFaulted); Assert.False(task.IsCanceled);
        Assert.Same(expected, task.Exception!.InnerException);
        Assert.Same(expected, Record.Exception(() => task.GetAwaiter().GetResult()));
    }

    [Fact]
    public async Task AnAwaitedOperationFaultRetainsItsIdentityAfterSuspension()
    {
        var pending = new TaskCompletionSource<bool>();
        var expected = new InvalidOperationException("late operation fault");
        var task = new Fixture().Run(() => pending.Task);
        Assert.False(task.IsCompleted);
        pending.SetException(expected);
        Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(() => task.WaitAsync(TimeSpan.FromSeconds(5))));
        Assert.True(task.IsFaulted);
    }

    [Fact]
    public void AnAwaitedCanceledTaskBecomesAFaultAndRetainsItsCancellationToken()
    {
        using var source = new CancellationTokenSource(); source.Cancel();
        var task = new Fixture().Run(() => Task.FromCanceled(source.Token));
        Assert.True(task.IsFaulted); Assert.False(task.IsCanceled);
        var error = Assert.IsType<TaskCanceledException>(task.Exception!.InnerException);
        Assert.Equal(source.Token, error.CancellationToken);
    }

    [Fact]
    public void NullOperationTaskIsRecoveredAsAFaultInsteadOfEscapingTheDispatcherCallback()
    {
        var task = new Fixture().Run(() => null!);
        Assert.True(task.IsFaulted); Assert.IsType<NullReferenceException>(task.Exception!.InnerException);
    }

    [Fact]
    public void QueueRejectionCancelsWithTheDefaultTokenWithoutCallingTheOperation()
    {
        var f = new Fixture { Queue = true, Accepted = false };
        var task = f.Run(() => Task.CompletedTask);
        Assert.True(task.IsCanceled); Assert.False(task.IsFaulted); Assert.Equal(0, f.Calls);
        var error = Assert.Throws<TaskCanceledException>(() => task.GetAwaiter().GetResult());
        Assert.Equal(default, error.CancellationToken);
        Assert.Equal(["access", "enqueue"], f.Trace);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DispatcherPortErrorsRemainSynchronousAndDoNotInvokeTheOperation(bool enqueueError)
    {
        var expected = new InvalidOperationException("dispatcher port");
        var f = new Fixture { Queue = enqueueError };
        if (enqueueError) f.OnEnqueue = _ => throw expected;
        else f.OnAccess = () => throw expected;
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => { _ = f.Run(() => Task.CompletedTask); }));
        Assert.Equal(0, f.Calls);
        Assert.Equal(enqueueError ? ["access", "enqueue"] : ["access"], f.Trace);
    }

    [Fact]
    public void ReadsCurrentThreadAccessForEachCall()
    {
        var f = new Fixture();
        var first = f.Run(() => Task.CompletedTask);
        f.Queue = true;
        var second = f.Run(() => Task.CompletedTask);
        Assert.True(first.IsCompletedSuccessfully); Assert.False(second.IsCompleted);
        Assert.Equal(1, f.Calls); f.RunQueued(); Assert.True(second.IsCompletedSuccessfully);
        Assert.Equal(["access", "operation", "access", "enqueue", "operation"], f.Trace);
    }

    [Fact]
    public async Task ARepeatedCallbackKeepsTheFirstCompletionWithoutSuppressingLaterWork()
    {
        var f = new Fixture { Queue = true }; int runs = 0;
        var task = f.Run(() => ++runs == 1 ? Task.CompletedTask : Task.FromException(new InvalidOperationException("later")));
        f.RunQueued(); f.RunQueued();
        Assert.Equal(2, runs); Assert.True(task.IsCompletedSuccessfully);
        Assert.True(await Assert.IsAssignableFrom<Task<bool>>(task));
    }

    [Fact]
    public void InlineQueueExecutionBeforeAFalseReplyRetainsItsAlreadyCompletedResult()
    {
        var f = new Fixture { Queue = true, OnEnqueue = callback => { callback(); return false; } };
        var task = f.Run(() => Task.CompletedTask);
        Assert.True(task.IsCompletedSuccessfully); Assert.Equal(1, f.Calls);
        Assert.Equal(["access", "enqueue", "operation"], f.Trace);
    }

    [Fact]
    public async Task ExternalContinuationCannotRunInlineOnTheCompletionThread()
    {
        await Task.Run(() =>
        {
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                var pending = new TaskCompletionSource<bool>();
                var task = new Fixture().Run(() => pending.Task);
                var scheduler = new ContinuationScheduler(); bool continued = false;
                var continuation = task.ContinueWith(_ => continued = true, CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously, scheduler);
                pending.SetResult(true);
                Assert.True(task.IsCompletedSuccessfully);
                Assert.False(scheduler.InlineAttempted); Assert.False(continued);
                scheduler.Drain();
                Assert.True(continued); Assert.True(continuation.IsCompletedSuccessfully);
            }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
        });
    }

    [Fact]
    public void AwaitedOperationRetainsTheCurrentSynchronizationContext()
    {
        var previous = SynchronizationContext.Current;
        var context = new QueuedContext();
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            var pending = new TaskCompletionSource<bool>();
            var task = new Fixture().Run(() => pending.Task);
            SynchronizationContext.SetSynchronizationContext(null);
            pending.SetResult(true);
            Assert.False(task.IsCompleted); Assert.Equal(1, context.PendingCount);
            SynchronizationContext.SetSynchronizationContext(context);
            context.Drain(); Assert.True(task.IsCompletedSuccessfully);
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SynchronousActionsDoNotEnterAsyncVoidLifecycleEvenWhenTheContextRejectsIt(bool queued, bool rejecting)
    {
        var previous = SynchronizationContext.Current;
        var context = new LifecycleContext { Reject = rejecting };
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            var f = new Fixture { Queue = queued }; int operations = 0;
            var task = f.RunSynchronous(() => operations++);
            if (queued) { Assert.False(task.IsCompleted); f.RunQueued(); }
            Assert.True(task.IsCompletedSuccessfully); Assert.Equal(1, operations);
            Assert.Equal(0, context.Started); Assert.Equal(0, context.Completed);
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SynchronousActionErrorsRemainFaultsWithoutEnteringAsyncVoidLifecycle(bool queued, bool cancellation)
    {
        var previous = SynchronizationContext.Current;
        var context = new LifecycleContext { Reject = true };
        Exception expected = cancellation ? new OperationCanceledException("sync cancel") : new InvalidOperationException("sync error");
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            var f = new Fixture { Queue = queued };
            var task = f.RunSynchronous(() => throw expected);
            if (queued) f.RunQueued();
            Assert.True(task.IsFaulted); Assert.False(task.IsCanceled);
            Assert.Same(expected, task.Exception!.InnerException);
            Assert.Equal(0, context.Started); Assert.Equal(0, context.Completed);
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    [Fact]
    public async Task SynchronousActionCompletionAlsoQueuesExternalContinuations()
    {
        await Task.Run(() =>
        {
            var f = new Fixture { Queue = true };
            var task = f.RunSynchronous(() => { });
            var scheduler = new ContinuationScheduler(); bool continued = false;
            var continuation = task.ContinueWith(_ => continued = true, CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, scheduler);
            f.RunQueued();
            Assert.True(task.IsCompletedSuccessfully); Assert.False(scheduler.InlineAttempted); Assert.False(continued);
            scheduler.Drain(); Assert.True(continued); Assert.True(continuation.IsCompletedSuccessfully);
        });
    }

    private sealed class Fixture
    {
        public bool Queue, Accepted = true;
        public int Calls;
        public Action? Queued;
        public Func<bool>? OnAccess;
        public Func<Action, bool>? OnEnqueue;
        public List<string> Trace { get; } = [];
        public Task Run(Func<Task> operation) => DispatcherTaskBridge.Run(
            () => { Calls++; Trace.Add("operation"); return operation(); },
            ReadAccess, Enqueue);
        public Task RunSynchronous(Action operation) => DispatcherTaskBridge.Run(
            () => { Calls++; Trace.Add("operation"); operation(); }, ReadAccess, Enqueue);
        private bool ReadAccess() { Trace.Add("access"); return OnAccess?.Invoke() ?? !Queue; }
        private bool Enqueue(Action callback) { Trace.Add("enqueue"); Queued = callback; return OnEnqueue?.Invoke(callback) ?? Accepted; }
        public void RunQueued() => Queued!();
    }

    private sealed class LifecycleContext : SynchronizationContext
    {
        public bool Reject;
        public int Started, Completed;
        public override void OperationStarted()
        { Started++; if (Reject) throw new InvalidOperationException("owned async-void rejection"); }
        public override void OperationCompleted() => Completed++;
    }

    private sealed class ContinuationScheduler : TaskScheduler
    {
        private readonly Queue<Task> _pending = new();
        public bool InlineAttempted;
        protected override IEnumerable<Task> GetScheduledTasks() => _pending.ToArray();
        protected override void QueueTask(Task task) => _pending.Enqueue(task);
        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        { InlineAttempted = true; return TryExecuteTask(task); }
        public void Drain() { while (_pending.TryDequeue(out var task)) TryExecuteTask(task); }
    }

    private sealed class QueuedContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _pending = new();
        public int PendingCount => _pending.Count;
        public override void Post(SendOrPostCallback callback, object? state) => _pending.Enqueue((callback, state));
        public void Drain() { while (_pending.TryDequeue(out var item)) item.Callback(item.State); }
    }
}
