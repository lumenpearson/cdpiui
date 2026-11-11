using System.Collections.Concurrent;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.AddOns.BlockCheck2.Reporting;
using CDPIUI.ViewModels;

namespace CDPIUI.Tests.Presentation;

public sealed class BlockCheck2RunControllerTests
{
    [Fact]
    public void ConstructorOnlyStoresHost()
    {
        var f = new Fixture();
        Assert.Empty(f.Host.Trace);
    }

    [Fact]
    public async Task StartGuardReturnsNullWithoutResetProgressOrFinally()
    {
        var f = new Fixture(); f.Host.AllowStart = false;
        Assert.Null(await f.Controller.StartAsync());
        Assert.Equal(new[] { "can-start" }, f.Host.Trace);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NormalRunPreservesPortOrderOriginalObjectsAndOptionalRootRenewal(bool previousReport)
    {
        var f = new Fixture(); if (previousReport) f.Host.ReportValue = new();
        var result = Success(); f.Host.Run = (_, _) => Task.FromResult(result);
        Assert.Same(result, await f.Controller.StartAsync());
        var expected = new List<string> { "can-start", "report" };
        if (previousReport) expected.Add("renew");
        expected.AddRange(["reset", "start", "prepare", "prepared", "state:Running", "run", "apply", "complete", "report", "history", "state:Completed", "finish", "cleanup", "actions"]);
        Assert.Equal(expected, f.Host.Trace);
        Assert.Same(f.Host.Prepared, f.Host.RunCalls.Single().Prepared);
        Assert.Same(f.Host.Prepared.Targets, f.Host.Targets);
        Assert.Same(f.Host.Prepared.Issues, f.Host.Issues);
        Assert.Same(f.Host.Prepared, f.Host.Applied.Single().Prepared);
        Assert.Same(result, f.Host.Applied[0].Result);
        Assert.Same(f.Host.ResultReport, f.Host.HistoryReports.Single());
        AssertDisposed(f.Host.RunCalls[0].Token);
    }

    [Fact]
    public async Task InvalidPreparationStillAppliesTargetsIssuesAndRunsFinallyWithoutBackend()
    {
        var f = new Fixture();
        f.Host.Prepared = new() { Targets = [new() { Id = "invalid" }], Issues = [Error("invalid")] };
        Assert.Null(await f.Controller.StartAsync());
        Assert.Same(f.Host.Prepared.Targets, f.Host.Targets);
        Assert.Same(f.Host.Prepared.Issues, f.Host.Issues);
        Assert.Empty(f.Host.RunCalls);
        Assert.Equal(BlockCheckSessionState.InputInvalid, f.Host.State);
        Assert.Equal(new[] { "can-start", "report", "reset", "start", "prepare", "prepared", "state:InputInvalid", "finish", "cleanup", "actions" }, f.Host.Trace);
    }

    [Theory]
    [InlineData("success", BlockCheckSessionState.Completed)]
    [InlineData("canceled", BlockCheckSessionState.Canceled)]
    [InlineData("canceled-best", BlockCheckSessionState.Canceled)]
    [InlineData("best-effort", BlockCheckSessionState.CompletedWithWarnings)]
    [InlineData("failed", BlockCheckSessionState.Failed)]
    [InlineData("no-synthesis", BlockCheckSessionState.Failed)]
    public async Task FinalStateUsesOriginalCanceledSuccessBestEffortPrecedence(string kind, BlockCheckSessionState expected)
    {
        var f = new Fixture();
        var result = kind switch
        {
            "success" => Success(),
            "canceled" => new() { WasCanceled = true, Synthesis = new() },
            "canceled-best" => new() { WasCanceled = true, Synthesis = new() { IsBestEffort = true, Issues = [Error("failure")] } },
            "best-effort" => new() { Synthesis = new() { IsBestEffort = true, Issues = [Error("failure")] } },
            "failed" => new() { Synthesis = new() { Issues = [Error("failure")] } },
            _ => new BlockCheckRunResult(),
        };
        f.Host.Run = (_, _) => Task.FromResult(result);
        Assert.Same(result, await f.Controller.StartAsync());
        Assert.Equal(expected, f.Host.State);
        Assert.Single(f.Host.HistoryReports);
    }

    [Fact]
    public async Task PendingHistoryKeepsRunBlockedAndCtsAliveAndUsesReportAfterCompleteCallback()
    {
        var f = new Fixture(); var history = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var currentReport = new BlockCheckReport();
        f.Host.OnCall = point => { if (point == "complete") f.Host.ReportValue = currentReport; };
        f.Host.History = _ => history.Task;
        var task = f.Controller.StartAsync(); var token = f.Host.RunCalls.Single().Token;
        Assert.False(task.IsCompleted);
        Assert.Equal(BlockCheckSessionState.Running, f.Host.State);
        Assert.Same(currentReport, f.Host.HistoryReports.Single());
        Assert.NotNull(token.WaitHandle);
        Assert.Null(await f.Controller.StartAsync());
        Assert.Single(f.Host.RunCalls);
        history.SetResult(); await task;
        Assert.Equal(BlockCheckSessionState.Completed, f.Host.State);
        AssertDisposed(token);
    }

    [Fact]
    public async Task FinalStateIsComputedBeforeAwaitingHistoryEvenWhenResultIssuesChange()
    {
        var f = new Fixture(); var history = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var issues = new List<BlockCheckIssue>(); var result = new BlockCheckRunResult { Synthesis = new(), PreflightIssues = issues };
        f.Host.Run = (_, _) => Task.FromResult(result); f.Host.History = _ => history.Task;
        var task = f.Controller.StartAsync();
        issues.Add(Error("late")); Assert.False(result.Success);
        history.SetResult(); Assert.Same(result, await task);
        Assert.Equal(BlockCheckSessionState.Completed, f.Host.State);
    }

    [Theory]
    [InlineData("can-start")]
    [InlineData("report")]
    [InlineData("renew")]
    [InlineData("reset")]
    [InlineData("start")]
    public async Task PreludeFailuresEscapeWithoutOperationErrorOrFinally(string point)
    {
        var f = new Fixture(); var failure = new IOException(point);
        f.Host.ReportValue = new(); f.Host.OnCall = call => { if (call == point) throw failure; };
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => f.Controller.StartAsync()));
        Assert.DoesNotContain("error", f.Host.Trace);
        Assert.DoesNotContain("finish", f.Host.Trace);
        Assert.DoesNotContain("cleanup", f.Host.Trace);
        Assert.Empty(f.Host.RunCalls);
    }

    [Theory]
    [InlineData("prepare")]
    [InlineData("prepared")]
    [InlineData("state:Running")]
    [InlineData("run")]
    [InlineData("apply")]
    [InlineData("complete")]
    [InlineData("history")]
    public async Task ExceptionsInsideTrySetErrorAndFailedThenFinishCleanupAndActions(string point)
    {
        var f = new Fixture(); var failure = new IOException(point);
        f.Host.OnCall = call => { if (call == point) throw failure; };
        Assert.Null(await f.Controller.StartAsync());
        Assert.Equal(failure.Message, f.Host.OperationError);
        Assert.Equal(BlockCheckSessionState.Failed, f.Host.State);
        Assert.Equal(new[] { "error", "state:Failed", "finish", "cleanup", "actions" }, f.Host.Trace.TakeLast(5));
        if (f.Host.RunCalls.Count > 0) AssertDisposed(f.Host.RunCalls[0].Token);
    }

    [Theory]
    [InlineData("prepare")]
    [InlineData("run")]
    [InlineData("apply")]
    [InlineData("history")]
    public async Task OperationCanceledExceptionsReturnNullAndCanceledWithoutOperationError(string point)
    {
        var f = new Fixture(); f.Host.OnCall = call => { if (call == point) throw new OperationCanceledException("controlled"); };
        var task = f.Controller.StartAsync();
        Assert.Null(await task); Assert.True(task.IsCompletedSuccessfully);
        Assert.Null(f.Host.OperationError);
        Assert.Equal(BlockCheckSessionState.Canceled, f.Host.State);
        Assert.Equal(new[] { "state:Canceled", "finish", "cleanup", "actions" }, f.Host.Trace.TakeLast(4));
    }

    [Fact]
    public async Task BackendCanceledTaskIsTranslatedToNullAndCanceledState()
    {
        var f = new Fixture(); using var canceled = new CancellationTokenSource(); canceled.Cancel();
        f.Host.Run = (_, _) => Task.FromCanceled<BlockCheckRunResult>(canceled.Token);
        var task = f.Controller.StartAsync();
        Assert.Null(await task);
        Assert.True(task.IsCompletedSuccessfully);
        Assert.Equal(BlockCheckSessionState.Canceled, f.Host.State);
        Assert.Null(f.Host.OperationError);
        AssertDisposed(f.Host.RunCalls.Single().Token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancelChecksCanCancelBeforeMissingCtsAndDoesNothing(bool forceCanCancel)
    {
        var f = new Fixture(); f.Host.CancelOverride = forceCanCancel;
        f.Controller.Cancel();
        Assert.Equal(new[] { "can-cancel" }, f.Host.Trace);
        Assert.Equal(BlockCheckSessionState.Idle, f.Host.State);
    }

    [Fact]
    public async Task CancelSetsCancelingBeforeCancelCallbacksAndDoesNotAddResultCancellationPolicy()
    {
        var f = new Fixture(); var pending = Pending();
        f.Host.Run = (_, _) => pending.Task;
        var task = f.Controller.StartAsync(); var token = f.Host.RunCalls.Single().Token;
        BlockCheckSessionState? stateAtCancellation = null;
        using var registration = token.Register(() => stateAtCancellation = f.Host.State);
        f.Controller.Cancel();
        Assert.True(token.IsCancellationRequested);
        Assert.Equal(BlockCheckSessionState.Canceling, stateAtCancellation);
        int states = f.Host.Trace.Count(point => point == "state:Canceling");
        f.Controller.Cancel();
        Assert.Equal(states, f.Host.Trace.Count(point => point == "state:Canceling"));
        var result = Success(); pending.SetResult(result);
        Assert.Same(result, await task);
        Assert.Equal(BlockCheckSessionState.Completed, f.Host.State);
        AssertDisposed(token);
    }

    [Fact]
    public async Task CancelDuringRunningNotificationReachesBackendAsCanceledToken()
    {
        var f = new Fixture();
        f.Host.OnCall = point => { if (point == "state:Running") f.Controller.Cancel(); };
        await f.Controller.StartAsync();
        Assert.True(f.Host.RunCalls.Single().Token.IsCancellationRequested);
        Assert.True(f.Host.Trace.IndexOf("state:Canceling") < f.Host.Trace.IndexOf("run"));
        Assert.Equal(BlockCheckSessionState.Completed, f.Host.State);
    }

    [Fact]
    public async Task CancelCallbackFailuresEscapeSynchronouslyAfterStateAndTokenChanged()
    {
        var f = new Fixture(); var pending = Pending(); f.Host.Run = (_, _) => pending.Task;
        var task = f.Controller.StartAsync(); var token = f.Host.RunCalls.Single().Token;
        var failure = new IOException("cancel callback");
        using var registration = token.Register(() => throw failure);
        var aggregate = Assert.Throws<AggregateException>(() => f.Controller.Cancel());
        Assert.Same(failure, Assert.Single(aggregate.InnerExceptions));
        Assert.True(token.IsCancellationRequested);
        Assert.Equal(BlockCheckSessionState.Canceling, f.Host.State);
        pending.SetResult(new() { WasCanceled = true }); await task;
        Assert.Equal(BlockCheckSessionState.Canceled, f.Host.State);
    }

    [Fact]
    public async Task CancelWhileHistoryPendingKeepsOriginalFinalStateDecision()
    {
        var f = new Fixture(); var history = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Host.History = _ => history.Task;
        var task = f.Controller.StartAsync();
        f.Controller.Cancel(); Assert.Equal(BlockCheckSessionState.Canceling, f.Host.State);
        history.SetResult(); await task;
        Assert.Equal(BlockCheckSessionState.Completed, f.Host.State);
    }

    [Fact]
    public async Task OldFinallyCannotClearOrDisposeNewRunCtsStartedFromFinalStateCallback()
    {
        var f = new Fixture(); var second = Pending(); Task<BlockCheckRunResult?>? next = null;
        int runs = 0;
        f.Host.Run = (_, _) => ++runs == 1 ? Task.FromResult(Success()) : second.Task;
        bool reentered = false;
        f.Host.OnCall = point =>
        {
            if (point != "state:Completed" || reentered) return;
            reentered = true; next = f.Controller.StartAsync();
        };
        await f.Controller.StartAsync();
        Assert.NotNull(next); Assert.False(next.IsCompleted);
        Assert.Equal(2, f.Host.RunCalls.Count);
        var oldToken = f.Host.RunCalls[0].Token; var current = f.Host.RunCalls[1].Token;
        AssertDisposed(oldToken);
        Assert.NotNull(current.WaitHandle);
        f.Controller.Cancel(); Assert.True(current.IsCancellationRequested);
        second.SetResult(new() { WasCanceled = true }); await next;
        AssertDisposed(current);
        Assert.Equal(BlockCheckSessionState.Canceled, f.Host.State);
    }

    [Theory]
    [InlineData("error")]
    [InlineData("state:Failed")]
    [InlineData("state:Canceled")]
    public async Task FailuresInCatchCallbacksEscapeAndStillRunFinally(string point)
    {
        var f = new Fixture(); var secondary = new IOException("catch callback");
        f.Host.OnCall = call =>
        {
            if (call == "prepare") { if (point == "state:Canceled") throw new OperationCanceledException(); throw new IOException("primary"); }
            if (call == point) throw secondary;
        };
        Assert.Same(secondary, await Assert.ThrowsAsync<IOException>(() => f.Controller.StartAsync()));
        Assert.Equal(new[] { "finish", "cleanup", "actions" }, f.Host.Trace.TakeLast(3));
    }

    [Theory]
    [InlineData("finish")]
    [InlineData("cleanup")]
    [InlineData("actions")]
    public async Task FinallyFailuresStopLaterStepsWithOriginalDisposalBoundary(string point)
    {
        var f = new Fixture(); var failure = new IOException(point);
        f.Host.OnCall = call => { if (call == point) throw failure; };
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => f.Controller.StartAsync()));
        var token = f.Host.RunCalls.Single().Token;
        Assert.Equal(point, f.Host.Trace.Last());
        f.Host.CancelOverride = true; f.Host.OnCall = null;
        f.Controller.Cancel();
        if (point == "finish")
        {
            Assert.True(token.IsCancellationRequested);
            Assert.Equal(BlockCheckSessionState.Canceling, f.Host.State);
        }
        else
        {
            AssertDisposed(token);
            Assert.Equal(BlockCheckSessionState.Completed, f.Host.State);
        }
    }

    [Fact]
    public void BothRunAndHistoryContinuationsKeepCapturedSynchronizationContext()
    {
        var f = new Fixture(); var run = Pending(); var history = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Host.Run = (_, _) => run.Task; f.Host.History = _ => history.Task;
        var observed = new List<SynchronizationContext?>();
        f.Host.OnCall = point => { if (point is "apply" or "complete" or "history" or "state:Completed" or "finish" or "cleanup" or "actions") observed.Add(SynchronizationContext.Current); };
        var context = new PumpContext(); var previous = SynchronizationContext.Current;
        Task<BlockCheckRunResult?> task;
        try { SynchronizationContext.SetSynchronizationContext(context); task = f.Controller.StartAsync(); }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
        run.SetResult(Success()); PumpPosted(context);
        Assert.False(task.IsCompleted); Assert.Single(f.Host.HistoryReports);
        history.SetResult(); PumpPosted(context);
        Assert.True(task.IsCompletedSuccessfully);
        Assert.Equal(BlockCheckSessionState.Completed, f.Host.State);
        Assert.Equal(7, observed.Count);
        Assert.All(observed, value => Assert.Same(context, value));
    }

    private static void PumpPosted(PumpContext context)
    {
        Assert.True(SpinWait.SpinUntil(() => context.HasWork, TimeSpan.FromSeconds(3)));
        var previous = SynchronizationContext.Current;
        try { SynchronizationContext.SetSynchronizationContext(context); context.Pump(); }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }
    private static void AssertDisposed(CancellationToken token) => Assert.Throws<ObjectDisposedException>(() => { _ = token.WaitHandle; });
    private static BlockCheckRunResult Success() => new() { Synthesis = new() };
    private static BlockCheckIssue Error(string code) => new(BlockCheckIssueSeverity.Error, code, "fixture");
    private static TaskCompletionSource<BlockCheckRunResult> Pending() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private sealed class Fixture
    {
        public Host Host { get; } = new();
        public BlockCheck2RunController Controller { get; }
        public Fixture() => Controller = new(Host);
    }
    private sealed record RunCall(BlockCheckSessionPreparationResult Prepared, CancellationToken Token);
    private sealed record ApplyCall(BlockCheckSessionPreparationResult Prepared, BlockCheckRunResult Result);
    private sealed class Host : IBlockCheck2RunHost
    {
        public List<string> Trace { get; } = [];
        public List<RunCall> RunCalls { get; } = [];
        public List<ApplyCall> Applied { get; } = [];
        public List<BlockCheckReport> HistoryReports { get; } = [];
        public bool AllowStart = true;
        public bool? CancelOverride;
        public BlockCheckSessionState State = BlockCheckSessionState.Idle;
        public BlockCheckReport? ReportValue;
        public BlockCheckReport ResultReport = new();
        public BlockCheckSessionPreparationResult Prepared = new() { Targets = [new() { Id = "target" }] };
        public IReadOnlyList<BlockCheckTarget> Targets = [];
        public IReadOnlyList<BlockCheckIssue> Issues = [];
        public string? OperationError;
        public Action<string>? OnCall;
        public Func<BlockCheckSessionPreparationResult, CancellationToken, Task<BlockCheckRunResult>> Run = (_, _) => Task.FromResult(Success());
        public Func<BlockCheckReport, Task> History = _ => Task.CompletedTask;
        private void Call(string point) { Trace.Add(point); OnCall?.Invoke(point); }
        public bool CanStart { get { Call("can-start"); return AllowStart && State is not (BlockCheckSessionState.Running or BlockCheckSessionState.Canceling); } }
        public bool CanCancel { get { Call("can-cancel"); return CancelOverride ?? State == BlockCheckSessionState.Running; } }
        public BlockCheckReport? LastReport { get { Call("report"); return ReportValue; } }
        public void RenewGeneratedSiteListRoot() => Call("renew");
        public void ResetResultState() { ReportValue = null; Call("reset"); }
        public void StartProgress() => Call("start");
        public BlockCheckSessionPreparationResult Prepare() { Call("prepare"); return Prepared; }
        public void ApplyPrepared(BlockCheckSessionPreparationResult prepared) { Targets = prepared.Targets; Issues = prepared.Issues; Call("prepared"); }
        public void SetState(BlockCheckSessionState state) { State = state; Call("state:" + state); }
        public Task<BlockCheckRunResult> RunPreparedAsync(BlockCheckSessionPreparationResult prepared, CancellationToken cancellationToken)
        {
            RunCalls.Add(new(prepared, cancellationToken)); Call("run"); return Run(prepared, cancellationToken);
        }
        public void ApplyRunResult(BlockCheckSessionPreparationResult prepared, BlockCheckRunResult result)
        {
            Applied.Add(new(prepared, result)); ReportValue = ResultReport; Call("apply");
        }
        public void CompleteDisplayedProgress() => Call("complete");
        public Task SaveReportToHistoryAsync(BlockCheckReport report) { HistoryReports.Add(report); Call("history"); return History(report); }
        public void SetOperationError(string message) { OperationError = message; Call("error"); }
        public void FinishProgress() => Call("finish");
        public void CleanupGeneratedSiteListsIfUnviewable() => Call("cleanup");
        public void NotifyActionState() => Call("actions");
    }
    private sealed class PumpContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _work = new();
        public bool HasWork => !_work.IsEmpty;
        public override void Post(SendOrPostCallback d, object? state) => _work.Enqueue((d, state));
        public void Pump() { while (_work.TryDequeue(out var work)) work.Callback(work.State); }
    }
}
