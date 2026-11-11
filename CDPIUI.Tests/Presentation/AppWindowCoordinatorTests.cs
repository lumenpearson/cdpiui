using CDPIUI.Helper.WindowHelper;

namespace CDPIUI.Tests.Presentation;

public class AppWindowCoordinatorTests
{
    [Fact]
    public void ConstructionDoesNotReadReadinessOrWindowState()
    {
        var fixture = new WindowFixture();

        Assert.Empty(fixture.Trace);
        Assert.Equal(0, fixture.ReadyReads);
    }

    [Fact]
    public async Task SafeCreationWaitsForReadinessThenUsesTheLiveWindowList()
    {
        var fixture = new WindowFixture();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Ready = ready.Task;

        var creation = fixture.Coordinator.CreateSafeAsync(typeof(TestWindow), true);
        Assert.False(creation.IsCompleted);
        Assert.Empty(fixture.Trace);
        var window = new TestWindow("late");
        fixture.Windows.Add(window);
        ready.SetResult();

        Assert.Same(window, await creation);
        Assert.Equal(["live:late", "activate:late"], fixture.Trace);
    }

    [Fact]
    public async Task EachCreationReadsTheCurrentReadinessTask()
    {
        var fixture = new WindowFixture();
        fixture.Windows.Add(new TestWindow("existing"));
        await fixture.Coordinator.CreateSafeAsync(typeof(TestWindow), false);
        fixture.Trace.Clear();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Ready = ready.Task;

        var creation = fixture.Coordinator.CreateSafeAsync(typeof(TestWindow), false);

        Assert.False(creation.IsCompleted);
        Assert.Equal(2, fixture.ReadyReads);
        Assert.Empty(fixture.Trace);
        ready.SetResult();
        await creation;
    }

    [Fact]
    public async Task AReadinessFailureDoesNotTouchWindows()
    {
        var fixture = new WindowFixture { Ready = Task.FromException(new IOException("not ready")) };

        await Assert.ThrowsAsync<IOException>(() => fixture.Coordinator.CreateUnsafeAsync(typeof(TestWindow), true, "id"));

        Assert.Empty(fixture.Trace);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SafeCreationReusesTheOnlyLiveWindow(bool activate)
    {
        var fixture = new WindowFixture();
        var window = new TestWindow("one");
        fixture.Windows.Add(window);

        Assert.Same(window, await fixture.Coordinator.CreateSafeAsync(typeof(TestWindow), activate));

        Assert.Equal(activate ? ["live:one", "activate:one"] : new[] { "live:one" }, fixture.Trace);
        Assert.Same(window, Assert.Single(fixture.Windows));
    }

    [Fact]
    public async Task ADeadWindowIsClosedAndRemovedBeforeCreatingAndRegisteringItsReplacement()
    {
        var fixture = new WindowFixture();
        fixture.Windows.Add(new TestWindow("dead") { HasDispatcher = false });

        var window = await fixture.Coordinator.CreateSafeAsync(typeof(TestWindow), true);

        Assert.Same(fixture.Created, window);
        Assert.Same(window, Assert.Single(fixture.Windows));
        Assert.Equal(["live:dead", "close:dead", "create", "position:created", "register:created:False", "activate:created"], fixture.Trace);
    }

    [Fact]
    public async Task DuplicateWindowsAreReplacedFromASnapshotEvenIfClosingAddsAnotherWindow()
    {
        var fixture = new WindowFixture();
        fixture.Windows.AddRange([new TestWindow("first"), new TestWindow("second")]);
        var late = new TestWindow("late");
        fixture.AfterClose = window => { if (window.Name == "first") fixture.Windows.Add(late); };

        await fixture.Coordinator.CreateSafeAsync(typeof(TestWindow), false);

        Assert.Equal(["live:first", "close:first", "close:second", "create", "position:created", "register:created:False"], fixture.Trace);
        Assert.Equal([late, fixture.Created], fixture.Windows);
    }

    [Fact]
    public async Task ANullTypeWithNoWindowsReachesTheFactoryAndPreservesItsArgumentException()
    {
        var fixture = new WindowFixture { CreateError = new ArgumentNullException("type") };

        await Assert.ThrowsAsync<ArgumentNullException>(() => fixture.Coordinator.CreateSafeAsync(null!, true));
        Assert.Equal(["create"], fixture.Trace);
        fixture.Trace.Clear();
        await Assert.ThrowsAsync<ArgumentNullException>(() => fixture.Coordinator.CreateUnsafeAsync(null!, true, "id"));
        Assert.Equal(["create"], fixture.Trace);
    }

    [Fact]
    public async Task CreationFailurePropagatesAfterOldWindowsHaveBeenRemoved()
    {
        var fixture = new WindowFixture();
        fixture.Windows.Add(new TestWindow("dead") { HasDispatcher = false });
        fixture.CreateError = new IOException("create failed");

        await Assert.ThrowsAsync<IOException>(() => fixture.Coordinator.CreateSafeAsync(typeof(TestWindow), true));

        Assert.Empty(fixture.Windows);
        Assert.Equal(["live:dead", "close:dead", "create"], fixture.Trace);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PositionOrRegistrationFailureDoesNotActivateTheNewWindow(bool registerFailure)
    {
        var fixture = new WindowFixture();
        if (registerFailure) fixture.RegisterError = new IOException("register failed");
        else fixture.PositionError = new IOException("position failed");

        await Assert.ThrowsAsync<IOException>(() => fixture.Coordinator.CreateSafeAsync(typeof(TestWindow), true));

        Assert.DoesNotContain(fixture.Trace, item => item.StartsWith("activate:", StringComparison.Ordinal));
        Assert.Empty(fixture.Windows);
    }

    [Fact]
    public async Task UnsafeReuseIgnoresDispatcherAndMatchesTheFirstNonEmptyTemplateIdIgnoringCase()
    {
        var fixture = new WindowFixture();
        fixture.Windows.Add(new TestWindow("plain"));
        var first = new TemplateTestWindow("first", "ID") { HasDispatcher = false };
        fixture.Windows.AddRange([first, new TemplateTestWindow("second", "id")]);

        Assert.Same(first, await fixture.Coordinator.CreateUnsafeAsync(typeof(TestWindow), true, "id"));

        Assert.Equal(["id:plain", "id:first", "activate:first"], fixture.Trace);
        Assert.Equal(3, fixture.Windows.Count);
    }

    [Fact]
    public async Task UnsafeCreationWithAnEmptyIdAlwaysCreatesAnotherTemplate()
    {
        var fixture = new WindowFixture();
        var existing = new TemplateTestWindow("empty", "");
        fixture.Windows.Add(existing);

        var created = await fixture.Coordinator.CreateUnsafeAsync(typeof(TemplateTestWindow), false, "");

        Assert.Same(fixture.Created, created);
        Assert.Equal("", fixture.Created.Id);
        Assert.Equal([existing, fixture.Created], fixture.Windows);
        Assert.Equal(["id:empty", "create", "set-id:created:", "position:created", "register:created:True"], fixture.Trace);
    }

    [Fact]
    public async Task UnsafeCreationSetsIdBeforePositionRegistrationAndActivationWithoutClosingOtherIds()
    {
        var fixture = new WindowFixture();
        fixture.Windows.Add(new TemplateTestWindow("other", "other-id"));

        await fixture.Coordinator.CreateUnsafeAsync(typeof(TemplateTestWindow), true, "new-id");

        Assert.Equal("new-id", fixture.Created.Id);
        Assert.Equal(["id:other", "create", "set-id:created:new-id", "position:created", "register:created:True", "activate:created"], fixture.Trace);
        Assert.Equal(2, fixture.Windows.Count);
    }

    [Fact]
    public async Task UnsafeCreationAlsoWaitsBeforeTakingItsWindowSnapshot()
    {
        var fixture = new WindowFixture();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Ready = ready.Task;
        var creation = fixture.Coordinator.CreateUnsafeAsync(typeof(TemplateTestWindow), false, "id");
        var late = new TemplateTestWindow("late", "id");
        fixture.Windows.Add(late);
        ready.SetResult();

        Assert.Same(late, await creation);
        Assert.Equal(["id:late"], fixture.Trace);
    }

    [Fact]
    public void GetCurrentSelectsTheFirstLiveAssignableWindowAndSkipsNulls()
    {
        var fixture = new WindowFixture();
        var live = new TemplateTestWindow("live", "id");
        fixture.Windows.AddRange([null!, new TestWindow("dead") { HasDispatcher = false }, live]);

        Assert.Same(live, fixture.Coordinator.GetCurrent(typeof(TestWindow)));

        Assert.Equal(["live:dead", "live:live"], fixture.Trace);
    }

    [Fact]
    public void CloseAllClosesAndRemovesOnlyItsOriginalTypeSnapshot()
    {
        var fixture = new WindowFixture();
        var other = new TestWindow("other");
        var first = new TemplateTestWindow("first", "1");
        var late = new TemplateTestWindow("late", "2");
        fixture.Windows.AddRange([other, first]);
        fixture.AfterClose = _ => { fixture.Windows.Remove(first); fixture.Windows.Add(late); };

        fixture.Coordinator.CloseAll(typeof(TemplateTestWindow));

        Assert.Equal(["close:first"], fixture.Trace);
        Assert.Equal([other, late], fixture.Windows);
    }

    [Theory]
    [InlineData("id", "ID")]
    [InlineData("", "")]
    public void CloseTemplateUsesTheFirstMatchingIdAndLeavesListRemovalToTheCloseEvent(string id, string stored)
    {
        var fixture = new WindowFixture();
        fixture.Windows.AddRange([new TemplateTestWindow("first", stored), new TemplateTestWindow("second", stored)]);

        fixture.Coordinator.CloseTemplate(typeof(TemplateTestWindow), id);

        Assert.Equal(["id:first", "close:first"], fixture.Trace);
        Assert.Equal(2, fixture.Windows.Count);
    }

    [Fact]
    public void ACloseFailureStopsBeforeExplicitRemovalOrFurtherCloses()
    {
        var fixture = new WindowFixture();
        fixture.Windows.AddRange([new TestWindow("first"), new TestWindow("second")]);
        fixture.CloseError = new IOException("close failed");

        Assert.Throws<IOException>(() => fixture.Coordinator.CloseAll(typeof(TestWindow)));

        Assert.Equal(["close:first"], fixture.Trace);
        Assert.Equal(2, fixture.Windows.Count);
    }

    private class TestWindow(string name)
    {
        public string Name { get; } = name;
        public bool HasDispatcher { get; set; } = true;
    }

    private sealed class TemplateTestWindow(string name, string id) : TestWindow(name)
    {
        public string Id { get; set; } = id;
    }

    private sealed class WindowFixture : IAppWindowOperations<TestWindow>
    {
        public WindowFixture() => Coordinator = new(Windows, () => { ReadyReads++; return Ready; }, this);
        public List<TestWindow> Windows { get; } = [];
        public List<string> Trace { get; } = [];
        public Task Ready { get; set; } = Task.CompletedTask;
        public int ReadyReads { get; private set; }
        public TemplateTestWindow Created { get; } = new("created", "");
        public Action<TestWindow>? AfterClose { get; set; }
        public Exception? CreateError { get; set; }
        public Exception? PositionError { get; set; }
        public Exception? RegisterError { get; set; }
        public Exception? CloseError { get; set; }
        public AppWindowCoordinator<TestWindow> Coordinator { get; }

        public bool HasDispatcher(TestWindow window) { Trace.Add("live:" + window.Name); return window.HasDispatcher; }
        public bool TryGetTemplateId(TestWindow window, out string? id)
        {
            Trace.Add("id:" + window.Name);
            id = (window as TemplateTestWindow)?.Id;
            return window is TemplateTestWindow;
        }
        public TestWindow Create(Type windowType) { Trace.Add("create"); if (CreateError != null) throw CreateError; return Created; }
        public void SetTemplateId(TestWindow window, string id) { Trace.Add($"set-id:{window.Name}:{id}"); ((TemplateTestWindow)window).Id = id; }
        public void SetPosition(TestWindow window) { Trace.Add("position:" + window.Name); if (PositionError != null) throw PositionError; }
        public void Register(TestWindow window, bool isUnsafe) { Trace.Add($"register:{window.Name}:{isUnsafe}"); if (RegisterError != null) throw RegisterError; Windows.Add(window); }
        public void Activate(TestWindow window) => Trace.Add("activate:" + window.Name);
        public void Close(TestWindow window) { Trace.Add("close:" + window.Name); if (CloseError != null) throw CloseError; AfterClose?.Invoke(window); }
    }
}
