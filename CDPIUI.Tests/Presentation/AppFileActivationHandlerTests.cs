using CDPIUI.Helper.Basic;

namespace CDPIUI.Tests.Presentation;

public sealed class AppFileActivationHandlerTests
{
    [Fact]
    public void ConstructionDoesNotCallAnyOperation()
    {
        var f = new Fixture();
        Assert.Empty(f.Calls);
    }

    [Fact]
    public async Task EmptyInputIsNotHandled()
    {
        var f = new Fixture();
        Assert.False(await f.Handler.ProcessFiles([]));
        Assert.Empty(f.Calls);
    }

    [Fact]
    public async Task NullInputRemainsAFaultedTaskRatherThanASynchronousThrow()
    {
        var f = new Fixture();
        Task<bool> handling = f.Handler.ProcessFiles(null!);
        Assert.True(handling.IsFaulted);
        await Assert.ThrowsAsync<NullReferenceException>(() => handling);
        Assert.Empty(f.Calls);
    }

    [Fact]
    public async Task NullElementFaultsBeforeLoggingAndDoesNotReachLaterFiles()
    {
        var f = new Fixture();
        Task<bool> handling = f.Handler.ProcessFiles([null!, "later.cdpitask"]);
        await Assert.ThrowsAsync<NullReferenceException>(() => handling);
        Assert.Empty(f.Calls);
    }

    [Theory]
    [InlineData(".cdpisignedpack", "pack")]
    [InlineData(".cdpiconfigpack", "pack")]
    [InlineData(".cdpipatch", "update")]
    [InlineData(".cdpitask", "task")]
    [InlineData(".cdpiconfig", "share")]
    [InlineData(".CDPICONFIG", "share")]
    [InlineData(".CdPiTaSk", "task")]
    [InlineData(".CDPIPATCH", "update")]
    public async Task SupportedFilesKeepTheCaseInsensitiveWhitelistAndRouteOrder(string extension, string route)
    {
        var f = new Fixture();
        string path = "C:/fixture/input" + extension;
        Assert.True(await f.Handler.ProcessFiles([path]));
        Assert.Equal(new[] { "log:" + path, "exists:" + path, "share?:" + path, "open:" + route + ":" + path }, f.Calls);
    }

    [Theory]
    [InlineData(".zip")]
    [InlineData(".exe")]
    [InlineData(".txt")]
    [InlineData(".cdpiconfig.bak")]
    [InlineData("")]
    [InlineData(".cdpitask ")]
    public async Task ExistingUnsupportedNamesNeverAskAnImporter(string extension)
    {
        var f = new Fixture();
        string path = "input" + extension;
        Assert.False(await f.Handler.ProcessFiles([path]));
        Assert.Equal(new[] { "log:" + path, "exists:" + path }, f.Calls);
    }

    [Fact]
    public async Task MissingSupportedPathsStopBeforeTheSharePredicate()
    {
        var f = new Fixture();
        f.Operations.Exists = _ => false;
        Assert.False(await f.Handler.ProcessFiles(["missing.cdpiconfig"]));
        Assert.Equal(new[] { "log:missing.cdpiconfig", "exists:missing.cdpiconfig" }, f.Calls);
    }

    [Theory]
    [InlineData("\"C:/fixture/input.cdpitask\"", "C:/fixture/input.cdpitask", "task")]
    [InlineData("C:/fi\"xture/in\"put.cdpipatch", "C:/fixture/input.cdpipatch", "update")]
    public async Task AllDoubleQuotesAreRemovedBeforeLoggingAndLookup(string input, string normalized, string route)
    {
        var f = new Fixture();
        Assert.True(await f.Handler.ProcessFiles([input]));
        Assert.Equal(new[] { "log:" + normalized, "exists:" + normalized, "share?:" + normalized, "open:" + route + ":" + normalized }, f.Calls);
    }

    [Fact]
    public async Task WhitespaceIsNotTrimmedFromAPath()
    {
        var f = new Fixture();
        string path = "  C:/fixture/input.cdpitask  ";
        Assert.False(await f.Handler.ProcessFiles([path]));
        Assert.Equal(new[] { "log:" + path, "exists:" + path }, f.Calls);
    }

    [Theory]
    [InlineData(".cdpitask")]
    [InlineData(".cdpipatch")]
    public async Task ShareSupportTakesPrecedenceOverTheOtherSpecialRoutes(string extension)
    {
        var f = new Fixture();
        f.Operations.Share = _ => true;
        string path = "first" + extension;
        Assert.True(await f.Handler.ProcessFiles([path, "later.cdpiconfig"]));
        Assert.Equal("open:share:" + path, f.Calls.Last());
        Assert.Equal(4, f.Calls.Count);
    }

    [Fact]
    public async Task SkipsEarlierCandidatesThenStopsBeforeReadingALaterNull()
    {
        var f = new Fixture();
        f.Operations.Exists = path => path != "missing.cdpiconfig";
        Assert.True(await f.Handler.ProcessFiles(["unsupported.txt", "missing.cdpiconfig", "selected.cdpitask", null!]));
        Assert.Equal(new[] { "log:unsupported.txt", "exists:unsupported.txt", "log:missing.cdpiconfig", "exists:missing.cdpiconfig", "log:selected.cdpitask", "exists:selected.cdpitask", "share?:selected.cdpitask", "open:task:selected.cdpitask" }, f.Calls);
    }

    [Fact]
    public async Task ReadsTheOriginalArrayLiveAfterAnOperationChangesTheNextElement()
    {
        var f = new Fixture();
        string[] paths = ["skip.exe", "before.cdpiconfig"];
        f.Operations.Exists = path => { if (path == "skip.exe") paths[1] = "after.cdpitask"; return true; };
        Assert.True(await f.Handler.ProcessFiles(paths));
        Assert.Equal(new[] { "log:skip.exe", "exists:skip.exe", "log:after.cdpitask", "exists:after.cdpitask", "share?:after.cdpitask", "open:task:after.cdpitask" }, f.Calls);
    }

    [Theory]
    [InlineData(".cdpiconfig", "share")]
    [InlineData(".cdpitask", "task")]
    [InlineData(".cdpipatch", "update")]
    [InlineData(".cdpisignedpack", "pack")]
    public async Task EachRouteIsAwaitedAndNoLaterFileIsRead(string extension, string route)
    {
        var f = new Fixture();
        var reply = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Operations.Open = (selectedRoute, _) => { Assert.Equal(route, selectedRoute); return reply.Task; };
        Task<bool> handling = f.Handler.ProcessFiles(["first" + extension, "later.cdpitask"]);
        Assert.False(handling.IsCompleted);
        Assert.Equal(4, f.Calls.Count);
        Assert.Equal("open:" + route + ":first" + extension, f.Calls.Last());
        reply.SetResult();
        Assert.True(await handling);
        Assert.Equal(4, f.Calls.Count);
    }

    [Theory]
    [InlineData("log")]
    [InlineData("exists")]
    [InlineData("share")]
    public async Task SynchronousPortFailuresFaultTheAsyncResultAndDoNotTryTheNextFile(string point)
    {
        var f = new Fixture();
        var failure = new IOException(point);
        if (point == "log") f.Operations.Log = _ => throw failure;
        if (point == "exists") f.Operations.Exists = _ => throw failure;
        if (point == "share") f.Operations.Share = _ => throw failure;
        Task<bool> handling = f.Handler.ProcessFiles(["first.cdpiconfig", "later.cdpitask"]);
        Assert.True(handling.IsFaulted);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => handling));
        Assert.DoesNotContain(f.Calls, call => call.Contains("later", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(".cdpiconfig")]
    [InlineData(".cdpitask")]
    [InlineData(".cdpipatch")]
    [InlineData(".cdpisignedpack")]
    public async Task RouteFailuresPropagateUnchangedWithoutFallback(string extension)
    {
        var f = new Fixture();
        var failure = new IOException("route failure");
        f.Operations.Open = (_, _) => Task.FromException(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => f.Handler.ProcessFiles(["first" + extension, "later.cdpitask"])));
        Assert.Equal(4, f.Calls.Count);
    }

    [Theory]
    [InlineData(".cdpiconfig")]
    [InlineData(".cdpitask")]
    [InlineData(".cdpipatch")]
    [InlineData(".cdpisignedpack")]
    public async Task CanceledRouteStaysCanceledInsteadOfFallingBack(string extension)
    {
        var f = new Fixture();
        f.Operations.Open = (_, _) => Task.FromCanceled(new CancellationToken(canceled: true));
        Task<bool> handling = f.Handler.ProcessFiles(["first" + extension, "later.cdpitask"]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handling);
        Assert.True(handling.IsCanceled);
        Assert.Equal(4, f.Calls.Count);
    }

    [Fact]
    public async Task RealStartupDispatcherUsesTheHandlerAndSuppressesArgvFilesAfterInitialFileSuccess()
    {
        var f = new Fixture();
        List<Exception> errors = [];
        var dispatcher = Dispatcher(f, errors);
        Assert.True(await dispatcher.ProcessStartupAsync(["argv.cdpitask"], kind =>
            kind == AppActivationKind.File ? new(kind, Files: ["initial.cdpiconfig"]) : new()));
        Assert.Equal(new[] { "log:initial.cdpiconfig", "exists:initial.cdpiconfig", "share?:initial.cdpiconfig", "open:share:initial.cdpiconfig" }, f.Calls);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task RealStartupDispatcherCatchesHandlerFailureWithoutContinuingTheArray()
    {
        var f = new Fixture();
        var failure = new IOException("import failed");
        f.Operations.Open = (_, _) => Task.FromException(failure);
        List<Exception> errors = [];
        Assert.False(await Dispatcher(f, errors).ProcessStartupAsync(["first.cdpiconfig", "later.cdpitask"], _ => new()));
        Assert.Same(failure, Assert.Single(errors));
        Assert.Equal(4, f.Calls.Count);
    }

    [Fact]
    public async Task RealRedirectedDispatcherLeavesHandlerFailureForTheAppBoundary()
    {
        var f = new Fixture();
        var failure = new IOException("import failed");
        f.Operations.Open = (_, _) => Task.FromException(failure);
        List<Exception> errors = [];
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => Dispatcher(f, errors).ProcessRedirectedAsync(new(AppActivationKind.File, Files: ["first.cdpiconfig", "later.cdpitask"]))));
        Assert.Empty(errors);
        Assert.Equal(4, f.Calls.Count);
    }

    [Fact]
    public async Task RealRedirectedLaunchFallsBackToTheHandlerAfterTheSplitter()
    {
        var f = new Fixture();
        List<Exception> errors = [];
        Assert.True(await Dispatcher(f, errors).ProcessRedirectedAsync(new(AppActivationKind.Launch, Arguments: "\"launch.cdpitask\"")));
        Assert.Equal(new[] { "log:launch.cdpitask", "exists:launch.cdpitask", "share?:launch.cdpitask", "open:task:launch.cdpitask" }, f.Calls);
        Assert.Empty(errors);
    }

    private static AppActivationDispatcher Dispatcher(Fixture fixture, List<Exception> errors) => new(
        _ => Task.FromResult(false), _ => Task.FromResult(false), fixture.Handler.ProcessFiles,
        _ => Task.FromResult(false), _ => Task.FromResult(false), _ => false, text => [text], errors.Add);

    private sealed class Fixture
    {
        public List<string> Calls { get; } = [];
        public Operations Operations { get; }
        public AppFileActivationHandler Handler { get; }
        public Fixture() { Operations = new(Calls); Handler = new(Operations); }
    }

    private sealed class Operations(List<string> calls) : IAppFileActivationOperations
    {
        public Action<string>? Log;
        public Func<string, bool> Exists = _ => true;
        public Func<string, bool> Share = path => Path.GetExtension(path).Equals(".cdpiconfig", StringComparison.OrdinalIgnoreCase);
        public Func<string, string, Task> Open = (_, _) => Task.CompletedTask;
        public void LogFile(string path) { calls.Add("log:" + path); Log?.Invoke(path); }
        public bool PathExists(string path) { calls.Add("exists:" + path); return Exists(path); }
        public bool IsConfigShareSupported(string path) { calls.Add("share?:" + path); return Share(path); }
        public Task OpenConfigShareAsync(string path) => Route("share", path);
        public Task OpenConditionalTaskAsync(string path) => Route("task", path);
        public Task OpenUpdateFileAsync(string path) => Route("update", path);
        public Task OpenStorePackAsync(string path) => Route("pack", path);
        private Task Route(string route, string path) { calls.Add("open:" + route + ":" + path); return Open(route, path); }
    }
}
