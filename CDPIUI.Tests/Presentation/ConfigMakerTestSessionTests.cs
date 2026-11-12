using CDPIUI.Helper.CreateConfigHelper;

namespace CDPIUI.Tests.Presentation;

public class ConfigMakerTestSessionTests
{
    [Fact]
    public async Task StartPublishesStateBeforeCheckingInstallationAndRunningTheTest()
    {
        var fixture = new SessionFixture();

        await fixture.Session.StartAsync("--test");

        Assert.True(fixture.Session.IsTesting);
        Assert.Equal("component", fixture.Session.TestComponentId);
        Assert.Equal(["running:component", "stop:component", "state:True", "installed:component", "notice:Started",
            "run:component:--test", "refresh"], fixture.Trace);
    }

    [Fact]
    public async Task DecliningReplacementKeepsTheExistingComponentUntouched()
    {
        var fixture = new SessionFixture { ConfirmResult = false };
        fixture.Tasks.WasRunning = true;

        await fixture.Session.StartAsync("--test");

        Assert.False(fixture.Session.IsTesting);
        Assert.Equal(["running:component", "confirm"], fixture.Trace);
        Assert.Equal(0, fixture.Tasks.SubscriberCount);
    }

    [Fact]
    public async Task StopRestoresThePreviousConfigurationWithoutTestArguments()
    {
        var fixture = new SessionFixture();
        fixture.Tasks.WasRunning = true;
        await fixture.Session.StartAsync("--test");
        fixture.Trace.Clear();
        fixture.ComponentId = "different-selection";

        await fixture.Session.StopAsync(showCompletionMessage: true);

        Assert.False(fixture.Session.IsTesting);
        Assert.Empty(fixture.Session.TestComponentId);
        Assert.Equal(["state:False", "stop:component", "restore:component", "notice:Stopped"], fixture.Trace);
        Assert.Equal(0, fixture.Tasks.SubscriberCount);
    }

    [Fact]
    public async Task MissingInstallationClearsTheTestStateWithoutLaunching()
    {
        var fixture = new SessionFixture { IsInstalled = false };

        await fixture.Session.StartAsync("--test");

        Assert.False(fixture.Session.IsTesting);
        Assert.Equal(["running:component", "stop:component", "state:True", "installed:component", "notice:NotInstalled",
            "state:False", "stop:component"], fixture.Trace);
        Assert.Equal(0, fixture.Tasks.SubscriberCount);
    }

    [Fact]
    public async Task FailedTestLaunchShowsTheErrorAndRestoresThePreviousRun()
    {
        var fixture = new SessionFixture();
        fixture.Tasks.WasRunning = true;
        fixture.Tasks.Run = (_, _) => Task.FromException(new IOException("launch failed"));

        await fixture.Session.StartAsync("--test");

        Assert.False(fixture.Session.IsTesting);
        Assert.Equal((ConfigMakerTestNotice.StartFailed, "launch failed"), fixture.Notices.Single(notice => notice.Kind == ConfigMakerTestNotice.StartFailed));
        Assert.Equal("restore:component", fixture.Trace[^1]);
        Assert.Equal(0, fixture.Tasks.SubscriberCount);
    }

    [Fact]
    public async Task OutputRefreshFailureUsesTheExistingLaunchFailureBoundary()
    {
        var fixture = new SessionFixture { Refresh = () => Task.FromException(new IOException("refresh failed")) };

        await fixture.Session.StartAsync("--test");

        Assert.False(fixture.Session.IsTesting);
        Assert.Contains((ConfigMakerTestNotice.StartFailed, "refresh failed"), fixture.Notices);
        Assert.Equal("stop:component", fixture.Trace[^1]);
    }

    [Fact]
    public async Task StateChangesDuringStartupAreIgnoredUntilRunCompletes()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fixture = new SessionFixture();
        fixture.Tasks.Run = (_, _) => completion.Task;
        Task starting = fixture.Session.StartAsync("--test");

        fixture.Tasks.Emit("component", isRunning: false);
        Assert.Empty(fixture.Queued);
        completion.SetResult();
        await starting;
        fixture.Tasks.Emit("component", isRunning: false);
        Assert.Single(fixture.Queued);
    }

    [Theory]
    [InlineData("COMPONENT", false, 1)]
    [InlineData("other", false, 0)]
    [InlineData("component", true, 0)]
    public async Task OnlyAnExitOfTheMatchingComponentQueuesCompletion(string component, bool running, int expected)
    {
        var fixture = new SessionFixture();
        await fixture.Session.StartAsync("--test");

        fixture.Tasks.Emit(component, running);

        Assert.Equal(expected, fixture.Queued.Count);
        Assert.True(fixture.Session.IsTesting);
    }

    [Fact]
    public async Task QueuedExitReportsBeforeStoppingAndRestoring()
    {
        var fixture = new SessionFixture();
        fixture.Tasks.WasRunning = true;
        await fixture.Session.StartAsync("--test");
        fixture.Trace.Clear();
        fixture.Tasks.Emit("component", false);

        await Assert.Single(fixture.Queued)();

        Assert.Equal(["notice:Exited", "state:False", "stop:component", "restore:component"], fixture.Trace);
    }

    [Fact]
    public async Task StoppingAnIdleSessionDoesNotReportOrCallTheManager()
    {
        var fixture = new SessionFixture();

        await fixture.Session.StopAsync(showCompletionMessage: true);

        Assert.Empty(fixture.Trace);
    }

    [Fact]
    public async Task RepeatedStopDoesNotRestoreOrReportTwice()
    {
        var fixture = new SessionFixture();
        fixture.Tasks.WasRunning = true;
        await fixture.Session.StartAsync("--test");
        await fixture.Session.StopAsync(showCompletionMessage: true);
        fixture.Trace.Clear();

        await fixture.Session.StopAsync(showCompletionMessage: true);
        fixture.Tasks.Emit("component", false);

        Assert.Empty(fixture.Trace);
        Assert.Empty(fixture.Queued);
    }

    [Fact]
    public async Task SelectedComponentIsReadAgainAfterTheInitialStopAwaits()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fixture = new SessionFixture();
        fixture.Tasks.Stop = _ => completion.Task;
        Task starting = fixture.Session.StartAsync("--resolved-earlier");

        fixture.ComponentId = "after-await";
        completion.SetResult();
        await starting;

        Assert.Contains("stop:component", fixture.Trace);
        Assert.Contains("run:after-await:--resolved-earlier", fixture.Trace);
        Assert.Equal("after-await", fixture.Session.TestComponentId);
    }

    [Fact]
    public async Task StopFailurePropagatesAfterStateAndSubscriptionAreReleased()
    {
        var fixture = new SessionFixture();
        await fixture.Session.StartAsync("--test");
        fixture.Tasks.Stop = _ => Task.FromException(new IOException("stop failed"));

        await Assert.ThrowsAsync<IOException>(() => fixture.Session.StopAsync());

        Assert.False(fixture.Session.IsTesting);
        Assert.Equal(0, fixture.Tasks.SubscriberCount);
        Assert.Equal("component", fixture.Session.TestComponentId);
        Assert.DoesNotContain(fixture.Notices, notice => notice.Kind == ConfigMakerTestNotice.StartFailed);
    }

    [Fact]
    public async Task ConfirmationFailureRemainsOutsideTheLaunchErrorBoundary()
    {
        var fixture = new SessionFixture { Confirm = () => Task.FromException<bool>(new IOException("confirm failed")) };
        fixture.Tasks.WasRunning = true;

        await Assert.ThrowsAsync<IOException>(() => fixture.Session.StartAsync("--test"));

        Assert.False(fixture.Session.IsTesting);
        Assert.Empty(fixture.Notices);
        Assert.DoesNotContain(fixture.Trace, item => item.StartsWith("stop:", StringComparison.Ordinal));
    }

    private sealed class SessionFixture
    {
        public SessionFixture()
        {
            Tasks = new(Trace);
            Session = new(Tasks, () => ComponentId,
                () => { Trace.Add("confirm"); return Confirm?.Invoke() ?? Task.FromResult(ConfirmResult); },
                id => { Trace.Add("installed:" + id); return IsInstalled; },
                (kind, details) => { Trace.Add("notice:" + kind); Notices.Add((kind, details)); },
                () => { Trace.Add("refresh"); return Refresh?.Invoke() ?? Task.CompletedTask; },
                action => Queued.Add(action));
            Session.StateChanged += (_, _) => Trace.Add("state:" + Session.IsTesting);
        }

        public string ComponentId { get; set; } = "component";
        public bool ConfirmResult { get; init; } = true;
        public bool IsInstalled { get; init; } = true;
        public Func<Task<bool>>? Confirm { get; init; }
        public Func<Task>? Refresh { get; init; }
        public List<string> Trace { get; } = [];
        public List<(ConfigMakerTestNotice Kind, string? Details)> Notices { get; } = [];
        public List<Func<Task>> Queued { get; } = [];
        public TestTasks Tasks { get; }
        public ConfigMakerTestSession Session { get; }
    }

    private sealed class TestTasks(List<string> trace) : IConfigMakerTestTasks
    {
        public event Action<Tuple<string, bool>>? StateUpdated;
        public int SubscriberCount => StateUpdated?.GetInvocationList().Length ?? 0;
        public bool WasRunning { get; set; }
        public Func<string, Task>? Stop { get; set; }
        public Func<string, string, Task>? Run { get; set; }
        public Task<bool> IsRunningAsync(string id) { trace.Add("running:" + id); return Task.FromResult(WasRunning); }
        public Task StopAsync(string id) { trace.Add("stop:" + id); return Stop?.Invoke(id) ?? Task.CompletedTask; }
        public Task RunAsync(string id, string args) { trace.Add("run:" + id + ":" + args); return Run?.Invoke(id, args) ?? Task.CompletedTask; }
        public Task RestoreAsync(string id) { trace.Add("restore:" + id); return Task.CompletedTask; }
        public void Emit(string id, bool isRunning) => StateUpdated?.Invoke(Tuple.Create(id, isRunning));
    }
}
