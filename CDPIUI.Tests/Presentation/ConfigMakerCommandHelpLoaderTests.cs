using CDPIUI.Helper.CreateConfigHelper;

namespace CDPIUI.Tests.Presentation;

public class ConfigMakerCommandHelpLoaderTests
{
    [Fact]
    public void ConstructionAndCancelBeforeFirstLoadDoNotReadAnyProvider()
    {
        var fixture = new LoaderFixture();

        fixture.Loader.Cancel();

        Assert.Empty(fixture.Trace);
        Assert.Empty(fixture.Requests);
        Assert.False(fixture.Loader.IsHelpLoading);
        Assert.True(fixture.Loader.IsCommandListEnabled);
        Assert.Equal("", fixture.Loader.UsageText);
    }

    [Fact]
    public async Task LoadUpdatesLoadingAndUsageBeforeAwaitThenAppliesTheDocumentAndCallbacksInOrder()
    {
        var fixture = new LoaderFixture();
        var document = Document("  usage  ");

        var load = fixture.Loader.LoadAsync("component", false);

        Assert.True(fixture.Loader.IsHelpLoading);
        Assert.False(fixture.Loader.IsCommandListEnabled);
        Assert.Equal("Loading", fixture.Loader.UsageText);
        fixture.OnStructure = () => Assert.Same(document.Options[0], Assert.Single(fixture.Panel.CommandOptions).Source);
        fixture.Requests[0].Reply.SetResult(document);
        await load;

        Assert.Same(document, fixture.Panel.Document);
        Assert.Equal("  usage  ", fixture.Loader.UsageText);
        Assert.False(fixture.Loader.IsHelpLoading);
        Assert.Equal(["notify:IsHelpLoading", "notify:IsCommandListEnabled", "localize:ConfigMakerLoadingHelpMessage", "notify:UsageText", "load:component:False",
            "current:component", "notify:UsageText", "structure", "diagnostics", "status", "notify:IsHelpLoading", "notify:IsCommandListEnabled"], fixture.Trace);
    }

    [Fact]
    public async Task LoadPreservesBothObservableOptionResetsBeforeRebuildingStructure()
    {
        var fixture = new LoaderFixture();
        List<string> events = [];
        fixture.Panel.CommandModules.CollectionChanged += (_, args) => events.Add("modules:" + args.Action);
        fixture.Panel.CommandOptions.CollectionChanged += (_, args) => events.Add("options:" + args.Action);
        fixture.OnStructure = () => events.Add("structure");
        var load = fixture.Loader.LoadAsync("component", false);

        fixture.Requests[0].Reply.SetResult(Document("usage"));
        await load;

        Assert.Equal(["modules:Reset", "modules:Add", "options:Reset", "options:Add", "options:Reset", "options:Add", "structure"], events);
    }

    [Fact]
    public async Task EmptyUsageFormatsTheLocalizedLoadedOptionCount()
    {
        var fixture = new LoaderFixture();
        var load = fixture.Loader.LoadAsync("component", false);

        fixture.Requests[0].Reply.SetResult(Document(" \t"));
        await load;

        Assert.Equal("1 loaded", fixture.Loader.UsageText);
        Assert.Contains("localize:ConfigMakerHelpOptionsLoadedMessage", fixture.Trace);
    }

    [Fact]
    public async Task ForceRefreshIsForwardedAndTheCurrentComponentComparisonIgnoresCase()
    {
        var fixture = new LoaderFixture();
        var load = fixture.Loader.LoadAsync("component", true);
        fixture.ComponentId = "COMPONENT";

        fixture.Requests[0].Reply.SetResult(Document("usage"));
        await load;

        Assert.True(fixture.Requests[0].ForceRefresh);
        Assert.Equal("component", fixture.Requests[0].ComponentId);
        Assert.Equal("usage", fixture.Loader.UsageText);
        Assert.Contains("status", fixture.Trace);
    }

    [Fact]
    public async Task AResultForAnotherCurrentComponentDoesNotChangeThePanelOrInvokeCallbacks()
    {
        var fixture = new LoaderFixture();
        var original = fixture.Panel.Document;
        var load = fixture.Loader.LoadAsync("component", false);
        fixture.ComponentId = "other";

        fixture.Requests[0].Reply.SetResult(Document("stale"));
        await load;

        Assert.Same(original, fixture.Panel.Document);
        Assert.Equal("Loading", fixture.Loader.UsageText);
        Assert.False(fixture.Loader.IsHelpLoading);
        Assert.DoesNotContain("structure", fixture.Trace);
    }

    [Fact]
    public async Task CancelSuppressesALateSuccessAndPreservesTheExistingLoadingFlag()
    {
        var fixture = new LoaderFixture();
        var original = fixture.Panel.Document;
        var load = fixture.Loader.LoadAsync("component", false);
        fixture.Trace.Clear();

        fixture.Loader.Cancel();
        fixture.Requests[0].Reply.SetResult(Document("late"));
        await load;

        Assert.True(fixture.Requests[0].Token.IsCancellationRequested);
        Assert.Same(original, fixture.Panel.Document);
        Assert.True(fixture.Loader.IsHelpLoading);
        Assert.Empty(fixture.Trace);
    }

    [Fact]
    public async Task ANewRequestCancelsAndDisposesThePreviousSourceWithoutClearingItsOwnLoadingState()
    {
        var fixture = new LoaderFixture();
        var oldLoad = fixture.Loader.LoadAsync("component", false);
        var oldToken = fixture.Requests[0].Token;
        var newLoad = fixture.Loader.LoadAsync("component", true);

        Assert.True(oldToken.IsCancellationRequested);
        Assert.Throws<ObjectDisposedException>(() => oldToken.WaitHandle);
        fixture.Requests[0].Reply.SetResult(Document("old"));
        await oldLoad;
        Assert.True(fixture.Loader.IsHelpLoading);
        Assert.Equal("Loading", fixture.Loader.UsageText);

        fixture.Requests[1].Reply.SetResult(Document("new"));
        await newLoad;
        Assert.Equal("new", fixture.Loader.UsageText);
        Assert.False(fixture.Loader.IsHelpLoading);
    }

    [Fact]
    public async Task AnOlderCompletionAfterTheNewRequestCannotReplaceItsDocument()
    {
        var fixture = new LoaderFixture();
        var oldLoad = fixture.Loader.LoadAsync("component", false);
        var newLoad = fixture.Loader.LoadAsync("component", true);
        var document = Document("new");
        fixture.Requests[1].Reply.SetResult(document);
        await newLoad;
        fixture.Trace.Clear();

        fixture.Requests[0].Reply.SetResult(Document("old"));
        await oldLoad;

        Assert.Same(document, fixture.Panel.Document);
        Assert.Equal("new", fixture.Loader.UsageText);
        Assert.Empty(fixture.Trace);
    }

    [Fact]
    public async Task AnOperationCanceledExceptionWithoutTokenCancellationIsSwallowedAndClearsLoading()
    {
        var fixture = new LoaderFixture();
        var load = fixture.Loader.LoadAsync("component", false);

        fixture.Requests[0].Reply.SetException(new OperationCanceledException());
        await load;

        Assert.False(fixture.Requests[0].Token.IsCancellationRequested);
        Assert.False(fixture.Loader.IsHelpLoading);
        Assert.DoesNotContain("structure", fixture.Trace);
    }

    [Fact]
    public async Task AServiceFailurePropagatesAfterClearingLoading()
    {
        var fixture = new LoaderFixture();
        var failure = new IOException("service failed");
        var load = fixture.Loader.LoadAsync("component", false);

        fixture.Requests[0].Reply.SetException(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => load));
        Assert.False(fixture.Loader.IsHelpLoading);
        Assert.DoesNotContain("structure", fixture.Trace);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ACallbackFailureUsesTheSameCatchBoundaryAsTheService(bool canceledException)
    {
        var fixture = new LoaderFixture();
        Exception failure = canceledException ? new OperationCanceledException() : new IOException("structure failed");
        fixture.OnStructure = () => throw failure;
        var document = Document("usage");
        var load = fixture.Loader.LoadAsync("component", false);

        fixture.Requests[0].Reply.SetResult(document);
        if (canceledException) await load;
        else Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => load));

        Assert.Same(document, fixture.Panel.Document);
        Assert.Equal("usage", fixture.Loader.UsageText);
        Assert.False(fixture.Loader.IsHelpLoading);
        Assert.DoesNotContain("diagnostics", fixture.Trace);
        Assert.DoesNotContain("status", fixture.Trace);
    }

    [Fact]
    public async Task ALoadingLocalizationFailureRemainsOutsideTheCatchAndFinallyBoundary()
    {
        var fixture = new LoaderFixture { LocalizeError = new OperationCanceledException() };

        await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.Loader.LoadAsync("component", false));

        Assert.True(fixture.Loader.IsHelpLoading);
        Assert.Empty(fixture.Requests);
        Assert.Equal("", fixture.Loader.UsageText);
    }

    [Fact]
    public async Task AnInitialLoadingNotificationFailureAlsoRemainsOutsideFinally()
    {
        var fixture = new LoaderFixture();
        fixture.Loader.PropertyChanged += (_, args) => { if (args.PropertyName == "IsHelpLoading") throw new IOException("observer failed"); };

        await Assert.ThrowsAsync<IOException>(() => fixture.Loader.LoadAsync("component", false));

        Assert.True(fixture.Loader.IsHelpLoading);
        Assert.Equal(["notify:IsHelpLoading"], fixture.Trace);
        Assert.Empty(fixture.Requests);
    }

    [Fact]
    public async Task CancelDuringAcceptedUsageNotificationDoesNotAddALaterStaleGuard()
    {
        var fixture = new LoaderFixture();
        fixture.Loader.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == "UsageText" && fixture.Loader.UsageText == "accepted") fixture.Loader.Cancel();
        };
        var load = fixture.Loader.LoadAsync("component", false);

        fixture.Requests[0].Reply.SetResult(Document("accepted"));
        await load;

        Assert.Contains("structure", fixture.Trace);
        Assert.Contains("diagnostics", fixture.Trace);
        Assert.Contains("status", fixture.Trace);
        Assert.True(fixture.Loader.IsHelpLoading);
    }

    [Fact]
    public async Task AReentrantLoadCapturesItsOwnTokenBeforeLoadingNotifications()
    {
        var fixture = new LoaderFixture();
        Task? nested = null;
        bool entered = false;
        fixture.Loader.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != "IsHelpLoading" || !fixture.Loader.IsHelpLoading || entered) return;
            entered = true;
            nested = fixture.Loader.LoadAsync("component", true);
        };

        var outer = fixture.Loader.LoadAsync("component", false);

        Assert.Equal(2, fixture.Requests.Count);
        Assert.True(fixture.Requests[0].ForceRefresh);
        Assert.False(fixture.Requests[0].Token.IsCancellationRequested);
        Assert.True(fixture.Requests[1].Token.IsCancellationRequested);
        fixture.Requests[0].Reply.SetResult(Document("nested"));
        await nested!;
        fixture.Requests[1].Reply.SetResult(Document("outer"));
        await outer;
        Assert.Equal("nested", fixture.Loader.UsageText);
    }

    private static ComponentCommandHelpDocument Document(string usage) => new()
    {
        ComponentId = "component", Usage = usage,
        Options = [new() { Names = ["--flag"], Description = "Flag", Syntax = "--flag" }],
    };

    private sealed class LoaderFixture
    {
        public LoaderFixture()
        {
            Panel = new(key => key, () => false);
            Loader = new(Panel, () => { Trace.Add("current:" + ComponentId); return ComponentId; }, Localize,
                (id, token, force) =>
                {
                    Trace.Add($"load:{id}:{force}");
                    var request = new LoadRequest(id, token, force);
                    Requests.Add(request);
                    return request.Reply.Task;
                },
                () => { Trace.Add("structure"); OnStructure?.Invoke(); },
                () => Trace.Add("diagnostics"), _ => Trace.Add("status"));
            Loader.PropertyChanged += (_, args) => Trace.Add("notify:" + args.PropertyName);
        }
        public List<string> Trace { get; } = [];
        public List<LoadRequest> Requests { get; } = [];
        public string ComponentId { get; set; } = "component";
        public Exception? LocalizeError { get; set; }
        public Action? OnStructure { get; set; }
        public ConfigMakerCommandHelpPanel Panel { get; }
        public ConfigMakerCommandHelpLoader Loader { get; }
        private string Localize(string key)
        {
            Trace.Add("localize:" + key);
            if (LocalizeError != null) throw LocalizeError;
            return key == "ConfigMakerLoadingHelpMessage" ? "Loading" : "{0} loaded";
        }
    }

    private sealed record LoadRequest(string ComponentId, CancellationToken Token, bool ForceRefresh)
    {
        public TaskCompletionSource<ComponentCommandHelpDocument> Reply { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
