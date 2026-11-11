using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using CDPIUI.AddOns.GoodCheck;
using CDPIUI.ViewModels;

namespace CDPIUI.Tests.Presentation;

public sealed class BlockCheck2SiteListControllerTests
{
    [Fact]
    public void ConstructorStoresCollectionsAndPortsWithoutReadingAnything()
    {
        var f = new Fixture(); Assert.Empty(f.Trace); Assert.False(f.Owner.IsSiteListLoading); Assert.Null(f.Owner.SiteListLoadError);
        Assert.Equal(0, f.Owner.SelectedSiteListCount); Assert.Equal(0, f.Owner.SelectedDomainCount); Assert.Empty(f.Trace);
    }

    [Fact]
    public async Task LoadPublishesPreludeBeforeBackendAndSkipsConcurrentAndCachedCalls()
    {
        var f = new Fixture(); var gate = new TaskCompletionSource<List<SiteListElement>>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Backend.LoadReply = gate.Task;
        Task load = f.Owner.LoadAsync();
        Assert.True(f.Owner.IsSiteListLoading); Assert.Null(f.Owner.SiteListLoadError);
        Assert.Equal(["event:IsSiteListLoading", "event:SiteListLoadError", "load"], f.Trace);
        await f.Owner.LoadAsync(); Assert.Equal(1, f.Backend.LoadCalls); Assert.False(load.IsCompleted);
        gate.SetResult([]); await load;
        Assert.False(f.Owner.IsSiteListLoading); Assert.Equal("event:IsSiteListLoading", f.Trace.Last());
        f.Available.Clear(); f.Trace.Clear(); await f.Owner.LoadAsync(); Assert.Empty(f.Trace); Assert.Equal(1, f.Backend.LoadCalls);
    }

    [Theory]
    [InlineData("en-US", "ä", "z")]
    [InlineData("sv-SE", "z", "ä")]
    public async Task LoadUsesCurrentCultureForPackThenName(string culture, string first, string second)
    {
        var f = new Fixture(); f.Backend.Definitions.AddRange([Definition(second, "same"), Definition(first, "same"), Definition("before", "a")]);
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture); await f.Owner.LoadAsync();
            Assert.Equal(["before", first, second], f.Available.Select(x => x.Name));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public async Task LoadClearsBeforeFilteringAndUsesSameMutableDefinitionsAndLateRoot()
    {
        var f = new Fixture(); var first = Definition("a"); var second = Definition("b"); var blank = Definition("blank");
        f.Backend.Definitions.AddRange([second, first, blank]);
        f.OnAvailable = e => { if (e.Action == NotifyCollectionChangedAction.Reset) blank.Directory = " "; };
        f.Backend.OnCreate = a => { if (a.Name == "a") { second.Name = "mutated"; f.Root = "late-root"; } };
        await f.Owner.LoadAsync();
        Assert.Equal(["a", "mutated"], f.Available.Select(x => x.Name));
        Assert.Equal(["root", "late-root"], f.Backend.Creates.Select(x => x.Root));
        Assert.True(f.Trace.IndexOf("available:Reset") < f.Trace.IndexOf("legacy:a.txt"));
        Assert.DoesNotContain("legacy: ", f.Trace);
    }

    [Fact]
    public async Task LoadRetainsLegacyBlankDomainAndCaseSensitiveExcludeFiltersWithoutDedup()
    {
        var f = new Fixture();
        f.Backend.Definitions.AddRange([Definition("legacy"), Definition("exclude"), Definition("Exclude"), Definition("zero"), Definition("same"), Definition("same"), new()]);
        f.Backend.Legacy.Add("legacy.txt"); f.Backend.DomainReply = a => a.Name == "zero" ? 0 : 1;
        await f.Owner.LoadAsync();
        Assert.Equal(["Exclude", "same", "same"], f.Available.Select(x => x.Name));
        Assert.DoesNotContain(f.Backend.Creates, a => a.Name == "legacy");
        Assert.All(f.Backend.Creates, a => { Assert.False(a.Custom); Assert.Null(a.Content); });
    }

    [Fact]
    public async Task MissingTemplateNameAndPackUseOriginalFallbacks()
    {
        var f = new Fixture(); f.Backend.Definitions.Add(new() { Directory = "fallback.txt" });
        await f.Owner.LoadAsync(); var a = Assert.Single(f.Backend.Creates);
        Assert.Equal("fallback.txt", a.Name); Assert.Equal("", a.Source); Assert.Equal("fallback.txt", a.Path);
    }

    [Theory]
    [InlineData("IsSiteListLoading")]
    [InlineData("SiteListLoadError")]
    public async Task PreludeObserverFailureIsOutsideFinallyAndLeavesLoadingSet(string property)
    {
        var f = new Fixture(); f.Observe = name => { if (name == property) throw new IOException("prelude"); };
        await Assert.ThrowsAsync<IOException>(f.Owner.LoadAsync);
        Assert.True(f.Owner.IsSiteListLoading); Assert.Equal(0, f.Backend.LoadCalls);
        f.Observe = null; await f.Owner.LoadAsync(); Assert.Equal(0, f.Backend.LoadCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedOrCanceledLoadPublishesErrorAndCanRetry(bool canceled)
    {
        var f = new Fixture(); var error = new IOException("load failed");
        f.Backend.LoadReply = canceled ? Task.FromCanceled<List<SiteListElement>>(new CancellationToken(true)) : Task.FromException<List<SiteListElement>>(error);
        await f.Owner.LoadAsync(); Assert.False(f.Owner.IsSiteListLoading); Assert.NotNull(f.Owner.SiteListLoadError);
        if (!canceled) Assert.Equal(error.Message, f.Owner.SiteListLoadError);
        Assert.Equal(["event:SiteListLoadError", "event:IsSiteListLoading"], f.Trace.TakeLast(2));
        f.Backend.LoadReply = null; f.Backend.Definitions.Add(Definition("a")); await f.Owner.LoadAsync();
        Assert.Null(f.Owner.SiteListLoadError); Assert.Single(f.Available); Assert.Equal(2, f.Backend.LoadCalls);
    }

    [Theory]
    [InlineData("reset", 0)]
    [InlineData("add", 1)]
    [InlineData("factory", 1)]
    public async Task CollectionOrFactoryFailureRetainsPartialPublicationAndAllowsRetry(string failure, int count)
    {
        var f = new Fixture(); f.Backend.Definitions.AddRange([Definition("a"), Definition("b")]);
        f.OnAvailable = e =>
        {
            if (failure == "reset" && e.Action == NotifyCollectionChangedAction.Reset || failure == "add" && e.Action == NotifyCollectionChangedAction.Add)
                throw new IOException("collection");
        };
        f.Backend.OnCreate = a => { if (failure == "factory" && a.Name == "b") throw new IOException("factory"); };
        await f.Owner.LoadAsync(); Assert.Equal(count, f.Available.Count); Assert.NotNull(f.Owner.SiteListLoadError); Assert.False(f.Owner.IsSiteListLoading);
        f.OnAvailable = null; f.Backend.OnCreate = null; await f.Owner.LoadAsync(); Assert.Equal(2, f.Available.Count);
    }

    [Fact]
    public async Task ErrorObserverFailureStillRunsFinallyAndFinalObserverFailureKeepsLoadedCache()
    {
        var f = new Fixture(); f.Backend.LoadReply = Task.FromException<List<SiteListElement>>(new IOException("backend"));
        f.Observe = name => { if (name == "SiteListLoadError" && f.Owner.SiteListLoadError != null) throw new IOException("observer"); };
        Assert.Equal("observer", (await Assert.ThrowsAsync<IOException>(f.Owner.LoadAsync)).Message); Assert.False(f.Owner.IsSiteListLoading);
        f = new Fixture(); f.Observe = name => { if (name == "IsSiteListLoading" && !f.Owner.IsSiteListLoading) throw new IOException("finally"); };
        await Assert.ThrowsAsync<IOException>(f.Owner.LoadAsync); f.Observe = null; f.Trace.Clear(); await f.Owner.LoadAsync();
        Assert.Empty(f.Trace); Assert.Equal(1, f.Backend.LoadCalls);
    }

    [Fact]
    public async Task ReentryFromAvailableResetSeesLoadingAndDoesNotStartAnotherLoad()
    {
        var f = new Fixture(); Task? nested = null;
        f.OnAvailable = e => { if (e.Action == NotifyCollectionChangedAction.Reset) nested = f.Owner.LoadAsync(); };
        await f.Owner.LoadAsync(); Assert.NotNull(nested); Assert.True(nested.IsCompletedSuccessfully); Assert.Equal(1, f.Backend.LoadCalls);
    }

    [Fact]
    public void AddCustomNormalizesPathPrefersAvailableAndDoesNotMutateCollections()
    {
        var f = new Fixture(); string path = Path.GetFullPath("existing.txt");
        var available = new Row("available", path.ToUpperInvariant()); var selected = new Row("selected", path);
        f.Available.Add(available); f.Selected.Add(selected); f.Trace.Clear();
        Assert.Same(available, f.Owner.AddCustom("existing.txt", "source")); Assert.Empty(f.Backend.Creates);
        Assert.Single(f.Available); Assert.Single(f.Selected); Assert.DoesNotContain("root", f.Trace);
        var added = f.Owner.AddCustom("new.txt", "source"); var args = Assert.Single(f.Backend.Creates);
        Assert.Equal(Path.GetFullPath("new.txt"), args.Path); Assert.Equal("new.txt", args.Name); Assert.False(args.Custom); Assert.Null(args.Content);
        Assert.DoesNotContain(added, f.Available); Assert.DoesNotContain(added, f.Selected); Assert.DoesNotContain("selection", f.Trace);
    }

    [Fact]
    public async Task EmptyNormalizedCustomContentUsesOriginalLocalizedErrorBeforeWrite()
    {
        var f = new Fixture(); f.Backend.Normalized = "";
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => f.Owner.UpdateCustomAsync("raw", "name", "source"));
        Assert.Equal("localized empty", error.Message); Assert.Equal(["normalize:raw", "text:BlockCheck2CustomSiteListEmpty"], f.Trace);
        Assert.Empty(f.Backend.Writes); Assert.Empty(f.Backend.Creates);
    }

    [Fact]
    public async Task NewCustomCapturesPathBeforeAwaitButUsesCurrentRootAndCollectionAfterward()
    {
        var f = new Fixture(); var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Backend.WriteReply = gate.Task; using var cancellation = new CancellationTokenSource();
        Task<Row> update = f.Owner.UpdateCustomAsync("raw", "name", "source", cancellation.Token);
        Assert.False(update.IsCompleted); Assert.Empty(f.Backend.Creates);
        var other = new Row("other", "other") { Custom = true }; f.Selected.Add(other); f.Root = "late"; cancellation.Cancel();
        gate.SetResult(true); Row result = await update;
        var args = Assert.Single(f.Backend.Creates); Assert.True(args.Custom); Assert.Equal("normalized", args.Content); Assert.Equal("late", args.Root);
        Assert.Equal([result, other], f.Selected); Assert.Equal(cancellation.Token, f.Backend.Writes.Single().Token);
        Assert.Equal("selection", f.Trace.Last());
    }

    [Fact]
    public async Task ExistingCustomIsCapturedBeforeAwaitAndUpdatedEvenAfterRemoval()
    {
        var f = new Fixture(); var first = new Row("first", "captured") { Custom = true }; var second = new Row("second", "second") { Custom = true };
        f.Selected.Add(first); f.Selected.Add(second); var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Backend.WriteReply = gate.Task; Task<Row> update = f.Owner.UpdateCustomAsync("raw", "ignored", "ignored");
        f.Selected.Remove(first); f.Root = "late"; gate.SetResult(true);
        Assert.Same(first, await update); Assert.Equal("normalized", first.Content); Assert.Equal("captured", f.Backend.Writes.Single().Path);
        Assert.Same(second, Assert.Single(f.Selected)); Assert.Empty(f.Backend.Creates); Assert.DoesNotContain("root", f.Trace);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedOrCanceledWriteDoesNotMutateOrNotify(bool canceled)
    {
        var f = new Fixture(); f.Backend.WriteReply = canceled ? Task.FromCanceled(new CancellationToken(true)) : Task.FromException(new IOException("write"));
        await Assert.ThrowsAnyAsync<Exception>(() => f.Owner.UpdateCustomAsync("raw", "name", "source"));
        Assert.Empty(f.Selected); Assert.Empty(f.Backend.Creates); Assert.DoesNotContain("selection", f.Trace);
    }

    [Fact]
    public async Task InsertObserverAndUpdateFailuresKeepAssignedStateWithoutSelectionNotify()
    {
        var f = new Fixture(); f.OnSelected = e => { if (e.Action == NotifyCollectionChangedAction.Add) throw new IOException("insert"); };
        await Assert.ThrowsAsync<IOException>(() => f.Owner.UpdateCustomAsync("raw", "name", "source")); Assert.Single(f.Selected); Assert.DoesNotContain("selection", f.Trace);
        f = new Fixture(); var row = new Row("custom", "custom") { Custom = true }; f.Selected.Add(row); f.Trace.Clear();
        f.Backend.OnUpdate = _ => throw new IOException("update");
        await Assert.ThrowsAsync<IOException>(() => f.Owner.UpdateCustomAsync("raw", "name", "source"));
        Assert.Equal("normalized", row.Content); Assert.DoesNotContain("selection", f.Trace);
    }

    [Fact]
    public async Task OverlappingNewCustomUpdatesKeepOriginalCompletionOrderWithoutReselection()
    {
        var f = new Fixture(); var gates = new[] { new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously), new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously) };
        int calls = 0; f.Backend.WriteFactory = () => gates[calls++].Task;
        Task<Row> first = f.Owner.UpdateCustomAsync("one", "first", "source"); Task<Row> second = f.Owner.UpdateCustomAsync("two", "second", "source");
        gates[1].SetResult(true); Row secondRow = await second; gates[0].SetResult(true); Row firstRow = await first;
        Assert.Equal([firstRow, secondRow], f.Selected); Assert.Equal(2, f.Trace.Count(x => x == "selection"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectAndRemoveNullKeepOriginalItemParameterGuard(bool remove)
    {
        var f = new Fixture();
        var error = Assert.Throws<ArgumentNullException>(() => { if (remove) f.Owner.Remove(null!); else f.Owner.Select(null!); });
        Assert.Equal("item", error.ParamName); Assert.Empty(f.Trace);
    }

    [Fact]
    public void SelectionDeduplicatesPathsButRemovalKeepsObjectIdentityAndLiveCounts()
    {
        var f = new Fixture(); var first = new Row("first", "Path") { Count = 2 }; var duplicate = new Row("duplicate", "PATH") { Count = 4 };
        f.Owner.Select(first); f.Owner.Select(duplicate); Assert.Same(first, Assert.Single(f.Selected)); Assert.Equal(1, f.Trace.Count(x => x == "selection"));
        f.Owner.Remove(duplicate); Assert.Single(f.Selected); Assert.Equal(1, f.Trace.Count(x => x == "selection"));
        Assert.Equal(1, f.Owner.SelectedSiteListCount); Assert.Equal(2, f.Owner.SelectedDomainCount);
        first.Count = 7; Assert.Equal(7, f.Owner.SelectedDomainCount); f.Owner.Remove(first); Assert.Empty(f.Selected);
        Assert.Equal(2, f.Trace.Count(x => x == "selection"));
    }

    [Fact]
    public void SetSelectedKeepsFirstPathAndSelfSourceClearsBeforeEnumeration()
    {
        var f = new Fixture(); var first = new Row("first", "a"); var duplicate = new Row("duplicate", "A"); var second = new Row("second", "b");
        f.Owner.SetSelected([first, duplicate, second]); Assert.Equal([first, second], f.Selected);
        f.Owner.SetSelected(f.Selected); Assert.Empty(f.Selected); Assert.Equal(2, f.Trace.Count(x => x == "selection"));
        f.Selected.Add(first); f.Trace.Clear(); Assert.Throws<ArgumentNullException>(() => f.Owner.SetSelected(null!));
        Assert.Empty(f.Selected); Assert.Equal(["selected:Reset"], f.Trace);
    }

    [Theory]
    [InlineData("select")]
    [InlineData("remove")]
    [InlineData("set")]
    public void SelectionCallbackFailureDoesNotRollbackMutation(string operation)
    {
        var f = new Fixture(); var row = new Row("row", "path"); if (operation == "remove") f.Selected.Add(row);
        f.OnSelection = () => throw new IOException("notify");
        Assert.Throws<IOException>(() => { if (operation == "select") f.Owner.Select(row); else if (operation == "remove") f.Owner.Remove(row); else f.Owner.SetSelected([row]); });
        Assert.Equal(operation == "remove" ? 0 : 1, f.Selected.Count);
    }

    [Fact]
    public void SelectedDomainCountRetainsIntegerSumOverflow()
    {
        var f = new Fixture(); f.Selected.Add(new("max", "a") { Count = int.MaxValue }); f.Selected.Add(new("one", "b"));
        Assert.Throws<OverflowException>(() => f.Owner.SelectedDomainCount);
    }

    private static SiteListElement Definition(string name, string pack = "p") => new() { Name = name, PackName = pack, Directory = name + ".txt" };
    private sealed class Row(string name, string path)
    {
        public string Name = name, Path = path, Content = "";
        public bool Custom;
        public int Count = 1;
    }
    private sealed record CreateArgs(string Name, string Source, string Path, bool Custom, string? Content, string Root);
    private sealed class Fixture
    {
        public Fixture()
        {
            Backend = new(Trace);
            Owner = new(Available, Selected, Backend, () => { Trace.Add("root"); return Root; }, () => { Trace.Add("selection"); OnSelection?.Invoke(); });
            Owner.PropertyChanged += (sender, e) => { Assert.Same(Owner, sender); Trace.Add("event:" + e.PropertyName); Observe?.Invoke(e.PropertyName!); };
            Available.CollectionChanged += (_, e) => { Trace.Add("available:" + e.Action); OnAvailable?.Invoke(e); };
            Selected.CollectionChanged += (_, e) => { Trace.Add("selected:" + e.Action); OnSelected?.Invoke(e); };
        }
        public BlockCheck2SiteListController<Row> Owner { get; }
        public Backend Backend { get; }
        public ObservableCollection<Row> Available { get; } = [];
        public ObservableCollection<Row> Selected { get; } = [];
        public List<string> Trace { get; } = [];
        public string Root = "root";
        public Action<string>? Observe;
        public Action<NotifyCollectionChangedEventArgs>? OnAvailable, OnSelected;
        public Action? OnSelection;
    }
    private sealed class Backend(List<string> trace) : IBlockCheck2SiteListBackend<Row>
    {
        public List<SiteListElement> Definitions { get; } = [];
        public HashSet<string> Legacy { get; } = [];
        public List<CreateArgs> Creates { get; } = [];
        public List<(string Path, string Content, CancellationToken Token)> Writes { get; } = [];
        public Task<List<SiteListElement>>? LoadReply;
        public Task? WriteReply;
        public Func<Task>? WriteFactory;
        public Action<CreateArgs>? OnCreate;
        public Action<Row>? OnUpdate;
        public Func<CreateArgs, int>? DomainReply;
        public string Normalized = "normalized";
        public int LoadCalls;
        public Task<List<SiteListElement>> LoadAvailableAsync() { trace.Add("load"); LoadCalls++; return LoadReply ?? Task.FromResult(Definitions); }
        public bool IsLegacyGenerated(string path) { trace.Add("legacy:" + path); return Legacy.Contains(path); }
        public Row Create(string displayName, string sourceName, string path, bool isCustom, string? editableContent, string root)
        {
            var args = new CreateArgs(displayName, sourceName, path, isCustom, editableContent, root); Creates.Add(args); trace.Add("create:" + displayName); OnCreate?.Invoke(args);
            return new(displayName, path) { Custom = isCustom, Content = editableContent ?? "", Count = DomainReply?.Invoke(args) ?? 1 };
        }
        public string Normalize(string content) { trace.Add("normalize:" + content); return Normalized; }
        public string CreateCustomPath() { trace.Add("custom-path"); return "custom-path"; }
        public Task WriteAsync(string path, string normalized, CancellationToken token) { trace.Add("write:" + path); Writes.Add((path, normalized, token)); return WriteFactory?.Invoke() ?? WriteReply ?? Task.CompletedTask; }
        public string Text(string key) { trace.Add("text:" + key); return "localized empty"; }
        public string GetFilePath(Row item) { trace.Add("path:" + item.Name); return item.Path; }
        public string GetDisplayName(Row item) { trace.Add("name:" + item.Name); return item.Name; }
        public int GetDomainCount(Row item) { trace.Add("domain:" + item.Name); return item.Count; }
        public bool IsCustom(Row item) { trace.Add("custom:" + item.Name); return item.Custom; }
        public void UpdateCustom(Row item, string content) { trace.Add("update:" + item.Name); item.Content = content; OnUpdate?.Invoke(item); }
    }
}
