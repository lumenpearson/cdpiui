using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Shared.Models;
using CDPIUI.Shared.PrettyErrorConversionService;
using CDPIUI.ViewModels;
using CDPIUI.ViewModels.BlockCheck2;

namespace CDPIUI.Tests.Presentation;

public class BlockCheck2ManualTestSessionTests
{
    [Fact]
    public void ConstructorDoesNotReadHostResolveIdOrSubscribe()
    {
        var f = new Fixture();
        Assert.False(f.Owner.IsRunning);
        Assert.Empty(f.Trace);
        Assert.Empty(f.Order);
        Assert.Equal(0, f.Tasks.SubscriberCount);
    }

    [Fact]
    public async Task MissingItemStopsBeforeRunningStrategyOrAnyOtherPort()
    {
        var f = new Fixture { Item = null, StrategyRunning = true };
        await f.Owner.StartAsync();
        Assert.Equal(["selected"], f.Trace);
    }

    [Fact]
    public async Task RunningManualTestDoesNotReadStrategyFlagOrRestart()
    {
        var f = new Fixture(); await f.Owner.StartAsync(); f.Clear();
        await f.Owner.StartAsync();
        Assert.Equal(["selected"], f.Trace);
        Assert.True(f.Owner.IsRunning);
    }

    [Fact]
    public async Task RunningSingleStrategyBlocksManualStart()
    {
        var f = new Fixture { StrategyRunning = true };
        await f.Owner.StartAsync();
        Assert.Equal(["selected", "strategy"], f.Trace);
        Assert.False(f.Owner.IsRunning);
    }

    [Fact]
    public async Task StartPublishesFlagsAndSubscribesExactHostHandlerBeforeRun()
    {
        var f = new Fixture();
        f.OnCall = name => { if (name is "subscribe" or "show:Informational" or "run:A:--args") Assert.True(f.Owner.IsRunning); };
        await f.Owner.StartAsync();
        Assert.Equal(["selected", "strategy", "selection", "arguments", "selection", "id:A", "check:A", "id:A", "stop:A",
            "subscribe", "selection", "text:ManualTesting", "text:ConfigMakerTestStartedMessage", "show:Informational", "id:A", "run:A:--args"], f.Trace);
        Assert.Same(f.Handler, Assert.Single(f.Tasks.Added));
        Assert.Equal((BlockCheck2NoticeSeverity.Informational, "ManualTesting", "ConfigMakerTestStartedMessage"), Assert.Single(f.Notices));
        Assert.True(f.Owner.IsRunning);
    }

    [Theory]
    [InlineData(false, "--present")]
    [InlineData(true, "")]
    [InlineData(true, null)]
    public async Task InvalidArgumentsShowWarningWithoutComponentCalls(bool success, string? arguments)
    {
        var f = new Fixture { Arguments = new() { Success = success, Result = arguments } };
        await f.Owner.StartAsync();
        Assert.Equal(["selected", "strategy", "selection", "arguments", "text:ManualTesting", "text:ConfigMakerEmptyArgumentsMessage", "show:Warning"], f.Trace);
        Assert.Equal((BlockCheck2NoticeSeverity.Warning, "ManualTesting", "ConfigMakerEmptyArgumentsMessage"), Assert.Single(f.Notices));
        Assert.False(f.Owner.IsRunning);
    }

    [Fact]
    public async Task ArgumentErrorCodeIsShownWithoutEmptyArgumentLocalization()
    {
        var f = new Fixture { Arguments = OperationResultModel<string>.FailureResult(ErrorModel.OnlyErrorCode("OWNED_ERROR")) };
        await f.Owner.StartAsync();
        Assert.Equal((BlockCheck2NoticeSeverity.Warning, "ManualTesting", "OWNED_ERROR"), Assert.Single(f.Notices));
        Assert.DoesNotContain("text:ConfigMakerEmptyArgumentsMessage", f.Trace);
        Assert.DoesNotContain("id:A", f.Trace);
    }

    [Fact]
    public async Task WhitespaceArgumentsAreNotRejectedOrTrimmed()
    {
        var f = new Fixture { Arguments = OperationResultModel<string>.SuccessResult("  ") };
        await f.Owner.StartAsync();
        Assert.Contains("run:A:  ", f.Trace);
        Assert.True(f.Owner.IsRunning);
    }

    [Fact]
    public async Task SelectionPublicationKeepsCapturedItemButReadsCurrentArguments()
    {
        var f = new Fixture(); var selected = f.Item;
        f.OnCall = name =>
        {
            if (name != "selection") return;
            f.Item = new(new()); f.Arguments = OperationResultModel<string>.SuccessResult("--current");
        };
        await f.Owner.StartAsync();
        Assert.Same(selected, Assert.Single(f.ArgumentItems));
        Assert.Contains("run:A:--current", f.Trace);
    }

    [Fact]
    public async Task DecliningRunningReplacementDoesNotStopSubscribeOrPublishStarted()
    {
        var f = new Fixture { ConfirmResult = false }; f.Tasks.WasRunning = true;
        await f.Owner.StartAsync();
        Assert.Equal(["selected", "strategy", "selection", "arguments", "selection", "id:A", "check:A", "confirm"], f.Trace);
        Assert.False(f.Owner.IsRunning); Assert.Empty(f.Notices); Assert.Equal(0, f.Tasks.SubscriberCount);
    }

    [Fact]
    public async Task ConfirmIsNotReadWhenComponentWasStopped()
    {
        var f = new Fixture();
        f.OnCall = name => { if (name == "confirm") throw new InvalidOperationException("Unexpected confirmation"); };
        await f.Owner.StartAsync();
        Assert.DoesNotContain("confirm", f.Trace);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingConfirmationDefersStopAndKeepsOriginalRunningSnapshot(bool accepted)
    {
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var f = new Fixture(); f.Tasks.WasRunning = true; f.Confirm = () => gate.Task;
        Task start = f.Owner.StartAsync();
        try
        {
            Assert.False(start.IsCompleted); Assert.False(f.Owner.IsRunning);
            Assert.DoesNotContain("stop:A", f.Trace); Assert.Equal(0, f.Tasks.SubscriberCount);
            f.Tasks.WasRunning = false;
            gate.SetResult(accepted); await Wait(start);
            Assert.Equal(accepted, f.Owner.IsRunning);
            if (accepted) { f.Clear(); await f.Owner.StopAsync(); Assert.Contains("restore:A", f.Trace); }
            else { Assert.DoesNotContain("stop:A", f.Trace); Assert.Empty(f.Notices); }
        }
        finally { gate.TrySetResult(false); await Wait(start); }
    }

    [Fact]
    public async Task StartReadsLiveIdForCheckStopAndRunAfterCallbacks()
    {
        var f = new Fixture(); f.Tasks.WasRunning = true;
        f.OnCall = name =>
        {
            if (name == "confirm") f.Id = "B";
            if (name == "stop:B") f.Id = "C";
            if (name == "show:Informational") f.Id = "D";
        };
        await f.Owner.StartAsync();
        Assert.Contains("check:A", f.Trace); Assert.Contains("stop:B", f.Trace); Assert.Contains("run:D:--args", f.Trace);
        Assert.DoesNotContain("stop:A", f.Trace);
    }

    [Fact]
    public async Task ReceiverIsResolvedBeforeIdAndResolvedAgainForLaterOperations()
    {
        var f = new Fixture(); var replacement = new Tasks(f);
        f.OnCall = name => { if (name == "id:A") { f.CurrentTasks = replacement; f.Id = "B"; } };
        await f.Owner.StartAsync();
        Assert.Equal(["A"], f.Tasks.CheckIds); Assert.Empty(replacement.CheckIds);
        Assert.Empty(f.Tasks.StopIds); Assert.Equal(["B"], replacement.StopIds);
        Assert.Equal(["selected", "strategy", "selection", "arguments", "selection", "resolve", "id:A", "check:A",
            "resolve", "id:B", "stop:B", "resolve", "subscribe"], f.Order.Take(13));
        Assert.Equal(1, replacement.SubscriberCount);
    }

    [Theory]
    [InlineData("selected")]
    [InlineData("strategy")]
    [InlineData("selection")]
    [InlineData("arguments")]
    [InlineData("resolve")]
    [InlineData("check:A")]
    [InlineData("confirm")]
    [InlineData("stop:A")]
    [InlineData("subscribe")]
    [InlineData("text:ConfigMakerTestStartedMessage")]
    [InlineData("show:Informational")]
    public async Task SetupFaultIsReturnedInTaskOutsideRunCatch(string endpoint)
    {
        var f = new Fixture(); f.Tasks.WasRunning = true;
        var fault = new InvalidOperationException("setup fault");
        f.OnCall = name => { if (name == endpoint) throw fault; };
        Task task = f.Owner.StartAsync();
        Assert.Same(fault, await Assert.ThrowsAsync<InvalidOperationException>(() => Wait(task)));
        Assert.DoesNotContain("show:Error", f.Trace);
        Assert.DoesNotContain("assignments", f.Trace);
        Assert.DoesNotContain("run:A:--args", f.Trace);
        Assert.Equal(endpoint is "subscribe" or "text:ConfigMakerTestStartedMessage" or "show:Informational", f.Owner.IsRunning);
    }

    [Fact]
    public async Task StartedNoticeFaultLeavesStartingGuardActiveUntilExplicitStop()
    {
        var f = new Fixture(); var fault = new InvalidOperationException("notice fault");
        f.OnCall = name => { if (name == "show:Informational") throw fault; };
        Task task = f.Owner.StartAsync();
        Assert.Same(fault, await Assert.ThrowsAsync<InvalidOperationException>(() => Wait(task)));
        f.OnCall = null; f.Clear(); f.Tasks.Emit("A", false);
        Assert.True(f.Owner.IsRunning); Assert.Empty(f.Trace); Assert.Empty(f.Queue);
        await f.Owner.StopAsync(); Assert.False(f.Owner.IsRunning);
    }

    [Fact]
    public async Task PendingRunDropsStartingEventsAndConcurrentStopDoesNotRestartAfterAcknowledgment()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var f = new Fixture(); f.Tasks.Run = (_, _) => gate.Task;
        Task start = f.Owner.StartAsync();
        try
        {
            Assert.False(start.IsCompleted); Assert.True(f.Owner.IsRunning);
            f.Clear(); f.Tasks.Emit("A", false); Assert.Empty(f.Trace); Assert.Empty(f.Queue);
            await Wait(f.Owner.StopAsync()); Assert.False(f.Owner.IsRunning); Assert.Equal(0, f.Tasks.SubscriberCount);
            gate.SetResult(); await Wait(start);
            Assert.False(f.Owner.IsRunning); Assert.Equal(0, f.Tasks.SubscriberCount);
        }
        finally { gate.TrySetResult(); await Wait(start); }
    }

    [Fact]
    public async Task TwoStartsPendingAtRunningCheckRemainIndependentWithoutNewLock()
    {
        var firstGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var f = new Fixture(); int checks = 0;
        f.Tasks.Check = _ => ++checks == 1 ? firstGate.Task : secondGate.Task;
        Task first = f.Owner.StartAsync(); Task second = f.Owner.StartAsync();
        try
        {
            Assert.Equal(2, f.Trace.Count(name => name == "check:A"));
            Assert.False(first.IsCompleted); Assert.False(second.IsCompleted);
            firstGate.SetResult(false); await Wait(first);
            Assert.False(second.IsCompleted);
            secondGate.SetResult(false); await Wait(second);
            Assert.Equal(2, f.Trace.Count(name => name == "run:A:--args"));
            Assert.Equal(2, f.Tasks.SubscriberCount);
        }
        finally { firstGate.TrySetResult(false); secondGate.TrySetResult(false); await Wait(Task.WhenAll(first, second)); }
    }

    [Fact]
    public async Task StartedNoticeMayStopReentrantlyButOuterStartStillCallsRun()
    {
        var f = new Fixture(); Task? stop = null; bool entered = false;
        f.OnCall = name => { if (name == "show:Informational" && !entered) { entered = true; stop = f.Owner.StopAsync(); } };
        await f.Owner.StartAsync(); await stop!;
        Assert.False(f.Owner.IsRunning); Assert.Equal(0, f.Tasks.SubscriberCount);
        Assert.Equal("run:A:--args", f.Trace.Last());
        Assert.Single(f.Notices);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunFailureShowsErrorThenStopsAndRestoresPreviousComponent(bool canceled)
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Task failure = canceled ? Task.FromCanceled(cancellation.Token) : Task.FromException(new IOException("run failed"));
        Exception expected = await Assert.ThrowsAnyAsync<Exception>(() => failure);
        var f = new Fixture(); f.Tasks.WasRunning = true; f.Tasks.Run = (_, _) => failure;
        await f.Owner.StartAsync();
        Assert.False(f.Owner.IsRunning); Assert.Equal(0, f.Tasks.SubscriberCount);
        Assert.Equal((BlockCheck2NoticeSeverity.Error, "ManualTesting", "start failed: " + expected.Message), f.Notices[1]);
        Assert.Equal(["selection", "unsubscribe", "id:A", "id:A", "stop:A", "id:A", "restore:A", "selection", "assignments"], f.Trace.TakeLast(9));
        Assert.Empty(f.Warnings);
    }

    [Fact]
    public async Task MalformedFailureFormatLeavesRunningButStartingGuardIsReleased()
    {
        var f = new Fixture { FailedFormat = "{1}" }; f.Tasks.Run = (_, _) => throw new IOException("run failed");
        Task task = f.Owner.StartAsync(); await Assert.ThrowsAsync<FormatException>(() => Wait(task));
        Assert.True(f.Owner.IsRunning); Assert.Equal(1, f.Tasks.SubscriberCount);
        Assert.Single(f.Notices); f.Clear(); f.Tasks.Emit("A", false);
        Assert.Equal(["id:A", "enqueue"], f.Trace); Assert.Single(f.Queue);
    }

    [Fact]
    public async Task RunFailureCleanupFaultEscapesWithoutSecondErrorOrWarning()
    {
        var f = new Fixture(); f.Tasks.Run = (_, _) => throw new IOException("run failed");
        var cleanupFault = new InvalidOperationException("cleanup failed"); int stops = 0;
        f.Tasks.Stop = _ => ++stops == 2 ? throw cleanupFault : Task.CompletedTask;
        Task task = f.Owner.StartAsync();
        Assert.Same(cleanupFault, await Assert.ThrowsAsync<InvalidOperationException>(() => Wait(task)));
        Assert.False(f.Owner.IsRunning); Assert.Equal(2, f.Notices.Count); Assert.Empty(f.Warnings);
        Assert.DoesNotContain("assignments", f.Trace);
    }

    [Fact]
    public async Task IdResolutionForRunIsInsideStartFailureCatch()
    {
        var f = new Fixture(); int reads = 0;
        f.OnCall = name => { if (name == "id:A" && ++reads == 3) throw new InvalidOperationException("run id fault"); };
        await f.Owner.StartAsync();
        Assert.False(f.Owner.IsRunning); Assert.DoesNotContain("run:A:--args", f.Trace);
        Assert.Equal((BlockCheck2NoticeSeverity.Error, "ManualTesting", "start failed: run id fault"), f.Notices[1]);
        Assert.Equal("assignments", f.Trace.Last()); Assert.Empty(f.Warnings);
    }

    [Fact]
    public async Task StoppedStopOnlyUpdatesSelectionAndLeaveDoesNothing()
    {
        var f = new Fixture();
        await f.Owner.StopAsync(true); Assert.Equal(["selection"], f.Trace);
        f.Clear(); await f.Owner.StopOnLeaveAsync(); Assert.Empty(f.Trace);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreviouslyStoppedComponentIsNeverRestored(bool leave)
    {
        var f = new Fixture(); await f.Owner.StartAsync(); f.Clear();
        if (leave) await f.Owner.StopOnLeaveAsync(); else await f.Owner.StopAsync();
        Assert.Equal(leave ? ["unsubscribe", "id:A", "stop:A"] :
            ["selection", "unsubscribe", "id:A", "id:A", "stop:A", "selection", "assignments"], f.Trace);
        Assert.False(f.Owner.IsRunning);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StopClearsRunningAndUnsubscribesBeforeStopAwaitThenUpdatesView(bool completionMessage)
    {
        var f = new Fixture(); f.Tasks.WasRunning = true; await f.Owner.StartAsync(); f.Clear();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); f.Tasks.Stop = _ => gate.Task;
        Task stop = f.Owner.StopAsync(completionMessage);
        try
        {
            Assert.False(stop.IsCompleted); Assert.False(f.Owner.IsRunning); Assert.Equal(0, f.Tasks.SubscriberCount);
            Assert.Equal(["selection", "unsubscribe", "id:A", "id:A", "stop:A"], f.Trace);
            gate.SetResult(); await Wait(stop);
            var expected = new List<string> { "selection", "unsubscribe", "id:A", "id:A", "stop:A", "id:A", "restore:A" };
            if (completionMessage) expected.AddRange(["text:ManualTesting", "text:ConfigMakerTestStoppedMessage", "show:Informational"]);
            expected.AddRange(["selection", "assignments"]); Assert.Equal(expected, f.Trace);
        }
        finally { gate.TrySetResult(); await Wait(stop); }
    }

    [Fact]
    public async Task StopRereadsIdForWhitespaceGuardStopAndRestore()
    {
        var f = new Fixture(); f.Tasks.WasRunning = true; await f.Owner.StartAsync(); f.Clear();
        f.OnCall = name => { if (name == "id:A") f.Id = "B"; if (name == "stop:B") f.Id = "C"; };
        await f.Owner.StopAsync();
        Assert.Equal(["selection", "unsubscribe", "id:A", "id:B", "stop:B", "id:C", "restore:C", "selection", "assignments"], f.Trace);
    }

    [Fact]
    public async Task StopWithBlankIdStillPublishesCompletionAndAssignments()
    {
        var f = new Fixture(); f.Tasks.WasRunning = true; await f.Owner.StartAsync(); f.Clear(); f.Id = " ";
        await f.Owner.StopAsync(true);
        Assert.Equal(["selection", "unsubscribe", "id: ", "text:ManualTesting", "text:ConfigMakerTestStoppedMessage", "show:Informational", "selection", "assignments"], f.Trace);
        Assert.False(f.Owner.IsRunning);
    }

    [Theory]
    [InlineData("selection", true)]
    [InlineData("unsubscribe", false)]
    [InlineData("id:A", false)]
    [InlineData("stop:A", false)]
    [InlineData("restore:A", false)]
    [InlineData("text:ConfigMakerTestStoppedMessage", false)]
    [InlineData("show:Informational", false)]
    [InlineData("assignments", false)]
    public async Task StopFaultEscapesAtOriginalBoundaryWithoutWarning(string endpoint, bool stillRunning)
    {
        var f = new Fixture(); f.Tasks.WasRunning = true; await f.Owner.StartAsync(); f.Clear();
        var fault = new InvalidOperationException("stop fault"); f.OnCall = name => { if (name == endpoint) throw fault; };
        Task task = f.Owner.StopAsync(true);
        Assert.Same(fault, await Assert.ThrowsAsync<InvalidOperationException>(() => Wait(task)));
        Assert.Equal(stillRunning, f.Owner.IsRunning); Assert.Empty(f.Warnings); Assert.Equal(endpoint, f.Trace.Last());
    }

    [Fact]
    public async Task LeaveCapturesOneIdAndDoesNotUpdateSelectionOrShowCompletion()
    {
        var f = new Fixture(); f.Tasks.WasRunning = true; await f.Owner.StartAsync(); f.Clear();
        f.OnCall = name => { if (name == "id:A") f.Id = "B"; if (name == "stop:A") f.Id = "C"; };
        await f.Owner.StopOnLeaveAsync();
        Assert.Equal(["unsubscribe", "id:A", "stop:A", "restore:A"], f.Trace);
        Assert.False(f.Owner.IsRunning); Assert.Empty(f.Notices);
    }

    [Fact]
    public async Task LeaveWithBlankIdStopsAfterUnsubscribeWithoutViewUpdates()
    {
        var f = new Fixture(); await f.Owner.StartAsync(); f.Clear(); f.Id = "";
        await f.Owner.StopOnLeaveAsync();
        Assert.Equal(["unsubscribe", "id:"], f.Trace); Assert.False(f.Owner.IsRunning);
    }

    [Theory]
    [InlineData("unsubscribe")]
    [InlineData("id:A")]
    public async Task LeaveSetupFaultIsOutsideWarningCatch(string endpoint)
    {
        var f = new Fixture(); await f.Owner.StartAsync(); f.Clear(); var fault = new InvalidOperationException("leave setup");
        f.OnCall = name => { if (name == endpoint) throw fault; };
        Task task = f.Owner.StopOnLeaveAsync();
        Assert.Same(fault, await Assert.ThrowsAsync<InvalidOperationException>(() => Wait(task)));
        Assert.False(f.Owner.IsRunning); Assert.Empty(f.Warnings);
    }

    [Theory]
    [InlineData("stop:A")]
    [InlineData("restore:A")]
    public async Task LeaveComponentFaultIsWarnedWithOriginalExceptionAndNoViewUpdates(string endpoint)
    {
        var f = new Fixture(); f.Tasks.WasRunning = true; await f.Owner.StartAsync(); f.Clear(); var fault = new IOException("leave component");
        f.OnCall = name => { if (name == endpoint) throw fault; };
        await f.Owner.StopOnLeaveAsync();
        Assert.Same(fault, Assert.Single(f.Warnings)); Assert.False(f.Owner.IsRunning);
        Assert.Empty(f.Notices); Assert.DoesNotContain("selection", f.Trace); Assert.DoesNotContain("assignments", f.Trace);
        Assert.Equal("warn:leave component", f.Trace.Last());
    }

    [Fact]
    public async Task WarningFaultEscapesWithoutRetry()
    {
        var f = new Fixture(); await f.Owner.StartAsync(); f.Clear(); var fault = new InvalidOperationException("warn callback");
        f.Tasks.Stop = _ => throw new IOException("leave component"); f.OnCall = name => { if (name.StartsWith("warn:")) throw fault; };
        Task task = f.Owner.StopOnLeaveAsync();
        Assert.Same(fault, await Assert.ThrowsAsync<InvalidOperationException>(() => Wait(task)));
        Assert.Single(f.Warnings); Assert.False(f.Owner.IsRunning);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(null, false)]
    public async Task ButtonToggleUsesAlreadyChangedNullableCheck(bool? check, bool starts)
    {
        var f = new Fixture { Checked = check };
        await f.Owner.ToggleFromButtonAsync();
        Assert.Equal("checked", f.Trace[0]); Assert.Equal(starts, f.Owner.IsRunning);
        if (!starts) Assert.Equal(["checked", "selection"], f.Trace);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task UncheckingRunningButtonStopsAndShowsCompletionExactlyOnce(bool? check)
    {
        var f = new Fixture(); await f.Owner.StartAsync(); f.Clear(); f.Checked = check;
        await f.Owner.ToggleFromButtonAsync();
        Assert.False(f.Owner.IsRunning); Assert.Equal(0, f.Tasks.SubscriberCount);
        Assert.Equal((BlockCheck2NoticeSeverity.Informational, "ManualTesting", "ConfigMakerTestStoppedMessage"), Assert.Single(f.Notices));
        Assert.Equal(["checked", "selection", "unsubscribe", "id:A", "id:A", "stop:A",
            "text:ManualTesting", "text:ConfigMakerTestStoppedMessage", "show:Informational", "selection", "assignments"], f.Trace);
    }

    [Fact]
    public async Task MenuToggleUsesRunningStateInsteadOfButtonCheck()
    {
        var f = new Fixture { Checked = false }; await f.Owner.ToggleAsync();
        Assert.True(f.Owner.IsRunning); f.Clear(); f.Checked = true; await f.Owner.ToggleAsync();
        Assert.False(f.Owner.IsRunning); Assert.DoesNotContain("checked", f.Trace);
        Assert.Equal((BlockCheck2NoticeSeverity.Informational, "ManualTesting", "ConfigMakerTestStoppedMessage"), Assert.Single(f.Notices));
    }

    [Fact]
    public void StoppedStateEventShortCircuitsBeforeReadingNullTupleOrId()
    {
        var f = new Fixture(); f.Owner.HandleStateUpdated(null!); Assert.Empty(f.Trace);
    }

    [Theory]
    [InlineData("other", false)]
    [InlineData("A", true)]
    public async Task WrongOrStillRunningEventDoesNotQueue(string id, bool running)
    {
        var f = new Fixture(); await f.Owner.StartAsync(); f.Clear(); f.Tasks.Emit(id, running);
        Assert.Equal(["id:A"], f.Trace); Assert.Empty(f.Queue); Assert.True(f.Owner.IsRunning);
    }

    [Fact]
    public async Task MatchingEventUsesCurrentCaseInsensitiveIdAndDefersAllStateChanges()
    {
        var f = new Fixture(); await f.Owner.StartAsync(); f.Clear(); f.Id = "B";
        f.Tasks.Emit("A", false); f.Tasks.Emit("b", false);
        Assert.Equal(["id:B", "id:B", "enqueue"], f.Trace); Assert.True(f.Owner.IsRunning); Assert.Empty(f.Notices);
        await Wait(Assert.Single(f.Queue)());
        Assert.False(f.Owner.IsRunning);
        Assert.Equal((BlockCheck2NoticeSeverity.Warning, "ManualTesting", "ConfigMakerTestExitedMessage"), Assert.Single(f.Notices));
        Assert.Contains("stop:B", f.Trace); Assert.Equal("assignments", f.Trace.Last());
    }

    [Fact]
    public async Task QueuedExitDoesNotRepeatGuardOrDeduplicateAfterExplicitStop()
    {
        var f = new Fixture(); await f.Owner.StartAsync(); f.Tasks.Emit("A", false); f.Tasks.Emit("A", false);
        Assert.Equal(2, f.Queue.Count); await f.Owner.StopAsync(); f.Clear();
        foreach (var callback in f.Queue) await Wait(callback());
        Assert.Equal(2, f.Notices.Count); Assert.All(f.Notices, notice => Assert.Equal(BlockCheck2NoticeSeverity.Warning, notice.Severity));
        Assert.Equal(2, f.Trace.Count(name => name == "selection")); Assert.DoesNotContain("stop:A", f.Trace);
    }

    [Fact]
    public async Task DroppedDispatchDoesNotClearRunningOrShowExitNotice()
    {
        var f = new Fixture { DropQueue = true }; await f.Owner.StartAsync(); f.Clear(); f.Tasks.Emit("A", false);
        Assert.True(f.Owner.IsRunning); Assert.Empty(f.Queue); Assert.Empty(f.Notices); Assert.Equal(["id:A", "enqueue"], f.Trace);
    }

    [Fact]
    public async Task QueuedNoticeFaultIsReturnedByCallbackAndPreventsStop()
    {
        var f = new Fixture(); await f.Owner.StartAsync(); f.Tasks.Emit("A", false); f.Clear(); var fault = new IOException("exit notice");
        f.OnCall = name => { if (name == "show:Warning") throw fault; };
        Task callback = Assert.Single(f.Queue)();
        Assert.Same(fault, await Assert.ThrowsAsync<IOException>(() => Wait(callback)));
        Assert.True(f.Owner.IsRunning); Assert.DoesNotContain("selection", f.Trace); Assert.Empty(f.Warnings);
    }

    private static Task Wait(Task task) => task.WaitAsync(TimeSpan.FromSeconds(3));

    private sealed class Fixture : IBlockCheck2ManualTestHost
    {
        internal readonly List<string> Trace = [];
        internal readonly List<string> Order = [];
        internal readonly List<(BlockCheck2NoticeSeverity Severity, string Title, string Message)> Notices = [];
        internal readonly List<Exception> Warnings = [];
        internal readonly List<BlockCheck2StrategyEvidenceItem> ArgumentItems = [];
        internal readonly List<Func<Task>> Queue = [];
        internal readonly Tasks Tasks;
        internal IConfigMakerTestTasks CurrentTasks;
        internal readonly BlockCheck2ManualTestSession Owner;
        internal readonly Action<Tuple<string, bool>> Handler;
        internal BlockCheck2StrategyEvidenceItem? Item = new(new());
        internal bool StrategyRunning, ConfirmResult = true, DropQueue;
        internal bool? Checked;
        internal string Id = "A", FailedFormat = "start failed: {0}";
        internal OperationResultModel<string> Arguments = OperationResultModel<string>.SuccessResult("--args");
        internal Action<string>? OnCall;
        internal Func<Task<bool>>? Confirm;
        internal Fixture()
        {
            Tasks = new(this); CurrentTasks = Tasks; Handler = state => Owner.HandleStateUpdated(state);
            Owner = new(this, ResolveTasks, () => { string id = Id; Call("id:" + id); return id; }, Handler);
        }
        private IConfigMakerTestTasks ResolveTasks() { Order.Add("resolve"); OnCall?.Invoke("resolve"); return CurrentTasks; }
        internal void Clear() { Trace.Clear(); Order.Clear(); Notices.Clear(); Warnings.Clear(); }
        internal void Call(string name) { Trace.Add(name); Order.Add(name); OnCall?.Invoke(name); }
        public BlockCheck2StrategyEvidenceItem? SelectedStrategy { get { Call("selected"); return Item; } }
        public bool IsStrategyTestRunning { get { Call("strategy"); return StrategyRunning; } }
        public bool? IsManualTestChecked { get { Call("checked"); return Checked; } }
        public OperationResultModel<string> GetFullStrategyArguments(BlockCheck2StrategyEvidenceItem item)
        { ArgumentItems.Add(item); Call("arguments"); return Arguments; }
        public Task<bool> ConfirmReplacementAsync() { Call("confirm"); return Confirm?.Invoke() ?? Task.FromResult(ConfirmResult); }
        public void UpdateSelectedStrategy() => Call("selection");
        public void UpdateAssignmentView() => Call("assignments");
        public string Text(string key) { Call("text:" + key); return key == "ConfigMakerTestStartFailedMessage" ? FailedFormat : key; }
        public void ShowMessage(BlockCheck2NoticeSeverity severity, string title, string message)
        { Notices.Add((severity, title, message)); Call("show:" + severity); }
        public void Warn(Exception exception) { Warnings.Add(exception); Call("warn:" + exception.Message); }
        public void Enqueue(Func<Task> callback) { Call("enqueue"); if (!DropQueue) Queue.Add(callback); }
    }

    private sealed class Tasks(Fixture f) : IConfigMakerTestTasks
    {
        private Action<Tuple<string, bool>>? _handlers;
        internal readonly List<Action<Tuple<string, bool>>> Added = [];
        internal readonly List<string> CheckIds = [], StopIds = [];
        internal bool WasRunning;
        internal Func<string, Task<bool>>? Check;
        internal Func<string, Task> Stop = _ => Task.CompletedTask;
        internal Func<string, string, Task> Run = (_, _) => Task.CompletedTask;
        internal int SubscriberCount => _handlers?.GetInvocationList().Length ?? 0;
        public event Action<Tuple<string, bool>>? StateUpdated
        {
            add { _handlers += value; Added.Add(value!); f.Call("subscribe"); }
            remove { _handlers -= value; f.Call("unsubscribe"); }
        }
        internal void Emit(string id, bool running) => _handlers?.Invoke(Tuple.Create(id, running));
        public Task<bool> IsRunningAsync(string componentId) { CheckIds.Add(componentId); f.Call("check:" + componentId); return Check?.Invoke(componentId) ?? Task.FromResult(WasRunning); }
        public Task StopAsync(string componentId) { StopIds.Add(componentId); f.Call("stop:" + componentId); return Stop(componentId); }
        public Task RunAsync(string componentId, string arguments) { f.Call("run:" + componentId + ":" + arguments); return Run(componentId, arguments); }
        public Task RestoreAsync(string componentId) { f.Call("restore:" + componentId); return Task.CompletedTask; }
    }
}
