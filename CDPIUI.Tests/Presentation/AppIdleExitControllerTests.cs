using CDPIUI.Helper.Basic;

namespace CDPIUI.Tests.Presentation;

// Call traces pin short circuits and callback boundaries; the delegates represent native endpoints.
public class AppIdleExitControllerTests
{
    [Fact]
    public void ConstructionDoesNotReadStateOrInvokeNativeEndpoints()
    {
        var f = new Fixture();
        Assert.Empty(f.Calls);
    }

    [Theory]
    [InlineData(0, true, false, false, "migration,count")]
    [InlineData(1, true, false, false, "migration,count,count")]
    [InlineData(1, false, false, false, "migration,count")]
    [InlineData(0, false, true, false, "migration,count,store")]
    [InlineData(0, false, false, true, "migration,count,store,good")]
    [InlineData(0, false, false, false, "migration,count,store,good,exit")]
    public void ExitStopsAtFirstActiveCondition(int windows, bool migration, bool store, bool good, string trace)
    {
        var f = new Fixture { Windows = windows, Migration = migration, Store = store, Good = good };
        f.Owner.ExitIfIdle();
        Assert.Equal(trace.Split(','), f.Calls);
        Assert.Empty(f.Warnings);
    }

    [Fact]
    public void PendingMigrationWithNonzeroFirstCountReadsCountAgainInsteadOfTakingSnapshot()
    {
        var f = new Fixture { Migration = true };
        int reads = 0;
        f.ReadCount = () => ++reads == 1 ? 1 : 0;
        f.Owner.ExitIfIdle();
        Assert.Equal(["migration", "count", "count", "store", "good", "exit"], f.Calls);
    }

    [Fact]
    public void MigrationChangedWhileReadingCountIsNotReadAgainInSameExit()
    {
        var f = new Fixture();
        f.ReadCount = () => { f.Migration = true; return 0; };
        f.Owner.ExitIfIdle();
        Assert.Equal(["migration", "count", "store", "good", "exit"], f.Calls);
    }

    [Theory]
    [InlineData(1, false, "count")]
    [InlineData(0, true, "count,migration")]
    [InlineData(0, false, "count,migration,create")]
    public void WorkingCreatesPrepareOnlyForNoWindowsAndNoMigration(int windows, bool migration, string trace)
    {
        var f = new Fixture { Windows = windows, Migration = migration };
        f.Owner.HandleStoreStateChanged(true);
        Assert.Equal(trace.Split(','), f.Calls);
        Assert.Empty(f.Warnings);
    }

    [Fact]
    public void WorkingGuardReadsMigrationAfterWindowCountCallback()
    {
        var f = new Fixture();
        f.ReadCount = () => { f.Migration = true; return 0; };
        f.Owner.HandleStoreStateChanged(true);
        Assert.Equal(["count", "migration"], f.Calls);
    }

    [Fact]
    public void StoppedClosesPrepareBeforeReadingAnyIdleCondition()
    {
        var f = new Fixture { Windows = 1, Migration = true, Store = true, Good = true };
        f.OnStep = step =>
        {
            if (step == "close") { f.Windows = 0; f.Migration = false; f.Store = false; f.Good = false; }
        };
        f.Owner.HandleStoreStateChanged(false);
        Assert.Equal(["close", "migration", "count", "store", "good", "exit"], f.Calls);
    }

    [Fact]
    public void StoppedStillClosesPrepareWhenMigrationBecamePending()
    {
        var f = new Fixture();
        f.OnStep = step => { if (step == "close") f.Migration = true; };
        f.Owner.HandleStoreStateChanged(false);
        Assert.Equal(["close", "migration", "count"], f.Calls);
    }

    [Fact]
    public void StoppedRereadsWindowAddedByCloseCallbackAndDoesNotExit()
    {
        var f = new Fixture();
        f.OnStep = step => { if (step == "close") f.Windows++; };
        f.Owner.HandleStoreStateChanged(false);
        Assert.Equal(["close", "migration", "count"], f.Calls);
    }

    [Theory]
    [InlineData("migration", "migration")]
    [InlineData("count", "migration,count")]
    [InlineData("store", "migration,count,store")]
    [InlineData("good", "migration,count,store,good")]
    [InlineData("exit", "migration,count,store,good,exit")]
    public void DirectExitDoesNotCatchPortExceptions(string failing, string trace)
    {
        var f = new Fixture(); var error = new IOException(failing);
        f.OnStep = step => { if (step == failing) throw error; };
        Assert.Same(error, Assert.Throws<IOException>(() => f.Owner.ExitIfIdle()));
        Assert.Equal(trace.Split(','), f.Calls);
        Assert.Empty(f.Warnings);
    }

    [Theory]
    [InlineData("count", "count,warn,migration,count,store,good,exit")]
    [InlineData("migration", "count,migration,warn,migration,count,store,good,exit")]
    [InlineData("create", "count,migration,create,warn,migration,count,store,good,exit")]
    public void WorkingSynchronousFailureWarnsWithSameExceptionThenChecksCurrentIdleState(string failing, string trace)
    {
        var f = new Fixture(); var error = new IOException(failing); bool thrown = false;
        f.OnStep = step => { if (step == failing && !thrown) { thrown = true; throw error; } };
        f.Owner.HandleStoreStateChanged(true);
        Assert.Same(error, Assert.Single(f.Warnings));
        Assert.Equal(trace.Split(','), f.Calls);
    }

    [Fact]
    public void CloseFailureWarnsOnceThenChecksIdleWithoutRetryingClose()
    {
        var f = new Fixture(); var error = new IOException("close");
        f.OnStep = step => { if (step == "close") throw error; };
        f.Owner.HandleStoreStateChanged(false);
        Assert.Same(error, Assert.Single(f.Warnings));
        Assert.Equal(["close", "warn", "migration", "count", "store", "good", "exit"], f.Calls);
    }

    [Theory]
    [InlineData("migration", "close,migration,warn,migration,count,store,good,exit")]
    [InlineData("count", "close,migration,count,warn,migration,count,store,good,exit")]
    [InlineData("store", "close,migration,count,store,warn,migration,count,store,good,exit")]
    [InlineData("good", "close,migration,count,store,good,warn,migration,count,store,good,exit")]
    [InlineData("exit", "close,migration,count,store,good,exit,warn,migration,count,store,good,exit")]
    public void StoppedIdleFailureIsInsideStoreCatchAndGetsOneRetry(string failing, string trace)
    {
        var f = new Fixture(); var error = new IOException(failing); bool thrown = false;
        f.OnStep = step => { if (step == failing && !thrown) { thrown = true; throw error; } };
        f.Owner.HandleStoreStateChanged(false);
        Assert.Same(error, Assert.Single(f.Warnings));
        Assert.Equal(trace.Split(','), f.Calls);
    }

    [Fact]
    public void WarningCanChangeConditionsBeforeFallbackReadsThem()
    {
        var f = new Fixture(); var error = new IOException("close");
        f.OnStep = step => { if (step == "close") throw error; if (step == "warn") f.Windows = 1; };
        f.Owner.HandleStoreStateChanged(false);
        Assert.Same(error, Assert.Single(f.Warnings));
        Assert.Equal(["close", "warn", "migration", "count"], f.Calls);
    }

    [Fact]
    public void WarningFailureEscapesWithoutFallback()
    {
        var f = new Fixture(); var primary = new IOException("close"); var warning = new IOException("warn");
        f.OnStep = step => { if (step == "close") throw primary; if (step == "warn") throw warning; };
        Assert.Same(warning, Assert.Throws<IOException>(() => f.Owner.HandleStoreStateChanged(false)));
        Assert.Same(primary, Assert.Single(f.Warnings));
        Assert.Equal(["close", "warn"], f.Calls);
    }

    [Fact]
    public void FallbackFailureEscapesWithoutSecondWarningOrRetry()
    {
        var f = new Fixture(); var primary = new IOException("close"); var fallback = new IOException("store");
        f.OnStep = step => { if (step == "close") throw primary; if (step == "store") throw fallback; };
        Assert.Same(fallback, Assert.Throws<IOException>(() => f.Owner.HandleStoreStateChanged(false)));
        Assert.Same(primary, Assert.Single(f.Warnings));
        Assert.Equal(["close", "warn", "migration", "count", "store"], f.Calls);
    }

    [Fact]
    public void SecondIdleFailureEscapesRatherThanRecursivelyRetrying()
    {
        var f = new Fixture(); var error = new IOException("store");
        f.OnStep = step => { if (step == "store") throw error; };
        Assert.Same(error, Assert.Throws<IOException>(() => f.Owner.HandleStoreStateChanged(false)));
        Assert.Same(error, Assert.Single(f.Warnings));
        Assert.Equal(["close", "migration", "count", "store", "warn", "migration", "count", "store"], f.Calls);
    }

    [Fact]
    public async Task PendingCreateIsStartedWithoutWaitingForCompletion()
    {
        var f = new Fixture(); var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Create = () => completion.Task;
        Task call = Task.Run(() => f.Owner.HandleStoreStateChanged(true));
        try
        {
            await call.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.False(completion.Task.IsCompleted);
            Assert.Equal(["count", "migration", "create"], f.Calls);
            Assert.Empty(f.Warnings);
        }
        finally { completion.TrySetResult(); await call.WaitAsync(TimeSpan.FromSeconds(2)); }
    }

    [Fact]
    public void AlreadyFaultedCreateTaskDoesNotEnterSynchronousCatch()
    {
        var f = new Fixture(); var error = new IOException("async create"); Task failed = Task.FromException(error);
        f.Create = () => failed;
        f.Owner.HandleStoreStateChanged(true);
        Assert.Equal(["count", "migration", "create"], f.Calls);
        Assert.Empty(f.Warnings);
        Assert.Same(error, Assert.Single(failed.Exception!.InnerExceptions));
    }

    [Fact]
    public void CloseCallbackCanReenterWorkingEventBeforeOuterIdleCheck()
    {
        var f = new Fixture();
        f.OnStep = step => { if (step == "close") f.Owner.HandleStoreStateChanged(true); };
        f.Owner.HandleStoreStateChanged(false);
        Assert.Equal(["close", "count", "migration", "create", "migration", "count", "store", "good", "exit"], f.Calls);
    }

    [Fact]
    public void CreateCallbackCanReenterStoppedEventWithoutDeduplication()
    {
        var f = new Fixture();
        f.Create = () => { f.Owner.HandleStoreStateChanged(false); return Task.CompletedTask; };
        f.Owner.HandleStoreStateChanged(true);
        Assert.Equal(["count", "migration", "create", "close", "migration", "count", "store", "good", "exit"], f.Calls);
    }

    [Fact]
    public void LaterCallsReadAllConditionsAgainRatherThanCachingPriorWork()
    {
        var f = new Fixture { Store = true };
        f.Owner.ExitIfIdle();
        f.Store = false; f.Calls.Clear();
        f.Owner.ExitIfIdle();
        Assert.Equal(["migration", "count", "store", "good", "exit"], f.Calls);
    }

    private sealed class Fixture
    {
        public readonly List<string> Calls = [];
        public readonly List<Exception> Warnings = [];
        public int Windows;
        public bool Migration, Store, Good;
        public Action<string>? OnStep;
        public Func<int>? ReadCount;
        public Func<Task> Create = () => Task.CompletedTask;
        public readonly AppIdleExitController Owner;

        public Fixture()
        {
            Owner = new(
                () => { Step("count"); return ReadCount?.Invoke() ?? Windows; },
                () => { Step("migration"); return Migration; },
                () => { Step("store"); return Store; },
                () => { Step("good"); return Good; },
                () => { Step("create"); return Create(); },
                () => Step("close"), () => Step("exit"),
                error => { Warnings.Add(error); Step("warn"); });
        }

        private void Step(string step) { Calls.Add(step); OnStep?.Invoke(step); }
    }
}
