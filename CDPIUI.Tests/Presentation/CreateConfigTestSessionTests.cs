using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.ViewModels.CreateConfig;

namespace CDPIUI.Tests.Presentation;

public sealed class CreateConfigTestSessionTests
{
    [Fact]
    public void ConstructorStoresCallbacksAndInitialTextDoesNotNotify()
    {
        var f = new Fixture();
        Assert.Empty(f.Trace); Assert.Empty(f.Events);
        Assert.Null(f.Session.TestButtonText);
        Assert.Equal("\uE768", f.Session.TestButtonGlyph);
        Assert.False(f.Session.IsRunning); Assert.Empty(f.Session.CurrentRunId);
        f.Session.InitializeText("initial");
        f.Session.InitializeText(null!);
        Assert.Null(f.Session.TestButtonText);
        Assert.Empty(f.Trace); Assert.Empty(f.Events);
    }

    [Fact]
    public async Task StartSubscribesBeforeSelectionAndStopsBeforePreparingCurrentConfiguration()
    {
        var f = new Fixture();
        f.Session.InitializeText("initial");
        await f.Session.ToggleAsync();
        Assert.Equal(["remove", "add", "selection", "stop:component", "convert", "create", "args", "run:component:--test"], f.Trace);
        Assert.True(f.Session.IsRunning); Assert.Equal("component", f.Session.CurrentRunId);
        Assert.Equal(1, f.Tasks.SubscriberCount);
        Assert.Equal("initial", f.Session.TestButtonText);
        Assert.Empty(f.Events);
    }

    [Fact]
    public async Task SelectionIsReadAfterSubscriptionCallbacks()
    {
        var f = new Fixture();
        f.Tasks.OnAdd = () => f.HasComponent = false;
        await f.Session.ToggleAsync();
        Assert.Equal(["remove", "add", "selection", "localize:TestThis", "event:TestButtonText", "remove"], f.Trace);
        Assert.False(f.Session.IsRunning); Assert.Empty(f.Session.CurrentRunId);
        Assert.Equal(0, f.Tasks.SubscriberCount); Assert.Empty(f.Tasks.Stops);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task SelectedComponentWithNullOrEmptyIdIsStillStarted(string? id)
    {
        var f = new Fixture { ComponentId = id! };
        await f.Session.ToggleAsync();
        Assert.True(f.Session.IsRunning);
        Assert.Equal(id, f.Session.CurrentRunId);
        Assert.Equal(id, Assert.Single(f.Tasks.Stops));
        Assert.Equal(id, Assert.Single(f.Tasks.Runs).Id);
    }

    [Fact]
    public void StopAlwaysUnsubscribesEvenWithoutARunningSession()
    {
        var f = new Fixture();
        f.Session.Stop();
        Assert.Equal(["remove"], f.Trace);
        Assert.Empty(f.Tasks.Stops); Assert.Empty(f.Events);
    }

    [Fact]
    public async Task StopResetsBeforeFireAndForgetStopAndUsesTheOriginalLocalId()
    {
        var f = new Fixture();
        await f.Session.ToggleAsync();
        f.Apply("component", true);
        f.Trace.Clear(); f.Events.Clear();
        f.Tasks.StopAction = _ =>
        {
            Assert.False(f.Session.IsRunning); Assert.Empty(f.Session.CurrentRunId);
            return Task.CompletedTask;
        };
        f.Session.Stop();
        Assert.Equal(["remove", "event:TestButtonGlyph", "localize:TestThis", "event:TestButtonText", "remove", "stop:component"], f.Trace);
        Assert.Equal(["TestButtonGlyph", "TestButtonText"], f.Events);
        Assert.Equal(0, f.Tasks.SubscriberCount);
    }

    [Fact]
    public async Task EmptyCurrentIdPreventsStopResetDespiteRunningFlag()
    {
        var f = new Fixture { ComponentId = "" };
        await f.Session.ToggleAsync();
        f.Trace.Clear();
        f.Session.Stop();
        Assert.Equal(["remove"], f.Trace);
        Assert.True(f.Session.IsRunning); Assert.Empty(f.Session.CurrentRunId);
    }

    [Fact]
    public async Task AwaitedStopDefersPreparationAndKeepsOriginalRunIdWithCurrentConfiguration()
    {
        var f = new Fixture();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Tasks.StopAction = _ => gate.Task;
        var pending = f.Session.ToggleAsync();
        try
        {
            Assert.False(pending.IsCompleted); Assert.True(f.Session.IsRunning);
            Assert.DoesNotContain("convert", f.Trace);
            f.ComponentId = "new-selection";
            f.Create = () => { Assert.Equal("new-selection", f.ComponentId); return f.Config; };
        }
        finally { gate.TrySetResult(); }
        await pending.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal("component", Assert.Single(f.Tasks.Runs).Id);
    }

    [Fact]
    public async Task NullConfigurationResetsWithoutStarting()
    {
        var f = new Fixture { Create = () => null };
        await f.Session.ToggleAsync();
        Assert.False(f.Session.IsRunning); Assert.Empty(f.Session.CurrentRunId);
        Assert.Equal(0, f.Tasks.SubscriberCount); Assert.Empty(f.Tasks.Runs);
        Assert.Equal(["remove", "add", "selection", "stop:component", "convert", "create", "localize:TestThis", "event:TestButtonText", "remove"], f.Trace);
    }

    [Fact]
    public async Task RunTaskIsNotAwaited()
    {
        var f = new Fixture();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Tasks.RunAction = (_, _) => gate.Task;
        try
        {
            await f.Session.ToggleAsync().WaitAsync(TimeSpan.FromSeconds(3));
            Assert.False(gate.Task.IsCompleted); Assert.True(f.Session.IsRunning);
        }
        finally { gate.TrySetResult(); }
    }

    [Fact]
    public async Task ReturnedRunFaultIsNotTurnedIntoToggleFaultOrReset()
    {
        var f = new Fixture();
        var failure = Task.FromException(new IOException("run task"));
        f.Tasks.RunAction = (_, _) => failure;
        await f.Session.ToggleAsync();
        await Assert.ThrowsAsync<IOException>(() => failure);
        Assert.True(f.Session.IsRunning); Assert.Equal(1, f.Tasks.SubscriberCount);
    }

    [Fact]
    public async Task RunIdArgumentIsCapturedBeforeFormatterReentersStop()
    {
        var f = new Fixture();
        f.Format = _ => { f.Session.Stop(); return "formatted"; };
        await f.Session.ToggleAsync();
        Assert.Equal(("component", "formatted"), Assert.Single(f.Tasks.Runs));
        Assert.False(f.Session.IsRunning); Assert.Empty(f.Session.CurrentRunId);
    }

    [Fact]
    public async Task ConversionReentryChangesTheLaterReadRunId()
    {
        var f = new Fixture();
        f.Convert = () => f.Session.Stop();
        await f.Session.ToggleAsync();
        Assert.Equal("", Assert.Single(f.Tasks.Runs).Id);
        Assert.False(f.Session.IsRunning);
    }

    [Fact]
    public async Task EventsAreQueuedBeforeTheCurrentIdGuardIsEvaluated()
    {
        var f = new Fixture();
        f.Tasks.Emit("not-current", true);
        Assert.Empty(f.Queued);
        await f.Session.ToggleAsync();
        f.Tasks.Emit("component", true);
        Assert.Single(f.Queued);
        Assert.Equal("\uE768", f.Session.TestButtonGlyph);
        f.Session.Stop();
        f.ComponentId = "other";
        await f.Session.ToggleAsync();
        f.Trace.Clear();
        f.Queued[0]();
        Assert.Empty(f.Trace);
        Assert.Equal("other", f.Session.CurrentRunId);
    }

    [Theory]
    [InlineData("Component")]
    [InlineData("other")]
    public async Task StateMatchingRemainsCaseSensitive(string eventId)
    {
        var f = new Fixture();
        await f.Session.ToggleAsync();
        f.Trace.Clear();
        f.Apply(eventId, true);
        Assert.Empty(f.Trace); Assert.Equal("\uE768", f.Session.TestButtonGlyph);
    }

    [Fact]
    public async Task MatchingStateChangesGlyphThenLocalizedTextThenRunning()
    {
        var f = new Fixture();
        await f.Session.ToggleAsync();
        f.Apply("component", true);
        f.Trace.Clear(); f.Events.Clear();
        f.Observer = name => Assert.True(f.Session.IsRunning);
        f.Apply("component", false);
        Assert.Equal(["event:TestButtonGlyph", "localize:TestThis", "event:TestButtonText"], f.Trace);
        Assert.Equal(["TestButtonGlyph", "TestButtonText"], f.Events);
        Assert.False(f.Session.IsRunning);
        Assert.Equal("component", f.Session.CurrentRunId); Assert.Equal(1, f.Tasks.SubscriberCount);
    }

    [Fact]
    public async Task NaturalExitKeepsSubscriptionUntilNextToggleOrStop()
    {
        var f = new Fixture();
        await f.Session.ToggleAsync();
        f.Apply("component", false);
        Assert.Equal(1, f.Tasks.SubscriberCount);
        f.Trace.Clear();
        f.ComponentId = "next";
        await f.Session.ToggleAsync();
        Assert.Equal(1, f.Tasks.SubscriberCount);
        Assert.Equal("next", f.Session.CurrentRunId);
        Assert.Equal(["remove", "add", "selection", "stop:next", "convert", "create", "args", "run:next:--test"], f.Trace);
    }

    [Fact]
    public async Task StopAfterNaturalExitOnlyUnsubscribesAndKeepsId()
    {
        var f = new Fixture();
        await f.Session.ToggleAsync();
        f.Apply("component", false); f.Trace.Clear();
        f.Session.Stop();
        Assert.Equal(["remove"], f.Trace); Assert.Equal("component", f.Session.CurrentRunId);
    }

    [Fact]
    public async Task RejectedDispatchDoesNotApplyState()
    {
        var f = new Fixture { AcceptEnqueue = false };
        await f.Session.ToggleAsync();
        f.Tasks.Emit("component", true);
        Assert.Empty(f.Queued); Assert.Equal("\uE768", f.Session.TestButtonGlyph);
    }

    [Fact]
    public async Task StopBranchAwaitsBackendBeforeResettingState()
    {
        var f = new Fixture();
        await f.Session.ToggleAsync();
        f.Apply("component", true);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Tasks.StopAction = _ => gate.Task;
        var pending = f.Session.ToggleAsync();
        try
        {
            Assert.False(pending.IsCompleted);
            Assert.True(f.Session.IsRunning); Assert.Equal("\uE71A", f.Session.TestButtonGlyph);
            Assert.Equal(1, f.Tasks.SubscriberCount);
        }
        finally { gate.TrySetResult(); }
        await pending.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.False(f.Session.IsRunning); Assert.Empty(f.Session.CurrentRunId);
        Assert.Equal(0, f.Tasks.SubscriberCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AwaitedStopFaultPreservesPartialState(bool alreadyRunning)
    {
        var f = new Fixture();
        if (alreadyRunning) await f.Session.ToggleAsync();
        f.Tasks.StopAction = _ => Task.FromException(new IOException("stop"));
        await Assert.ThrowsAsync<IOException>(() => f.Session.ToggleAsync());
        Assert.True(f.Session.IsRunning); Assert.Equal("component", f.Session.CurrentRunId);
        Assert.Equal(1, f.Tasks.SubscriberCount);
    }

    [Theory]
    [InlineData("selection")]
    [InlineData("convert")]
    [InlineData("create")]
    [InlineData("args")]
    [InlineData("run")]
    public async Task SynchronousPreparationFaultDoesNotAddCleanupOrRollback(string stage)
    {
        var f = new Fixture();
        var error = new IOException(stage);
        if (stage == "selection") f.Select = () => throw error;
        if (stage == "convert") f.Convert = () => throw error;
        if (stage == "create") f.Create = () => throw error;
        if (stage == "args") f.Format = _ => throw error;
        if (stage == "run") f.Tasks.RunAction = (_, _) => throw error;
        Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => f.Session.ToggleAsync()));
        Assert.True(f.Session.IsRunning); Assert.Equal(1, f.Tasks.SubscriberCount);
        Assert.Equal(stage == "selection" ? "" : "component", f.Session.CurrentRunId);
    }

    [Fact]
    public async Task StopLocalizerFailureLeavesTheOriginalPartialReset()
    {
        var f = new Fixture();
        await f.Session.ToggleAsync(); f.Apply("component", true);
        f.Localize = _ => throw new IOException("locale");
        Assert.Throws<IOException>(() => f.Session.Stop());
        Assert.Equal("\uE768", f.Session.TestButtonGlyph);
        Assert.True(f.Session.IsRunning); Assert.Equal("component", f.Session.CurrentRunId);
        Assert.Equal(0, f.Tasks.SubscriberCount); Assert.Single(f.Tasks.Stops);
    }

    [Fact]
    public async Task ObserverFailureStopsDerivedStateAfterTheAssignedGlyph()
    {
        var f = new Fixture();
        await f.Session.ToggleAsync(); f.Apply("component", true);
        f.Observer = _ => throw new IOException("observer");
        Assert.Throws<IOException>(() => f.Apply("component", false));
        Assert.Equal("\uE768", f.Session.TestButtonGlyph);
        Assert.Equal("localized:StopTest", f.Session.TestButtonText);
        Assert.True(f.Session.IsRunning); Assert.Equal(1, f.Tasks.SubscriberCount);
    }

    [Fact]
    public async Task ReentrantStopDuringGlyphNotificationRetainsOriginalOuterContinuation()
    {
        var f = new Fixture();
        await f.Session.ToggleAsync();
        bool entered = false;
        f.Observer = name =>
        {
            if (name == "TestButtonGlyph" && !entered) { entered = true; f.Session.Stop(); }
        };
        f.Apply("component", true);
        Assert.True(f.Session.IsRunning); Assert.Empty(f.Session.CurrentRunId);
        Assert.Equal("\uE768", f.Session.TestButtonGlyph);
        Assert.Equal("localized:StopTest", f.Session.TestButtonText);
        Assert.Equal(0, f.Tasks.SubscriberCount);
    }

    [Fact]
    public async Task NullStateIsQueuedAndOnlyFailsWhenNonemptyCurrentIdIsInspected()
    {
        var f = new Fixture();
        await f.Session.ToggleAsync();
        f.Tasks.EmitRaw(null!);
        Assert.Single(f.Queued);
        Assert.Throws<NullReferenceException>(() => f.Queued[0]());
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Tasks = new(Trace);
            Session = new(Tasks,
                () => { Trace.Add("selection"); return Select?.Invoke() ?? (HasComponent, ComponentId); },
                () => { Trace.Add("convert"); Convert?.Invoke(); },
                () => { Trace.Add("create"); return Create is null ? Config : Create(); },
                item => { Assert.Same(Config, item); Trace.Add("args"); return Format?.Invoke(item) ?? "--test"; },
                key => { Trace.Add("localize:" + key); return Localize?.Invoke(key) ?? "localized:" + key; },
                action => { if (AcceptEnqueue) Queued.Add(action); });
            Session.PropertyChanged += (sender, e) =>
            {
                Assert.Same(Session, sender);
                Events.Add(e.PropertyName!); Trace.Add("event:" + e.PropertyName);
                Observer?.Invoke(e.PropertyName!);
            };
        }
        public bool HasComponent { get; set; } = true;
        public string ComponentId { get; set; } = "component";
        public bool AcceptEnqueue { get; init; } = true;
        public ConfigItem Config { get; } = new();
        public Func<(bool, string)>? Select { get; set; }
        public Action? Convert { get; set; }
        public Func<ConfigItem?>? Create { get; set; }
        public Func<ConfigItem, string>? Format { get; set; }
        public Func<string, string>? Localize { get; set; }
        public Action<string>? Observer { get; set; }
        public List<string> Trace { get; } = [];
        public List<string> Events { get; } = [];
        public List<Action> Queued { get; } = [];
        public TestTasks Tasks { get; }
        public CreateConfigTestSession Session { get; }
        public void Apply(string id, bool running) { Tasks.Emit(id, running); Queued[^1](); }
    }

    private sealed class TestTasks(List<string> trace) : ICreateConfigTestTasks
    {
        private Action<Tuple<string, bool>>? _updated;
        public event Action<Tuple<string, bool>>? StateUpdated
        {
            add { trace.Add("add"); _updated += value; OnAdd?.Invoke(); }
            remove { trace.Add("remove"); _updated -= value; }
        }
        public int SubscriberCount => _updated?.GetInvocationList().Length ?? 0;
        public Action? OnAdd { get; set; }
        public Func<string, Task>? StopAction { get; set; }
        public Func<string, string, Task>? RunAction { get; set; }
        public List<string> Stops { get; } = [];
        public List<(string Id, string Args)> Runs { get; } = [];
        public Task StopAsync(string id) { trace.Add("stop:" + id); Stops.Add(id); return StopAction?.Invoke(id) ?? Task.CompletedTask; }
        public Task RunAsync(string id, string args) { trace.Add("run:" + id + ":" + args); Runs.Add((id, args)); return RunAction?.Invoke(id, args) ?? Task.CompletedTask; }
        public void Emit(string id, bool running) => EmitRaw(Tuple.Create(id, running));
        public void EmitRaw(Tuple<string, bool> state) => _updated?.Invoke(state);
    }
}
