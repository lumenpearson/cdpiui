using CDPIUI.Helper.Basic;

namespace CDPIUI.Tests.Presentation;

public class AppActivationDispatcherTests
{
    [Fact]
    public async Task StartupPreservesMigrationFilesProtocolAndDirectOrder()
    {
        var fixture = new ActivationFixture();
        string[] arguments = ["app.exe", "file.cdpiconfig", "----ms-protocol:cdpiui://public", "--direct:direct-payload"];
        fixture.DirectResult = true;

        bool handled = await fixture.Dispatcher.ProcessStartupAsync(arguments, _ => new());

        Assert.True(handled);
        Assert.Equal(["migration:argv", "files:app.exe|file.cdpiconfig|----ms-protocol:cdpiui://public|--direct:direct-payload",
            "protocol:cdpiui://public", "direct:direct-payload"], fixture.Calls);
    }

    [Fact]
    public async Task InitialFilesSuppressCommandLineFilesButNotProtocolOrDirect()
    {
        var fixture = new ActivationFixture { FileResult = true, DirectResult = true };

        bool handled = await fixture.Dispatcher.ProcessStartupAsync(
            ["argv-file", "----ms-protocol:cdpiui://fallback", "--direct:payload"],
            kind => kind == AppActivationKind.File ? new(kind, Files: ["initial-file"]) : new());

        Assert.True(handled);
        Assert.Equal(["migration:argv", "files:initial-file", "protocol:cdpiui://fallback", "direct:payload"], fixture.Calls);
    }

    [Fact]
    public async Task SuccessfulInitialProtocolSuppressesOnlyTheProtocolFallback()
    {
        var fixture = new ActivationFixture { ProtocolResult = true, DirectResult = true };

        await fixture.Dispatcher.ProcessStartupAsync(["----ms-protocol:ignored", "--direct:payload"],
            kind => kind == AppActivationKind.Protocol ? new(kind, ProtocolUri: "cdpiui://initial") : new());

        Assert.Equal(["migration:argv", "protocol:cdpiui://initial", "files:----ms-protocol:ignored|--direct:payload",
            "direct:payload"], fixture.Calls);
    }

    [Fact]
    public async Task FailedDirectResultStillOverridesTheEarlierProtocolResultAtStartup()
    {
        var fixture = new ActivationFixture { ProtocolResult = true, DirectResult = false };

        bool handled = await fixture.Dispatcher.ProcessStartupAsync(["----ms-protocol:uri", "--direct:payload"], _ => new());

        Assert.False(handled);
    }

    [Fact]
    public async Task InitialLaunchMigrationIsTriedOnlyWhenArgvMigrationWasNotHandled()
    {
        var fixture = new ActivationFixture { LaunchMigrationResult = true };

        bool handled = await fixture.Dispatcher.ProcessStartupAsync([], kind =>
            kind == AppActivationKind.Launch ? new(kind, Arguments: "migration-data") : new());

        Assert.True(handled);
        Assert.Equal(["migration:argv", "migration:launch:migration-data", "files:"], fixture.Calls);
    }

    [Fact]
    public async Task StartupRetainsPartialSuccessWhenLaterProcessingThrows()
    {
        var fixture = new ActivationFixture { ArgvMigrationResult = true, ThrowOnFiles = true };

        bool handled = await fixture.Dispatcher.ProcessStartupAsync(["file"], _ => new());

        Assert.True(handled);
        Assert.Equal(["migration:argv", "files:file", "startup-error:files failed"], fixture.Calls);
    }

    [Fact]
    public async Task StartupWaitsForMigrationBeforeReadingInitialActivation()
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        List<AppActivationKind> initialReads = [];
        var fixture = new ActivationFixture { ArgvMigrationCompletion = completion.Task };
        Task<bool> running = fixture.Dispatcher.ProcessStartupAsync([], kind => { initialReads.Add(kind); return new(); });

        Assert.Equal(["migration:argv"], fixture.Calls);
        Assert.Empty(initialReads);
        completion.SetResult(false);
        await running;
        Assert.Equal([AppActivationKind.Launch, AppActivationKind.File, AppActivationKind.Protocol], initialReads);
    }

    [Fact]
    public async Task RedirectedProtocolDoesNotProcessFilesOrLaunchArguments()
    {
        var fixture = new ActivationFixture { ProtocolResult = true };

        bool handled = await fixture.Dispatcher.ProcessRedirectedAsync(new(AppActivationKind.Protocol, ProtocolUri: "cdpiui://public?a=b%26c"));

        Assert.True(handled);
        Assert.Equal(["protocol:cdpiui://public?a=b%26c"], fixture.Calls);
    }

    [Fact]
    public async Task RedirectedFilesArePassedThroughInOrder()
    {
        var fixture = new ActivationFixture { FileResult = true };

        Assert.True(await fixture.Dispatcher.ProcessRedirectedAsync(new(AppActivationKind.File, Files: ["one", "two"])));
        Assert.Equal(["files:one|two"], fixture.Calls);
    }

    [Fact]
    public async Task RedirectedLaunchPreservesMigrationSplitDirectAndFileFallback()
    {
        var fixture = new ActivationFixture { SplitArguments = ["--direct:payload", "file"], FileResult = true };

        Assert.True(await fixture.Dispatcher.ProcessRedirectedAsync(new(AppActivationKind.Launch, Arguments: "raw-launch")));
        Assert.Equal(["migration:launch:raw-launch", "split:raw-launch", "direct:payload", "gui-direct:payload", "files:--direct:payload|file"], fixture.Calls);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task RedirectedLaunchSkipsFileFallbackAfterMigrationOrDirectSuccess(bool migration, bool direct)
    {
        var fixture = new ActivationFixture
        {
            LaunchMigrationResult = migration, DirectResult = direct, SplitArguments = ["--direct:payload", "file"],
        };

        Assert.True(await fixture.Dispatcher.ProcessRedirectedAsync(new(AppActivationKind.Launch, Arguments: "raw")));
        Assert.Equal(migration
            ? ["migration:launch:raw", "split:raw"]
            : new[] { "migration:launch:raw", "split:raw", "direct:payload" }, fixture.Calls);
    }

    [Fact]
    public async Task RedirectedExceptionsPropagateToTheExistingAppErrorBoundary()
    {
        var fixture = new ActivationFixture { ThrowOnFiles = true };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Dispatcher.ProcessRedirectedAsync(new(AppActivationKind.File, Files: ["file"])));
        Assert.Equal(["files:file"], fixture.Calls);
    }

    [Fact]
    public async Task DirectPrefixRemainsCaseSensitive()
    {
        var fixture = new ActivationFixture();

        Assert.False(await fixture.Dispatcher.ProcessStartupAsync(["--DIRECT:payload"], _ => new()));
        Assert.Equal(["migration:argv", "files:--DIRECT:payload"], fixture.Calls);
    }

    [Fact]
    public async Task StartupClearsProtocolSuccessBeforeAFailingGuiDirectFallback()
    {
        var fixture = new ActivationFixture { ProtocolResult = true, ThrowOnGuiDirect = true };

        bool handled = await fixture.Dispatcher.ProcessStartupAsync(["----ms-protocol:uri", "--direct:payload"], _ => new());

        Assert.False(handled);
        Assert.Equal("startup-error:GUI direct failed", fixture.Calls[^1]);
    }

    [Fact]
    public async Task AlreadyHandledArgvMigrationSkipsOnlyTheInitialLaunchRead()
    {
        var fixture = new ActivationFixture { ArgvMigrationResult = true };
        List<AppActivationKind> reads = [];

        await fixture.Dispatcher.ProcessStartupAsync([], kind => { reads.Add(kind); return new(); });

        Assert.Equal([AppActivationKind.File, AppActivationKind.Protocol], reads);
    }

    [Fact]
    public async Task RedirectedLaunchReadsArgumentsAgainAfterMigrationCompletes()
    {
        string arguments = "before";
        var calls = new List<string>();
        var dispatcher = new AppActivationDispatcher(
            _ => Task.FromResult(false),
            value => { calls.Add("migration:" + value); arguments = "after"; return Task.FromResult(false); },
            _ => Task.FromResult(false), _ => Task.FromResult(false), _ => Task.FromResult(false), _ => false,
            value => { calls.Add("split:" + value); return []; }, _ => { });

        await dispatcher.ProcessRedirectedAsync(new(AppActivationKind.Launch,
            ReadLaunchArguments: () => arguments));

        Assert.Equal(["migration:before", "split:after"], calls);
    }

    private sealed class ActivationFixture
    {
        public List<string> Calls { get; } = [];
        public bool ArgvMigrationResult { get; init; }
        public bool LaunchMigrationResult { get; init; }
        public bool FileResult { get; init; }
        public bool ProtocolResult { get; init; }
        public bool DirectResult { get; set; }
        public bool GuiDirectResult { get; init; }
        public bool ThrowOnFiles { get; init; }
        public bool ThrowOnGuiDirect { get; init; }
        public Task<bool>? ArgvMigrationCompletion { get; init; }
        public string[] SplitArguments { get; init; } = [];
        public AppActivationDispatcher Dispatcher => new(
            _ => { Calls.Add("migration:argv"); return ArgvMigrationCompletion ?? Task.FromResult(ArgvMigrationResult); },
            value => { Calls.Add("migration:launch:" + value); return Task.FromResult(LaunchMigrationResult); },
            files =>
            {
                Calls.Add("files:" + string.Join('|', files));
                return ThrowOnFiles ? throw new InvalidOperationException("files failed") : Task.FromResult(FileResult);
            },
            value => { Calls.Add("protocol:" + value); return Task.FromResult(ProtocolResult); },
            value => { Calls.Add("direct:" + value); return Task.FromResult(DirectResult); },
            value =>
            {
                Calls.Add("gui-direct:" + value);
                if (ThrowOnGuiDirect) throw new InvalidOperationException("GUI direct failed");
                return GuiDirectResult;
            },
            value => { Calls.Add("split:" + value); return SplitArguments; },
            error => Calls.Add("startup-error:" + error.Message));
    }
}
