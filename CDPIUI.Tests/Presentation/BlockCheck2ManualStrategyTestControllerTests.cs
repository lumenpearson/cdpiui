using System.Collections.Concurrent;
using CDPIUI.AddOns.BlockCheck2.Execution;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.Shared.Models;
using CDPIUI.ViewModels;

namespace CDPIUI.Tests.Presentation;

public sealed class BlockCheck2ManualStrategyTestControllerTests
{
    [Fact]
    public void ConstructorOnlyStoresDependencies()
    {
        var f = new Fixture();
        Assert.Equal(0, f.SessionReads);
        Assert.Empty(f.Operations.Arguments);
        Assert.Empty(f.Operations.Tests);
        Assert.Empty(f.Rebuilds);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task MissingSessionReturnsFreshIssueBeforeReadingItemOrCancellation(bool nullItem, bool canceled)
    {
        var f = new Fixture { Session = null };
        using var cts = new CancellationTokenSource();
        if (canceled) cts.Cancel();
        var item = nullItem ? null! : Item();
        var first = await f.Controller.TestStrategyAsync(item, cts.Token);
        var second = await f.Controller.TestStrategyAsync(item, cts.Token);
        Assert.NotSame(first, second);
        Assert.NotSame(first.Issues, second.Issues);
        var issue = Assert.Single(first.Issues);
        Assert.Equal(BlockCheckIssueSeverity.Error, issue.Severity);
        Assert.Equal("MANUAL_SESSION_UNAVAILABLE", issue.Code);
        Assert.Equal("The original BlockCheck session is unavailable; this report cannot launch a live strategy test.", issue.Message);
        Assert.Null(first.ProbeResult);
        Assert.False(first.Success);
        Assert.Empty(f.Operations.Tests);
        Assert.Empty(f.ManualTests);
        Assert.Empty(f.Rebuilds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SyncArgumentsReadsItemBeforeSessionAndDoesNotAddANoSessionGuard(bool nullItem)
    {
        var f = new Fixture { Session = null };
        Assert.Throws<NullReferenceException>(() => f.Controller.GetFullStrategyArguments(nullItem ? null! : Item()));
        Assert.Equal(nullItem ? 0 : 1, f.SessionReads);
        Assert.Empty(f.Operations.Arguments);
    }

    [Theory]
    [InlineData(-5, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    public void ArgumentsReturnsSameBackendResultWithFreshClampedOptions(int attempts, int expected)
    {
        var f = new Fixture { Session = Session(attempts) };
        var item = Item();
        var result = OperationResultModel<string>.SuccessResult("--fixture");
        f.Operations.GetArguments = _ => result;
        Assert.Same(result, f.Controller.GetFullStrategyArguments(item));
        Assert.Same(result, f.Controller.GetFullStrategyArguments(item));
        var one = f.Operations.Arguments[0]; var two = f.Operations.Arguments[1];
        Assert.Same(item.Evidence.Strategy, one.Strategy);
        Assert.Same(item.Evidence.Target, one.Target);
        Assert.Equal(expected, one.Options.Attempts);
        Assert.NotSame(one.Options, two.Options);
        Assert.NotSame(one.Options.WriterOptions, two.Options.WriterOptions);
        Assert.Empty(f.ManualTests);
        Assert.Empty(f.Rebuilds);
    }

    [Fact]
    public void ArgumentsBackendFailureEscapesSynchronously()
    {
        var f = new Fixture(); var failure = new IOException("arguments");
        f.Operations.GetArguments = _ => throw failure;
        Assert.Same(failure, Assert.Throws<IOException>(() => f.Controller.GetFullStrategyArguments(Item())));
        Assert.Single(f.Operations.Arguments);
        Assert.Empty(f.Rebuilds);
    }

    [Theory]
    [InlineData(-5, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    public async Task TestForwardsOriginalIdentitiesTimeoutsAndTokenBeforeAwait(int attempts, int expected)
    {
        var f = new Fixture { Session = Session(attempts) }; var item = Item();
        using var cts = new CancellationTokenSource();
        var pending = Pending(); f.Operations.Test = _ => pending.Task;
        var task = f.Controller.TestStrategyAsync(item, cts.Token);
        var call = Assert.Single(f.Operations.Tests);
        Assert.Same(item.Evidence.Strategy, call.Strategy);
        Assert.Same(item.Evidence.Target, call.Target);
        Assert.Equal(expected, call.Options.Attempts);
        Assert.Equal(TimeSpan.FromMilliseconds(123), call.StrategyOptions.StartupGracePeriod);
        Assert.Equal(TimeSpan.FromSeconds(4), call.ProbeOptions.ConnectTimeout);
        Assert.Equal(TimeSpan.FromSeconds(9), call.ProbeOptions.RequestTimeout);
        Assert.Equal(cts.Token, call.Token);
        Assert.False(task.IsCompleted);
        Assert.Empty(f.ManualTests);
        Assert.Equal(BlockCheckEvidenceStatus.Failed, item.Status);
        Assert.Empty(f.Rebuilds);
        var result = new BlockCheckManualStrategyTestResult { CommandLine = "no probe" };
        pending.SetResult(result);
        Assert.Same(result, await task);
        Assert.Empty(f.ManualTests);
        Assert.Equal(BlockCheckEvidenceStatus.Failed, item.Status);
        Assert.Empty(f.Rebuilds);
    }

    [Fact]
    public async Task EachTestCreatesFreshOptionObjectsAndReadsTheCurrentSession()
    {
        var f = new Fixture(); var item = Item();
        await f.Controller.TestStrategyAsync(item);
        f.Session = Session(11);
        await f.Controller.TestStrategyAsync(item);
        var one = f.Operations.Tests[0]; var two = f.Operations.Tests[1];
        Assert.Equal(3, one.Options.Attempts);
        Assert.Equal(11, two.Options.Attempts);
        Assert.NotSame(one.Options, two.Options);
        Assert.NotSame(one.Options.WriterOptions, two.Options.WriterOptions);
        Assert.NotSame(one.StrategyOptions, two.StrategyOptions);
        Assert.NotSame(one.ProbeOptions, two.ProbeOptions);
    }

    [Fact]
    public async Task NullItemWithSessionFailsInTaskBeforeBackend()
    {
        var f = new Fixture();
        var task = f.Controller.TestStrategyAsync(null!);
        await Assert.ThrowsAsync<NullReferenceException>(() => task);
        Assert.Empty(f.Operations.Tests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BackendSyncAndAsyncFaultsPropagateWithoutStateChanges(bool sync)
    {
        var f = new Fixture(); var item = Item(); var failure = new IOException("backend");
        f.Operations.Test = _ => sync ? throw failure : Task.FromException<BlockCheckManualStrategyTestResult>(failure);
        var task = f.Controller.TestStrategyAsync(item);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => task));
        Assert.Empty(f.ManualTests);
        Assert.Empty(f.Rebuilds);
        Assert.Equal(BlockCheckEvidenceStatus.Failed, item.Status);
    }

    [Fact]
    public async Task BackendCancellationRemainsCanceledWithoutApplyingAnything()
    {
        var f = new Fixture(); var item = Item();
        using var cts = new CancellationTokenSource(); cts.Cancel();
        f.Operations.Test = _ => Task.FromCanceled<BlockCheckManualStrategyTestResult>(cts.Token);
        var task = f.Controller.TestStrategyAsync(item, cts.Token);
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(cts.Token, exception.CancellationToken);
        Assert.True(task.IsCanceled);
        Assert.Empty(f.ManualTests);
        Assert.Empty(f.Rebuilds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallerCancellationDoesNotOverrideAResultReturnedByBackend(bool beforeCall)
    {
        var f = new Fixture(); var item = Item(); var pending = Pending();
        using var cts = new CancellationTokenSource();
        if (beforeCall) cts.Cancel();
        f.Operations.Test = _ => pending.Task;
        var task = f.Controller.TestStrategyAsync(item, cts.Token);
        cts.Cancel(); var result = Result(Probe());
        pending.SetResult(result);
        Assert.Same(result, await task);
        Assert.Same(result.ProbeResult, Assert.Single(f.ManualTests));
        Assert.Equal(BlockCheckEvidenceStatus.Successful, item.Status);
        Assert.Equal(new[] { false }, f.Rebuilds);
    }

    [Fact]
    public async Task OverlayRemovesAllMatchingPairsOnlyAndAppendsTheExactReturnedProbe()
    {
        var f = new Fixture(); var item = Item();
        var otherTarget = Probe("strategy", "TARGET"); var otherStrategy = Probe("other", "target");
        f.ManualTests.AddRange([Probe("STRATEGY"), otherTarget, Probe(), otherStrategy, Probe("Strategy")]);
        var probe = Probe(); var result = Result(probe);
        f.Operations.Test = _ => Task.FromResult(result);
        Assert.Same(result, await f.Controller.TestStrategyAsync(item));
        Assert.Equal(new[] { otherTarget, otherStrategy, probe }, f.ManualTests);
        Assert.Same(probe, f.ManualTests[2]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResultNotificationsPrecedeRebuildAndDraftFlagIsReadAfterObservers(bool becomeManual)
    {
        var f = new Fixture(); var item = Item(); var probe = Probe();
        if (!becomeManual) f.Draft.ApplyStructuredChange("--manual", []);
        var trace = new List<string>();
        item.PropertyChanged += (_, args) =>
        {
            trace.Add(args.PropertyName!);
            if (args.PropertyName != nameof(item.Status)) return;
            Assert.Same(probe, Assert.Single(f.ManualTests));
            Assert.Equal(BlockCheckEvidenceStatus.Successful, item.Status);
            Assert.Equal(1, item.AttemptCount); Assert.Equal(1, item.SuccessCount);
            if (becomeManual) f.Draft.ApplyStructuredChange("--manual", []); else f.Draft.LoadAutomatic("--automatic", []);
        };
        f.OnRebuild = _ => trace.Add("rebuild");
        f.Operations.Test = _ => Task.FromResult(Result(probe));
        await f.Controller.TestStrategyAsync(item);
        Assert.Equal(becomeManual, Assert.Single(f.Rebuilds));
        Assert.Equal(new[] { "Status", "AttemptCount", "SuccessCount", "SuccessRate", "ResponseTimeSortValue", "ResultText",
            "HasPartialScopeWarning", "ResponseTimeText", "HttpStatusText", "FailureText", "EvidenceDetails", "rebuild" }, trace);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ALoadWhilePendingStillAppliesToSameLiveListAndStaleItem(bool clearSession)
    {
        var f = new Fixture(); var staleItem = Item(); var pending = Pending();
        var old = Probe("old"); f.ManualTests.Add(old);
        f.Operations.Test = _ => pending.Task;
        var task = f.Controller.TestStrategyAsync(staleItem);
        var currentSession = clearSession ? null : Session(12);
        f.Session = currentSession; f.ManualTests.Clear();
        var currentProbe = Probe("current"); f.ManualTests.Add(currentProbe);
        f.Draft.ApplyStructuredChange("--new draft", []);
        f.OnRebuild = update => { Assert.Same(currentSession, f.Session); Assert.True(update); };
        var result = Result(Probe());
        pending.SetResult(result);
        Assert.Same(result, await task);
        Assert.Equal(new[] { currentProbe, result.ProbeResult! }, f.ManualTests);
        Assert.Equal(BlockCheckEvidenceStatus.Successful, staleItem.Status);
        Assert.True(Assert.Single(f.Rebuilds));
    }

    [Fact]
    public async Task OverlappingTestsApplyByCompletionOrderWithoutSerialization()
    {
        var f = new Fixture(); var item = Item(); var one = Pending(); var two = Pending();
        f.Operations.Test = _ => f.Operations.Tests.Count == 1 ? one.Task : two.Task;
        var first = f.Controller.TestStrategyAsync(item); var second = f.Controller.TestStrategyAsync(item);
        Assert.Equal(2, f.Operations.Tests.Count);
        var secondResult = Result(Probe()); two.SetResult(secondResult);
        Assert.Same(secondResult, await second);
        Assert.False(first.IsCompleted);
        var firstResult = Result(Probe(success: false)); one.SetResult(firstResult);
        Assert.Same(firstResult, await first);
        Assert.Same(firstResult.ProbeResult, Assert.Single(f.ManualTests));
        Assert.Equal(BlockCheckEvidenceStatus.Failed, item.Status);
        Assert.Equal(2, f.Rebuilds.Count);
    }

    [Theory]
    [InlineData(0, 0, BlockCheckEvidenceStatus.Untested)]
    [InlineData(3, 0, BlockCheckEvidenceStatus.Failed)]
    [InlineData(3, 1, BlockCheckEvidenceStatus.Partial)]
    [InlineData(3, 3, BlockCheckEvidenceStatus.Successful)]
    public async Task EveryNonNullProbeReachesActualItemEvenWhenNoAttemptsSucceeded(int attempts, int successes, BlockCheckEvidenceStatus status)
    {
        var f = new Fixture(); var item = Item();
        var probe = new ProbeResult
        {
            StrategyId = "strategy", TargetId = "target",
            Attempts = Enumerable.Range(0, attempts).Select(i => new ProbeAttempt
            {
                Success = i < successes, TimeToFirstByteMs = 25, HttpStatusCode = i < successes ? 200 : 0,
                FailureCode = i < successes ? "" : "FAIL",
            }).ToList(),
        };
        f.Operations.Test = _ => Task.FromResult(Result(probe));
        await f.Controller.TestStrategyAsync(item);
        Assert.Equal(status, item.Status);
        Assert.Equal(attempts, item.AttemptCount);
        Assert.Equal(successes, item.SuccessCount);
        Assert.Equal(successes > 0 ? "200" : "", item.HttpStatusText);
        Assert.Equal(attempts > successes ? "FAIL" : "", item.FailureText);
        Assert.Same(probe, Assert.Single(f.ManualTests));
        Assert.Single(f.Rebuilds);
    }

    [Fact]
    public async Task ReturnedPairOwnsReplacementAndItemObserverMayEditLiveListBeforeRebuild()
    {
        var f = new Fixture(); var item = Item();
        var originalItemPair = Probe(); var staleReturnedPair = Probe("DIFFERENT", "other-target");
        f.ManualTests.AddRange([originalItemPair, staleReturnedPair]);
        var returned = Probe("different", "other-target");
        var observerProbe = Probe("observer");
        item.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(item.Status)) return;
            Assert.Equal(new[] { originalItemPair, returned }, f.ManualTests);
            f.ManualTests.Clear(); f.ManualTests.Add(observerProbe);
        };
        f.OnRebuild = _ => Assert.Same(observerProbe, Assert.Single(f.ManualTests));
        f.Operations.Test = _ => Task.FromResult(Result(returned));
        await f.Controller.TestStrategyAsync(item);
        Assert.Equal(BlockCheckEvidenceStatus.Successful, item.Status);
        Assert.Same(observerProbe, Assert.Single(f.ManualTests));
        Assert.Single(f.Rebuilds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallbackFailuresRetainAlreadyAppliedListAndItemMutations(bool inRebuild)
    {
        var f = new Fixture(); var item = Item(); var result = Result(Probe());
        var failure = new IOException("callback"); var notifications = new List<string>();
        item.PropertyChanged += (_, args) =>
        {
            notifications.Add(args.PropertyName!);
            if (!inRebuild) throw failure;
        };
        if (inRebuild) f.OnRebuild = _ => throw failure;
        f.Operations.Test = _ => Task.FromResult(result);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => f.Controller.TestStrategyAsync(item)));
        Assert.Same(result.ProbeResult, Assert.Single(f.ManualTests));
        Assert.Equal(BlockCheckEvidenceStatus.Successful, item.Status);
        Assert.Equal(inRebuild ? 11 : 1, notifications.Count);
        Assert.Equal(inRebuild ? 1 : 0, f.Rebuilds.Count);
    }

    [Fact]
    public void PendingContinuationAppliesItemAndRebuildOnCapturedSynchronizationContext()
    {
        var f = new Fixture(); var item = Item(); var pending = Pending();
        f.Operations.Test = _ => pending.Task;
        var context = new PumpContext(); var previous = SynchronizationContext.Current;
        Task<BlockCheckManualStrategyTestResult> task;
        item.PropertyChanged += (_, _) => Assert.Same(context, SynchronizationContext.Current);
        f.OnRebuild = _ => Assert.Same(context, SynchronizationContext.Current);
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            task = f.Controller.TestStrategyAsync(item);
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
        var result = Result(Probe());
        pending.SetResult(result);
        Assert.True(SpinWait.SpinUntil(() => context.HasWork, TimeSpan.FromSeconds(3)));
        Assert.False(task.IsCompleted);
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            context.Pump();
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
        Assert.True(task.IsCompletedSuccessfully);
        Assert.Same(result.ProbeResult, Assert.Single(f.ManualTests));
        Assert.Single(f.Rebuilds);
    }

    private static BlockCheckResultSession Session(int attempts = 3) => new()
    {
        RunOptions = new() { Scan = new() { AttemptsPerTarget = attempts } },
        StartupGracePeriod = TimeSpan.FromMilliseconds(123), ConnectTimeout = TimeSpan.FromSeconds(4), RequestTimeout = TimeSpan.FromSeconds(9),
    };
    private static BlockCheck2StrategyEvidenceItem Item() => new(new()
    {
        Strategy = new() { Id = "strategy" },
        Target = new() { Id = "target", Host = "example.test", Protocol = BlockCheckProtocol.Tls12, IpVersion = BlockCheckIpVersion.IPv4 },
        Summary = new() { AttemptCount = 1 }, Status = BlockCheckEvidenceStatus.Failed,
    });
    private static ProbeResult Probe(string strategy = "strategy", string target = "target", bool success = true) => new()
    {
        StrategyId = strategy, TargetId = target,
        Attempts = [new() { Success = success, TimeToFirstByteMs = 23, HttpStatusCode = success ? 200 : 0, FailureCode = success ? "" : "failed" }],
    };
    private static BlockCheckManualStrategyTestResult Result(ProbeResult probe) => new() { ProbeResult = probe, CommandLine = "--test" };
    private static TaskCompletionSource<BlockCheckManualStrategyTestResult> Pending() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private sealed record ArgumentCall(StrategyDefinition Strategy, BlockCheckTarget Target, BlockCheckManualStrategyTestOptions Options);
    private sealed record TestCall(StrategyDefinition Strategy, BlockCheckTarget Target, BlockCheckManualStrategyTestOptions Options,
        CdpiuiZapret2StrategyRunnerOptions StrategyOptions, CurlBlockCheckProbeRunnerOptions ProbeOptions, CancellationToken Token);
    private sealed class Operations : IBlockCheck2ManualStrategyTestOperations
    {
        public List<ArgumentCall> Arguments { get; } = [];
        public List<TestCall> Tests { get; } = [];
        public Func<ArgumentCall, OperationResultModel<string>> GetArguments = _ => OperationResultModel<string>.SuccessResult("--test");
        public Func<TestCall, Task<BlockCheckManualStrategyTestResult>> Test = _ => Task.FromResult(new BlockCheckManualStrategyTestResult());
        public OperationResultModel<string> GetFullStrategyArguments(StrategyDefinition strategy, BlockCheckTarget target, BlockCheckManualStrategyTestOptions options)
        {
            var call = new ArgumentCall(strategy, target, options); Arguments.Add(call); return GetArguments(call);
        }
        public Task<BlockCheckManualStrategyTestResult> TestAsync(StrategyDefinition strategy, BlockCheckTarget target, BlockCheckManualStrategyTestOptions options,
            CdpiuiZapret2StrategyRunnerOptions strategyOptions, CurlBlockCheckProbeRunnerOptions probeOptions, CancellationToken cancellationToken)
        {
            var call = new TestCall(strategy, target, options, strategyOptions, probeOptions, cancellationToken); Tests.Add(call); return Test(call);
        }
    }
    private sealed class Fixture
    {
        public BlockCheckResultSession? Session = BlockCheck2ManualStrategyTestControllerTests.Session();
        public int SessionReads;
        public BlockCheck2PresetDraft Draft { get; } = new();
        public List<ProbeResult> ManualTests { get; } = [];
        public List<bool> Rebuilds { get; } = [];
        public Action<bool>? OnRebuild;
        public Operations Operations { get; } = new();
        public BlockCheck2ManualStrategyTestController Controller { get; }
        public Fixture()
        {
            Draft.LoadAutomatic("--automatic", []);
            Controller = new(Draft, ManualTests, () => { SessionReads++; return Session; },
                update => { Rebuilds.Add(update); OnRebuild?.Invoke(update); }, Operations);
        }
    }
    private sealed class PumpContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _work = new();
        public bool HasWork => !_work.IsEmpty;
        public override void Post(SendOrPostCallback d, object? state) => _work.Enqueue((d, state));
        public void Pump() { while (_work.TryDequeue(out var work)) work.Callback(work.State); }
    }
}
