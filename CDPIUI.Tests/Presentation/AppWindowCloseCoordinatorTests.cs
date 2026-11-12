using System.Runtime.CompilerServices;
using CDPIUI.Helper.Basic;

namespace CDPIUI.Tests.Presentation;

public sealed class AppWindowCloseCoordinatorTests
{
    [Fact]
    public void ConstructorOnlyStoresListAndOperations()
    {
        var f = new Fixture(); Assert.Empty(f.Ops.Trace); Assert.Same(f.Window, Assert.Single(f.Windows));
    }

    [Fact]
    public void NormalCloseCleansBeforeCurrentStoreQueryAndLowEnqueue()
    {
        var f = new Fixture(); var counts = new List<(string, int)>();
        f.Ops.OnStep = step => counts.Add((step, f.Windows.Count));
        f.Owner.HandleClosed(f.Window);
        Assert.Equal(["capture", "migration", "save", "detach", "release", "forget", "store", "enqueue"], f.Ops.Trace);
        Assert.Contains(("release", 1), counts); Assert.Contains(("forget", 0), counts); Assert.Empty(f.Windows);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MigrationCompletesInsideFinallyThenEnqueuesWithoutStoreOrPrepare(bool storeWorking)
    {
        var f = new Fixture(); f.Ops.Migration = true; f.Ops.StoreWorking = storeWorking;
        f.Owner.HandleClosed(f.Window);
        Assert.Equal(["capture", "migration", "save", "detach", "release", "complete", "forget", "enqueue"], f.Ops.Trace);
        Assert.Empty(f.Windows);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectedEnqueueRunsSynchronousExitAfterCleanup(bool migration)
    {
        var f = new Fixture(); f.Ops.Migration = migration; f.Ops.Queue = () => false;
        f.Owner.HandleClosed(f.Window);
        Assert.Equal(["enqueue", "exit"], f.Ops.Trace.TakeLast(2)); Assert.Empty(f.Windows);
    }

    [Fact]
    public void EmptyWindowListWithStoreWorkStartsPrepareWithoutQueueOrExit()
    {
        var f = new Fixture(); f.Ops.StoreWorking = true;
        f.Owner.HandleClosed(f.Window);
        Assert.Equal(["capture", "migration", "save", "detach", "release", "forget", "store", "prepare"], f.Ops.Trace);
    }

    [Fact]
    public void RemainingWindowSkipsStoreQueryAndDoesNotCreatePrepare()
    {
        var f = new Fixture(); var other = new TestWindow(); f.Windows.Add(other); f.Ops.StoreWorking = true;
        f.Owner.HandleClosed(f.Window);
        Assert.Same(other, Assert.Single(f.Windows)); Assert.DoesNotContain("store", f.Ops.Trace);
        Assert.DoesNotContain("prepare", f.Ops.Trace); Assert.Equal("enqueue", f.Ops.Trace.Last());
    }

    [Fact]
    public void MissingWindowStillRunsCleanupAndDuplicateReferenceRemovesOnlyOne()
    {
        var f = new Fixture(); f.Windows.Clear(); f.Owner.HandleClosed(f.Window);
        Assert.Contains("forget", f.Ops.Trace); Assert.Contains("store", f.Ops.Trace); Assert.Empty(f.Windows);
        f = new Fixture(); f.Windows.Add(f.Window); f.Owner.HandleClosed(f.Window);
        Assert.Same(f.Window, Assert.Single(f.Windows)); Assert.DoesNotContain("store", f.Ops.Trace);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MigrationBooleanIsCapturedBeforeSaveCallbacks(bool initial)
    {
        var f = new Fixture(); f.Ops.Migration = initial;
        f.Ops.OnStep = step => { if (step == "save") f.Ops.Migration = !initial; };
        f.Owner.HandleClosed(f.Window);
        Assert.Equal(initial, f.Ops.Trace.Contains("complete")); Assert.Equal(!initial, f.Ops.Trace.Contains("store"));
        Assert.Equal(1, f.Ops.Trace.Count(x => x == "migration"));
    }

    [Fact]
    public void EnqueueTargetIsCapturedBeforeSaveAndCalledOnlyAfterCleanup()
    {
        var f = new Fixture(); var queues = new List<string>();
        f.Ops.Queue = () => { queues.Add("original"); return true; };
        f.Ops.OnStep = step => { if (step == "save") f.Ops.Queue = () => { queues.Add("replacement"); return false; }; };
        f.Owner.HandleClosed(f.Window);
        Assert.Equal(["original"], queues); Assert.Equal("enqueue", f.Ops.Trace.Last()); Assert.DoesNotContain("exit", f.Ops.Trace);
    }

    [Fact]
    public void ForgetReentryUsesCurrentListCountRatherThanEarlierSnapshot()
    {
        var f = new Fixture(); var added = new TestWindow();
        f.Ops.OnStep = step => { if (step == "forget") f.Windows.Add(added); };
        f.Owner.HandleClosed(f.Window);
        Assert.Same(added, Assert.Single(f.Windows)); Assert.DoesNotContain("store", f.Ops.Trace);
    }

    [Fact]
    public void SaveReentryMayReplaceMembershipWithoutRemovingNewWindow()
    {
        var f = new Fixture(); var added = new TestWindow();
        f.Ops.OnStep = step => { if (step == "save") { f.Windows.Clear(); f.Windows.Add(added); } };
        f.Owner.HandleClosed(f.Window);
        Assert.Same(added, Assert.Single(f.Windows)); Assert.Contains("forget", f.Ops.Trace); Assert.DoesNotContain("store", f.Ops.Trace);
    }

    [Fact]
    public void StoreCallbackChangingListDoesNotAddNewCountRevalidation()
    {
        var f = new Fixture(); f.Ops.StoreWorking = true;
        f.Ops.OnStep = step => { if (step == "store") f.Windows.Add(new()); };
        f.Owner.HandleClosed(f.Window); Assert.Single(f.Windows); Assert.Equal("prepare", f.Ops.Trace.Last());
    }

    [Theory]
    [InlineData("save")]
    [InlineData("release")]
    public void RecoverableSaveOrModalFailureWarnsThenContinuesCleanup(string failing)
    {
        var f = new Fixture(); var error = new IOException(failing);
        f.Ops.OnStep = step => { if (step == failing) throw error; };
        f.Owner.HandleClosed(f.Window);
        Assert.Same(error, Assert.Single(f.Ops.Warned)); Assert.Empty(f.Windows);
        Assert.Equal(["forget", "store", "enqueue"], f.Ops.Trace.TakeLast(3));
    }

    [Fact]
    public void ListRemoveFailureWarnsAndForgetsWithoutPretendingRemovalSucceeded()
    {
        var f = new Fixture(); var error = new IOException("equals"); f.Window.EqualsError = error;
        f.Owner.HandleClosed(f.Window);
        Assert.Same(error, Assert.Single(f.Ops.Warned)); Assert.Same(f.Window, Assert.Single(f.Windows));
        Assert.Equal(["warn", "forget", "enqueue"], f.Ops.Trace.TakeLast(3)); Assert.DoesNotContain("store", f.Ops.Trace);
    }

    [Fact]
    public void WarningFailureInSaveCatchStillExecutesOuterFinallyButSkipsPostCleanupDispatch()
    {
        var f = new Fixture(); var primary = new IOException("save"); var logger = new IOException("logger");
        f.Ops.OnStep = step => { if (step == "save") throw primary; if (step == "warn") throw logger; };
        Assert.Same(logger, Assert.Throws<IOException>(() => f.Owner.HandleClosed(f.Window)));
        Assert.Empty(f.Windows); Assert.Equal(["detach", "release", "forget"], f.Ops.Trace.TakeLast(3));
        Assert.DoesNotContain("enqueue", f.Ops.Trace);
    }

    [Fact]
    public void WarningFailureInsideModalCleanupStopsRemainingFinally()
    {
        var f = new Fixture(); var primary = new IOException("release"); var logger = new IOException("logger");
        f.Ops.OnStep = step => { if (step == "release") throw primary; if (step == "warn") throw logger; };
        Assert.Same(logger, Assert.Throws<IOException>(() => f.Owner.HandleClosed(f.Window)));
        Assert.Same(f.Window, Assert.Single(f.Windows)); Assert.Equal("warn", f.Ops.Trace.Last());
        Assert.DoesNotContain("forget", f.Ops.Trace);
    }

    [Theory]
    [InlineData("capture", 1)]
    [InlineData("migration", 1)]
    [InlineData("detach", 1)]
    [InlineData("complete", 0)]
    [InlineData("forget", 0)]
    [InlineData("store", 0)]
    [InlineData("prepare", 0)]
    [InlineData("enqueue", 0)]
    [InlineData("exit", 0)]
    public void UncaughtOperationFailureKeepsExactCleanupStage(string failing, int remaining)
    {
        var f = new Fixture(); var error = new IOException(failing);
        f.Ops.Migration = failing == "complete"; f.Ops.StoreWorking = failing == "prepare";
        if (failing == "exit") f.Ops.Queue = () => false;
        f.Ops.OnStep = step => { if (step == failing) throw error; };
        Assert.Same(error, Assert.Throws<IOException>(() => f.Owner.HandleClosed(f.Window)));
        Assert.Equal(remaining, f.Windows.Count); Assert.Equal(failing, f.Ops.Trace.Last()); Assert.Empty(f.Ops.Warned);
    }

    [Fact]
    public void MigrationCompletionPrecedesForgetAndDispatchEvenAfterRecoverablePositionFailure()
    {
        var f = new Fixture(); f.Ops.Migration = true;
        f.Ops.OnStep = step => { if (step == "save") throw new IOException("save"); };
        f.Owner.HandleClosed(f.Window);
        Assert.Equal(["complete", "forget", "enqueue"], f.Ops.Trace.TakeLast(3)); Assert.Single(f.Ops.Warned);
    }

    [Fact]
    public async Task PendingPrepareTaskIsNotSynchronouslyJoined()
    {
        var f = new Fixture(); f.Ops.StoreWorking = true;
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); f.Ops.PrepareTask = gate.Task;
        Task worker = Task.Run(() => f.Owner.HandleClosed(f.Window));
        try { await worker.WaitAsync(TimeSpan.FromSeconds(2)); Assert.False(gate.Task.IsCompleted); Assert.Equal("prepare", f.Ops.Trace.Last()); }
        finally { gate.TrySetResult(true); await worker.WaitAsync(TimeSpan.FromSeconds(2)); }
    }

    [Fact]
    public void FaultedPrepareTaskRemainsUnawaitedAndNoExtraCatchLogsIt()
    {
        var f = new Fixture(); f.Ops.StoreWorking = true; var error = new IOException("async prepare");
        Task failed = Task.FromException(error); f.Ops.PrepareTask = failed;
        f.Owner.HandleClosed(f.Window); Assert.Empty(f.Ops.Warned);
        Assert.Same(error, Assert.Single(failed.Exception!.InnerExceptions));
    }

    [Fact]
    public void ReentrantCloseDuringSaveKeepsIndependentOuterContinuation()
    {
        var f = new Fixture(); var nested = new TestWindow(); f.Windows.Add(nested); bool entered = false;
        f.Ops.OnStep = step =>
        {
            if (step == "save" && !entered) { entered = true; f.Owner.HandleClosed(nested); }
        };
        f.Owner.HandleClosed(f.Window);
        Assert.Empty(f.Windows); Assert.Equal(2, f.Ops.Trace.Count(x => x == "capture"));
        Assert.Equal(2, f.Ops.Trace.Count(x => x == "enqueue")); Assert.Equal(1, f.Ops.Trace.Count(x => x == "store"));
    }

    private sealed class TestWindow
    {
        public Exception? EqualsError;
        public override bool Equals(object? other) => EqualsError is null ? ReferenceEquals(this, other) : throw EqualsError;
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
    }
    private sealed class Fixture
    {
        public Fixture() { Windows.Add(Window); Owner = new(Windows, Ops); }
        public TestWindow Window { get; } = new();
        public List<TestWindow> Windows { get; } = [];
        public Operations Ops { get; } = new();
        public AppWindowCloseCoordinator<TestWindow> Owner { get; }
    }
    private sealed class Operations : IAppWindowCloseOperations<TestWindow>
    {
        public List<string> Trace { get; } = [];
        public List<Exception> Warned { get; } = [];
        public Action<string>? OnStep;
        public bool Migration, StoreWorking;
        public Func<bool> Queue = () => true;
        public Task PrepareTask = Task.CompletedTask;
        private void Step(string step) { Trace.Add(step); OnStep?.Invoke(step); }
        public Func<bool> CaptureExitEnqueue(TestWindow window)
        {
            Step("capture"); var queue = Queue;
            return () => { Step("enqueue"); return queue(); };
        }
        public bool IsMigrationWindow(TestWindow window) { Step("migration"); return Migration; }
        public void SavePosition(TestWindow window) => Step("save");
        public void Detach(TestWindow window) => Step("detach");
        public void ReleaseModal(TestWindow window) => Step("release");
        public void Warn(Exception error) { Warned.Add(error); Step("warn"); }
        public void CompleteMigration() => Step("complete");
        public void ForgetOwner(TestWindow window) => Step("forget");
        public bool IsStoreWorking() { Step("store"); return StoreWorking; }
        public Task CreatePrepareAsync() { Step("prepare"); return PrepareTask; }
        public void ExitIfIdle() => Step("exit");
    }
}
