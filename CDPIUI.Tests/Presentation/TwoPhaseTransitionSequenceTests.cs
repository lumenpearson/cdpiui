using System.Collections;
using CDPIUI.Helper.UserExperience;

namespace CDPIUI.Tests.Presentation;

public class TwoPhaseTransitionSequenceTests
{
    [Fact]
    public async Task BothGroupsUseTheirOwnDelayPolicyAndNeverDelayAfterLastTransition()
    {
        using var f = new Fixture(); var a = new Cell("A"); var b = new Cell("B", true);
        var c = new Cell("C"); var d = new Cell("D", true);
        f.First.Items.AddRange([(a, "hide"), (b, "hide")]); f.Second.Items.AddRange([(c, "show"), (d, "show")]);
        await f.Run();
        Assert.Equal(["count:first", "count:second", "inspect:A:False", "apply:A:hide", "first:False", "delay:10",
            "inspect:B:True", "apply:B:hide", "first:True", "delay:30", "inspect:C:False", "apply:C:show", "second:False", "delay:20",
            "inspect:D:True", "apply:D:show", "finish"], f.Trace);
        Assert.Equal([10, 30, 20], f.Delays.Select(x => x.Duration.TotalMilliseconds));
        Assert.All(f.Delays, x => Assert.Equal(f.Source.Token, x.Token));
        Assert.Equal("hide", a.State); Assert.Equal("hide", b.State); Assert.Equal("show", c.State); Assert.Equal("show", d.State);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OneTransitionStillInspectsAndAppliesWithoutSelectingDelayOrReadingDisposedSource(bool first)
    {
        using var f = new Fixture(); var item = new Cell("only", true);
        (first ? f.First : f.Second).Items.Add((item, "new")); f.Source.Dispose();
        f.OnStep = name => { if (name.StartsWith("first:") || name.StartsWith("second:") || name.StartsWith("delay:")) throw new InvalidOperationException(name); };
        await f.Run();
        Assert.Equal(["count:first", "count:second", "inspect:only:True", "apply:only:new", "finish"], f.Trace);
        Assert.Equal("new", item.State); Assert.Empty(f.Delays);
    }

    [Fact]
    public async Task EmptyGroupsOnlyReadTheTwoCountsAndDoNotReadDisposedSource()
    {
        using var f = new Fixture(); f.Source.Dispose(); await f.Run();
        Assert.Equal(["count:first", "count:second", "finish"], f.Trace);
    }

    [Fact]
    public async Task InspectionIsCapturedBeforeApplyButDelaySelectorRunsAfterIt()
    {
        using var f = Pair(); var item = f.First.Items[0].Item;
        f.OnStep = name =>
        {
            if (name == "apply:A:hide") item.Extra = true;
            if (name == "first:False") Assert.Equal("hide", item.State);
        };
        await f.Run();
        Assert.True(item.Extra); Assert.Contains("first:False", f.Trace); Assert.DoesNotContain("first:True", f.Trace);
        Assert.Equal(10, Assert.Single(f.Delays).Duration.TotalMilliseconds);
    }

    [Fact]
    public async Task SecondPhaseWithExtraMovementUsesItsOwnDelaySelector()
    {
        using var f = new Fixture(); f.Second.Items.AddRange([(new("A", true), "show"), (new("B"), "show")]);
        await f.Run(); Assert.Contains("second:True", f.Trace);
        Assert.Equal(40, Assert.Single(f.Delays).Duration.TotalMilliseconds);
    }

    [Fact]
    public async Task PendingDelayKeepsCurrentLaterInspectionAndLiveSecondListButOriginalTotal()
    {
        using var f = Pair(); var b = f.Second.Items[0].Item;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); f.DelayReply = (_, _) => gate.Task;
        Task task = f.Run();
        try
        {
            Assert.False(task.IsCompleted); Assert.Equal("old", b.State);
            b.Extra = true; var added = new Cell("C"); f.Second.Items.Add((added, "late"));
            gate.SetResult(); await task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal("show", b.State); Assert.Equal("late", added.State); Assert.Contains("inspect:B:True", f.Trace);
            Assert.Single(f.Delays); Assert.Equal(1, f.Trace.Count(x => x == "count:first")); Assert.Equal(1, f.Trace.Count(x => x == "count:second"));
        }
        finally { gate.TrySetResult(); await task.WaitAsync(TimeSpan.FromSeconds(3)); }
    }

    [Fact]
    public async Task MutatingAnActivelyEnumeratedFirstListKeepsItsOriginalFailure()
    {
        using var f = Pair(); var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.DelayReply = (_, _) => gate.Task; Task task = f.Run();
        f.First.Items.Add((new("late"), "hide")); gate.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => task.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal("old", f.Second.Items[0].Item.State); Assert.Single(f.Delays);
    }

    [Fact]
    public async Task PreCancelledSourceDoesNotPreventTheFirstApplyBeforeActualDelayCancels()
    {
        using var f = Pair(); f.Source.Cancel(); Task task = f.Run();
        await task; Assert.Equal(TaskStatus.RanToCompletion, task.Status); Assert.True(f.Source.IsCancellationRequested);
        Assert.Equal("hide", f.First.Items[0].Item.State); Assert.Equal("old", f.Second.Items[0].Item.State);
        Assert.Equal("delay:10", f.Trace[^2]); Assert.Equal("finish", f.Trace.Last());
    }

    [Fact]
    public async Task PreCancelledSingleTransitionStillCompletesSuccessfully()
    {
        using var f = new Fixture(); f.First.Items.Add((new("A"), "new")); f.Source.Cancel();
        Task task = f.Run(); await task; Assert.Equal(TaskStatus.RanToCompletion, task.Status);
        Assert.Equal("new", f.First.Items[0].Item.State); Assert.Empty(f.Delays);
    }

    [Fact]
    public async Task DisposedSourceIsReadOnlyAfterApplyAndDelaySelectionWhenPauseIsNeeded()
    {
        using var f = Pair(); f.Source.Dispose(); Task task = f.Run();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => task);
        Assert.True(task.IsFaulted); Assert.Equal("first:False", f.Trace[^2]); Assert.Equal("finish", f.Trace.Last()); Assert.Empty(f.Delays);
        Assert.Equal("hide", f.First.Items[0].Item.State); Assert.Equal("old", f.Second.Items[0].Item.State);
    }

    [Theory]
    [InlineData("count:first")]
    [InlineData("count:second")]
    [InlineData("inspect:A:False")]
    [InlineData("apply:A:hide")]
    [InlineData("first:False")]
    [InlineData("delay:10")]
    [InlineData("inspect:B:False")]
    [InlineData("apply:B:show")]
    [InlineData("second:False")]
    public async Task SetupAndCallbackFailuresAreReturnedInTaskWithSameException(string step)
    {
        using var f = Pair(); f.Second.Items.Add((new("C"), "show"));
        var expected = new InvalidOperationException(step); f.OnStep = name => { if (name == step) throw expected; };
        Task? task = null; Assert.Null(Record.Exception(() => { task = f.Run(); }));
        Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(() => task!));
        Assert.Equal(step, f.Trace[^2]); Assert.Equal("finish", f.Trace.Last()); Assert.True(task!.IsFaulted);
        Assert.Null(Record.Exception(f.Source.Cancel)); // A borrowed source is never disposed by the utility.
    }

    [Fact]
    public async Task ArbitraryCancellationFromInspectionIsSwallowedAndFinalizedAsBefore()
    {
        using var f = Pair(); var expected = new OperationCanceledException(new CancellationToken(true));
        f.OnStep = name => { if (name == "inspect:A:False") throw expected; };
        Task task = f.Run(); await task;
        Assert.Equal(TaskStatus.RanToCompletion, task.Status); Assert.Equal("old", f.First.Items[0].Item.State);
        Assert.Equal(1, f.Trace.Count(x => x == "finish"));
    }

    [Fact]
    public async Task BorrowedSourceRemainsUsableAfterSuccess()
    {
        using var f = Pair(); await f.Run(); Assert.Null(Record.Exception(f.Source.Cancel)); Assert.True(f.Source.IsCancellationRequested);
    }

    [Fact]
    public async Task ZeroDurationStillCallsTheDelayPort()
    {
        using var f = Pair(); f.DurationOverride = TimeSpan.Zero; await f.Run();
        Assert.Equal(TimeSpan.Zero, Assert.Single(f.Delays).Duration); Assert.Equal("show", f.Second.Items[0].Item.State);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ZeroDurationStillReadsSourceAndRespectsDelayCancellation(bool disposed)
    {
        using var f = Pair(); f.DurationOverride = TimeSpan.Zero;
        if (disposed) f.Source.Dispose(); else f.Source.Cancel();
        Task task = f.Run();
        if (disposed)
        {
            await Assert.ThrowsAsync<ObjectDisposedException>(() => task); Assert.Empty(f.Delays);
        }
        else
        {
            await task; Assert.Equal(TaskStatus.RanToCompletion, task.Status); Assert.Equal(TimeSpan.Zero, Assert.Single(f.Delays).Duration);
        }
        Assert.Equal("hide", f.First.Items[0].Item.State); Assert.Equal("old", f.Second.Items[0].Item.State);
        Assert.Equal(1, f.Trace.Count(x => x == "finish"));
    }

    [Fact]
    public async Task ValueTypesAndPlainStatesNeedNoUiOrDomainTypes()
    {
        using var source = new CancellationTokenSource(); List<(int, bool)> applied = []; int finished = 0;
        await TwoPhaseTransitionSequence.RunAsync<int, bool>(new[] { (1, false) }, new[] { (2, true) },
            _ => false, (item, state) => applied.Add((item, state)), _ => TimeSpan.Zero, _ => TimeSpan.Zero,
            (_, _) => Task.CompletedTask, source, () => finished++);
        Assert.Equal([(1, false), (2, true)], applied); Assert.Equal(1, finished);
        Assert.Null(Record.Exception(source.Cancel));
    }

    [Fact]
    public async Task FinalizerFailureOverridesEarlierBodyFailureAndRunsExactlyOnce()
    {
        using var f = Pair(); var first = new InvalidOperationException("body"); var last = new InvalidOperationException("finalizer");
        f.OnStep = name => { if (name == "inspect:A:False") throw first; if (name == "finish") throw last; };
        Assert.Same(last, await Assert.ThrowsAsync<InvalidOperationException>(f.Run));
        Assert.Equal(1, f.Trace.Count(x => x == "finish"));
    }

    [Fact]
    public async Task CancellationRaisedByFinalizerIsNotSwallowed()
    {
        using var f = new Fixture(); var error = new OperationCanceledException(new CancellationToken(true));
        f.OnStep = name => { if (name == "finish") throw error; };
        Task task = f.Run(); Assert.Same(error, await Assert.ThrowsAsync<OperationCanceledException>(() => task));
        Assert.True(task.IsCanceled); Assert.Equal(1, f.Trace.Count(x => x == "finish"));
    }

    [Fact]
    public async Task FinalizationCompletesBeforeTheReturnedTaskCanBeObservedComplete()
    {
        using var f = Pair(); bool finalized = false;
        f.OnStep = name => { if (name == "finish") finalized = true; };
        Task task = f.Run(); Assert.True(task.IsCompletedSuccessfully); Assert.True(finalized); await task;
        Assert.Equal(1, f.Trace.Count(x => x == "finish"));
    }

    [Fact]
    public async Task ConcurrentCallsHaveIndependentIndicesAndCanCompleteInReverseOrder()
    {
        using var a = Pair(); using var b = Pair(); var one = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var two = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        a.DelayReply = (_, _) => one.Task; b.DelayReply = (_, _) => two.Task; Task first = a.Run(); Task second = b.Run();
        try
        {
            Assert.False(first.IsCompleted); Assert.False(second.IsCompleted);
            two.SetResult(); await second.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.False(first.IsCompleted); Assert.Equal("old", a.Second.Items[0].Item.State); Assert.Equal("show", b.Second.Items[0].Item.State);
            one.SetResult(); await first.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal("show", a.Second.Items[0].Item.State); Assert.Single(a.Delays); Assert.Single(b.Delays);
        }
        finally { one.TrySetResult(); two.TrySetResult(); await first.WaitAsync(TimeSpan.FromSeconds(3)); await second.WaitAsync(TimeSpan.FromSeconds(3)); }
    }

    private static Fixture Pair()
    {
        var f = new Fixture(); f.First.Items.Add((new("A"), "hide")); f.Second.Items.Add((new("B"), "show")); return f;
    }
    private sealed class Cell(string name, bool extra = false)
    {
        internal readonly string Name = name; internal bool Extra = extra; internal string State = "old";
    }
    private sealed class LoggedList<T>(string name, Action<string> step) : IReadOnlyList<T>
    {
        internal readonly List<T> Items = [];
        public int Count { get { step("count:" + name); return Items.Count; } }
        public T this[int index] => Items[index];
        public IEnumerator<T> GetEnumerator() => Items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed class Fixture : IDisposable
    {
        internal readonly List<string> Trace = [];
        internal readonly List<(TimeSpan Duration, CancellationToken Token)> Delays = [];
        internal readonly CancellationTokenSource Source = new();
        internal readonly LoggedList<(Cell Item, string State)> First, Second;
        internal Action<string>? OnStep;
        internal Func<TimeSpan, CancellationToken, Task>? DelayReply;
        internal TimeSpan? DurationOverride;
        internal Fixture() { First = new("first", Step); Second = new("second", Step); }
        private void Step(string value) { Trace.Add(value); OnStep?.Invoke(value); }
        internal Task Run() => TwoPhaseTransitionSequence.RunAsync(First, Second,
            item => { bool before = item.Extra; Step($"inspect:{item.Name}:{before}"); return before; },
            (item, state) => { Step($"apply:{item.Name}:{state}"); item.State = state; },
            before => { Step("first:" + before); return DurationOverride ?? TimeSpan.FromMilliseconds(before ? 30 : 10); },
            before => { Step("second:" + before); return DurationOverride ?? TimeSpan.FromMilliseconds(before ? 40 : 20); },
            (duration, token) => { Delays.Add((duration, token)); Step("delay:" + duration.TotalMilliseconds); return DelayReply?.Invoke(duration, token) ?? (token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask); }, Source, () => Step("finish"));
        public void Dispose() => Source.Dispose();
    }
}
