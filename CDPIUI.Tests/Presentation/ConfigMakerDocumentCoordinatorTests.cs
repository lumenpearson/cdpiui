using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.ViewModels;
using CDPIUI.ViewModels.ConfigMaker;

namespace CDPIUI.Tests.Presentation;

public sealed class ConfigMakerDocumentCoordinatorTests
{
    [Fact]
    public void LoadRetainsCollectionsClonesItemsAndPublishesCallbacksAfterMetadata()
    {
        var f = new Fixture();
        var source = Document();
        var variables = f.Document.Variables;
        var resources = f.Document.Resources;
        f.Host.ComponentSet = value =>
        {
            Assert.Equal("component", value);
            Assert.Equal("pack", f.Document.PackId);
            Assert.Equal("file", f.Document.FileName);
            Assert.Equal("meta", f.Document.Meta);
            Assert.Equal("target-version", f.Document.TargetVersion);
            Assert.Single(f.Document.Variables);
            Assert.Single(f.Document.Resources);
        };

        f.Coordinator.LoadPresetDocument(source);

        Assert.Same(variables, f.Document.Variables);
        Assert.Same(resources, f.Document.Resources);
        Assert.NotSame(source.Variables[0], Assert.Single(variables));
        Assert.NotSame(source.Resources[0], Assert.Single(resources));
        Assert.NotSame(source.Variables[0].Values, variables[0].Values);
        Assert.Equal(source.Variables[0].Id, variables[0].Id);
        Assert.Equal(new[] { "one", "two" }, variables[0].Values);
        Assert.Equal(new[] { "base:", "component:set:component", "editor:set:--source", "rebuild", "designer", "document:event" }, f.Host.Calls);
        source.Variables[0].Value = "changed source";
        source.Resources[0].Path = "changed source";
        Assert.Equal("one", variables[0].Value);
        Assert.Equal("resource-path", resources[0].Path);
    }

    [Fact]
    public void LoadOfTheCurrentDocumentKeepsBaselineSelfClearBehavior()
    {
        var f = new Fixture(Document());
        f.Coordinator.LoadPresetDocument(f.Document);
        Assert.Empty(f.Document.Variables);
        Assert.Empty(f.Document.Resources);
        Assert.Equal("--source", f.Host.CurrentEditor);
        Assert.Equal("pack", f.Document.PackId);
    }

    [Fact]
    public void LoadReadsSourceCommandAfterTheComponentCallback()
    {
        var f = new Fixture();
        var source = Document();
        f.Host.ComponentSet = _ => source.CommandText = "changed during component event";
        f.Coordinator.LoadPresetDocument(source);
        Assert.Equal("changed during component event", f.Host.CurrentEditor);
    }

    [Fact]
    public void LoadReadsLaterMetadataAndItemsAfterDocumentNotifications()
    {
        var f = new Fixture();
        var source = Document();
        f.Document.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(ConfigMakerPresetDocument.Name)) return;
            source.ComponentId = "changed component";
            source.Meta = "changed metadata";
            source.Variables[0].Value = "changed variable";
            source.Resources[0].Path = "changed resource";
        };
        f.Coordinator.LoadPresetDocument(source);
        Assert.Equal("changed component", f.Document.ComponentId);
        Assert.Equal("changed metadata", f.Document.Meta);
        Assert.Equal("changed variable", Assert.Single(f.Document.Variables).Value);
        Assert.Equal("changed resource", Assert.Single(f.Document.Resources).Path);
    }

    [Fact]
    public void LoadFailurePreservesPublishedPartialStateAndStopsLaterCallbacks()
    {
        var f = new Fixture();
        var failure = new InvalidOperationException("rebuild failed");
        f.Host.Rebuild = () => throw failure;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => f.Coordinator.LoadPresetDocument(Document())));
        Assert.Equal("pack", f.Document.PackId);
        Assert.Equal("--source", f.Host.CurrentEditor);
        Assert.DoesNotContain("designer", f.Host.Calls);
        Assert.DoesNotContain("document:event", f.Host.Calls);
    }

    [Fact]
    public void LoadNullIsSynchronousAndHasNoEffects()
    {
        var f = new Fixture();
        Assert.Equal("document", Assert.Throws<ArgumentNullException>(() => f.Coordinator.LoadPresetDocument(null!)).ParamName);
        Assert.Empty(f.Host.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfigLoadRetainsConversionAndCallbackOrder(bool correct)
    {
        var f = new Fixture();
        var item = Config();
        f.Host.ConfigDirectory = value => { Assert.Same(item, value); return "base-dir"; };
        f.Host.AutoCorrect = value => { Assert.Same(item, value); return Task.CompletedTask; };
        await f.Coordinator.LoadConfigItem(item, correct);
        var expected = new List<string> { "base:", "component:set:component", "editor:set:--source", "rebuild", "designer", "document:event", "base:get", "base:base-dir", "rebuild" };
        if (correct) expected.Add("autocorrect");
        expected.AddRange(["attach", "rebuild"]);
        Assert.Equal(expected, f.Host.Calls);
        Assert.Equal("pack", f.Document.PackId);
    }

    [Fact]
    public async Task ConfigLoadAwaitsCorrectionAndPropagatesItsFailureWithoutAttaching()
    {
        var f = new Fixture();
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Host.AutoCorrect = _ => pending.Task;
        Task loading = f.Coordinator.LoadConfigItem(Config(), true);
        Assert.False(loading.IsCompleted);
        Assert.DoesNotContain("attach", f.Host.Calls);
        var failure = new IOException("correction failed");
        pending.SetException(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => loading));
        Assert.DoesNotContain("attach", f.Host.Calls);
        Assert.Equal("--source", f.Host.CurrentEditor);
    }

    [Fact]
    public async Task ConfigLoadNullReturnsAFaultedTaskBeforeAnyEffect()
    {
        var f = new Fixture();
        Task task = f.Coordinator.LoadConfigItem(null!);
        Assert.Equal("item", (await Assert.ThrowsAsync<ArgumentNullException>(() => task)).ParamName);
        Assert.Empty(f.Host.Calls);
    }

    [Fact]
    public void CreateUsesTheCurrentComponentAndEditorAndRealConverter()
    {
        var f = new Fixture(Document());
        f.Host.CurrentComponent = "edited-component";
        f.Host.CurrentEditor = "--edited";
        ConfigItem item = f.Coordinator.CreateConfigItem("new-pack", "New preset");
        Assert.Equal(new[] { "component:get", "editor:get" }, f.Host.Calls);
        Assert.Equal("--edited", item.startup_string);
        Assert.Equal(new[] { "edited-component", "target-version" }, item.target);
        Assert.Equal("new-pack", item.packId);
        Assert.Equal("New preset", item.name);
        Assert.Equal("list", Assert.Single(item.configMaker!.resources!).alias);
    }

    [Fact]
    public void LegacyConditionalConversionUsesTheOriginalCoreParser()
    {
        var item = Config();
        item.variables = ["%Mode%=$LOCALCONDITION(mode_flag==true ? --on $SEPARATOR --off)"];
        item.jparams = new() { ["mode_flag"] = true };
        var result = ConfigMakerPresetDocument.FromConfigItem(item);
        var variable = Assert.Single(result.Variables);
        Assert.Equal("Mode", variable.Name);
        Assert.Equal(ConfigMakerVariableKind.Switch, variable.Kind);
        Assert.Equal(ConfigMakerVariableStorageKind.Conditional, variable.StorageKind);
        Assert.Equal("mode_flag", variable.InternalParameterName);
        Assert.Equal("--on", variable.OnValue);
        Assert.Equal("--off", variable.OffValue);
        Assert.True(variable.IsSwitchEnabled);
    }

    [Fact]
    public async Task SaveSyncsAgainAfterAttachmentAndCreatesStorageOnlyAfterDialog()
    {
        var f = new Fixture(Document());
        int reads = 0;
        f.Host.ReadEditor = () => ++reads == 1 ? "before attach" : "after attach";
        f.Host.Attach = () => Assert.Equal("before attach", f.Document.CommandText);
        var answer = new TaskCompletionSource<ConfigMakerSavePresetRequest?>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Host.AskSave = (name, overwrite) =>
        {
            Assert.Equal("Source", name);
            Assert.True(overwrite);
            Assert.Equal("after attach", f.Document.CommandText);
            return answer.Task;
        };
        var result = new ConfigMakerPresetSaveResult { Success = true, PackId = "saved-pack", ConfigFileName = "saved-file" };
        f.Storage.Save = (name, document) =>
        {
            Assert.Same(f.Document, document);
            Assert.Equal("Chosen", name);
            Assert.Equal("after attach", document.CommandText);
            return Task.FromResult(result);
        };
        Task<ConfigMakerPresetSaveResult> saving = f.Coordinator.SaveToApplicationAsync();
        Assert.Equal(0, f.StorageCreations);
        Assert.False(saving.IsCompleted);
        answer.SetResult(new("Chosen", false));
        Assert.Same(result, await saving);
        Assert.Equal(1, f.StorageCreations);
        Assert.Equal(new[] { "component:get", "editor:get", "attach", "component:get", "editor:get", "save:ask", "storage:new", "storage:save", "text:ConfigMakerPresetSavedMessage", "notice:False:saved:Chosen" }, f.Host.Calls);
        Assert.Equal("Chosen", f.Document.Name);
        Assert.Equal("saved-pack", f.Document.PackId);
        Assert.Equal("saved-file", f.Document.FileName);
    }

    [Theory]
    [InlineData(null, "file", false)]
    [InlineData("pack", " ", false)]
    [InlineData("pack", "file", true)]
    public async Task SaveCancelDoesNotCreateStorageOrShowNotice(string? pack, string? file, bool enabled)
    {
        var f = new Fixture(Document());
        f.Document.PackId = pack;
        f.Document.FileName = file;
        f.Host.AskSave = (_, canOverwrite) => { Assert.Equal(enabled, canOverwrite); return Task.FromResult<ConfigMakerSavePresetRequest?>(null); };
        var result = await f.Coordinator.SaveToApplicationAsync();
        Assert.False(result.Success);
        Assert.Equal("CANCELED", result.ErrorCode);
        Assert.Equal(0, f.StorageCreations);
        Assert.Empty(f.Host.Notices);
    }

    [Fact]
    public async Task OverwriteAppliesResultsToTheLiveDocumentWithoutReplacingItsName()
    {
        var f = new Fixture(Document());
        f.Host.AskSave = (_, _) => Task.FromResult<ConfigMakerSavePresetRequest?>(new("ignored name", true));
        var pending = new TaskCompletionSource<ConfigMakerPresetSaveResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Storage.Overwrite = document => { Assert.Same(f.Document, document); return pending.Task; };
        Task<ConfigMakerPresetSaveResult> saving = f.Coordinator.SaveToApplicationAsync();
        f.Document.Name = "Live name";
        f.Document.Resources.Clear();
        var live = new ConfigMakerPresetResource { Alias = "LiSt", Path = "before" };
        f.Document.Resources.Add(live);
        var result = new ConfigMakerPresetSaveResult { Success = true, PackId = "pack2", ConfigFileName = "file2", StoredResources = [new() { alias = "list", path = "after", isBuiltIn = true }] };
        pending.SetResult(result);
        Assert.Same(result, await saving);
        Assert.Equal("Live name", f.Document.Name);
        Assert.Equal("after", live.Path);
        Assert.True(live.IsBuiltIn);
        Assert.Equal(("saved:Live name", false), Assert.Single(f.Host.Notices));
        Assert.DoesNotContain("storage:save", f.Host.Calls);
    }

    [Fact]
    public async Task StoredResourcesUseFirstAliasMatchAndSkipBlankPaths()
    {
        var f = new Fixture(Document());
        var first = f.Document.Resources[0];
        var absent = new ConfigMakerPresetResource { Alias = "absent", Path = "untouched" };
        f.Document.Resources.Add(absent);
        f.Storage.Result = new() { Success = true, StoredResources = [new() { alias = "LIST", path = " " }, new() { alias = "list", path = "must-not-use", isBuiltIn = true }] };
        await f.Coordinator.SaveToApplicationAsync();
        Assert.Equal("resource-path", first.Path);
        Assert.False(first.IsBuiltIn);
        Assert.Equal("untouched", absent.Path);
    }

    [Fact]
    public async Task ResourceNotificationFailureKeepsMetadataAndPathAlreadyPublished()
    {
        var f = new Fixture(Document());
        var resource = f.Document.Resources[0];
        var failure = new IOException("resource subscriber");
        resource.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ConfigMakerPresetResource.Path)) throw failure;
        };
        f.Storage.Result = new() { Success = true, PackId = "published pack", ConfigFileName = "published file", StoredResources = [new() { alias = "LIST", path = "published path", isBuiltIn = true }] };
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => f.Coordinator.SaveToApplicationAsync()));
        Assert.Equal("Chosen", f.Document.Name);
        Assert.Equal("published pack", f.Document.PackId);
        Assert.Equal("published file", f.Document.FileName);
        Assert.Equal("published path", resource.Path);
        Assert.False(resource.IsBuiltIn);
        Assert.Empty(f.Host.Notices);
    }

    [Theory]
    [InlineData("CANCELED", "details", null)]
    [InlineData("canceled", "details", "failed:details")]
    [InlineData("FAILED", " ", "failed:FAILED")]
    [InlineData("FAILED", "actual", "failed:actual")]
    public async Task SaveFailureUsesOrdinalCancelAndDetailsFallback(string code, string details, string? expected)
    {
        var f = new Fixture(Document());
        var result = ConfigMakerPresetSaveResult.Failed(code, details);
        f.Storage.Result = result;
        Assert.Same(result, await f.Coordinator.SaveToApplicationAsync());
        Assert.Equal("Source", f.Document.Name);
        Assert.Equal("pack", f.Document.PackId);
        if (expected == null) Assert.Empty(f.Host.Notices);
        else Assert.Equal((expected, true), Assert.Single(f.Host.Notices));
    }

    [Theory]
    [InlineData("dialog")]
    [InlineData("factory")]
    [InlineData("storage")]
    [InlineData("notice")]
    public async Task SaveDoesNotCatchExternalFailures(string point)
    {
        var f = new Fixture(Document());
        var failure = new InvalidOperationException(point);
        if (point == "dialog") f.Host.AskSave = (_, _) => throw failure;
        if (point == "factory") f.Factory = () => throw failure;
        if (point == "storage") f.Storage.Save = (_, _) => Task.FromException<ConfigMakerPresetSaveResult>(failure);
        if (point == "notice") { f.Storage.Result = new() { Success = true, PackId = "published" }; f.Host.Notice = (_, _) => throw failure; }
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => f.Coordinator.SaveToApplicationAsync()));
        if (point == "notice") Assert.Equal("published", f.Document.PackId);
    }

    [Fact]
    public async Task OverlappingSavesRetainCompletionOrderWithoutSerialization()
    {
        var f = new Fixture(Document());
        var first = new TaskCompletionSource<ConfigMakerPresetSaveResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<ConfigMakerPresetSaveResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        int count = 0;
        f.Storage.Save = (_, document) => { Assert.Same(f.Document, document); return ++count == 1 ? first.Task : second.Task; };
        var a = f.Coordinator.SaveToApplicationAsync();
        var b = f.Coordinator.SaveToApplicationAsync();
        Assert.Equal(2, count);
        second.SetResult(new() { Success = true, PackId = "second" });
        await b;
        Assert.Equal("second", f.Document.PackId);
        first.SetResult(new() { Success = true, PackId = "first" });
        await a;
        Assert.Equal("first", f.Document.PackId);
    }

    [Fact]
    public async Task ReadonlyNewDoesNotReadTextOrTouchDocument()
    {
        var f = new Fixture(Document());
        f.Host.ReadOnly = true;
        await f.Coordinator.NewDocumentAsync();
        Assert.Equal(new[] { "readonly:get" }, f.Host.Calls);
        Assert.Single(f.Document.Variables);
    }

    [Fact]
    public async Task NewEmptyContentDoesNotAskAndKeepsOtherMetadata()
    {
        var f = new Fixture(Document());
        f.Document.Variables.Clear();
        f.Document.Resources.Clear();
        f.Host.CurrentEditor = " ";
        await f.Coordinator.NewDocumentAsync();
        Assert.DoesNotContain("new:confirm", f.Host.Calls);
        Assert.Equal(new[] { "readonly:get", "editor:get", "editor:set:", "base:", "structure:clear", "undo:clear" }, f.Host.Calls);
        Assert.Equal(string.Empty, f.Document.Name);
        Assert.Equal("pack", f.Document.PackId);
        Assert.Equal("file", f.Document.FileName);
        Assert.Equal("component", f.Document.ComponentId);
        Assert.Equal("meta", f.Document.Meta);
        Assert.Equal("target-version", f.Document.TargetVersion);
    }

    [Fact]
    public async Task NewCanceledDoesNotClearAnything()
    {
        var f = new Fixture(Document());
        f.Host.Confirm = () => Task.FromResult(false);
        await f.Coordinator.NewDocumentAsync();
        Assert.Equal(new[] { "readonly:get", "editor:get", "new:confirm" }, f.Host.Calls);
        Assert.Single(f.Document.Variables);
        Assert.Single(f.Document.Resources);
        Assert.Equal("Source", f.Document.Name);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task NewAsksForVariablesOrResourcesEvenWithEmptyText(bool variables, bool resources)
    {
        var f = new Fixture(Document());
        f.Host.CurrentEditor = "";
        if (!variables) f.Document.Variables.Clear();
        if (!resources) f.Document.Resources.Clear();
        f.Host.Confirm = () => Task.FromResult(false);
        await f.Coordinator.NewDocumentAsync();
        Assert.Contains("new:confirm", f.Host.Calls);
        Assert.Equal(variables, f.Document.HasVariables);
        Assert.Equal(resources, f.Document.HasResources);
        Assert.DoesNotContain("structure:clear", f.Host.Calls);
    }

    [Fact]
    public async Task NewConfirmationFailurePropagatesWithoutClearing()
    {
        var f = new Fixture(Document());
        var failure = new IOException("confirmation failed");
        f.Host.Confirm = () => Task.FromException<bool>(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => f.Coordinator.NewDocumentAsync()));
        Assert.Single(f.Document.Variables);
        Assert.Single(f.Document.Resources);
        Assert.DoesNotContain("editor:set:", f.Host.Calls);
    }

    [Fact]
    public async Task NewKeepsTheOriginalSingleReadonlyCheckAndClearEventOrder()
    {
        var f = new Fixture(Document());
        var answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Host.Confirm = () => answer.Task;
        f.Document.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ConfigMakerPresetDocument.Name)) f.Host.Calls.Add("name:changed"); };
        f.Document.Variables.CollectionChanged += (_, _) => f.Host.Calls.Add("variables:clear");
        f.Document.Resources.CollectionChanged += (_, _) => f.Host.Calls.Add("resources:clear");
        Task creating = f.Coordinator.NewDocumentAsync();
        Assert.False(creating.IsCompleted);
        f.Host.ReadOnly = true;
        answer.SetResult(true);
        await creating;
        Assert.Equal(new[] { "readonly:get", "editor:get", "new:confirm", "editor:set:", "base:", "name:changed", "variables:clear", "resources:clear", "structure:clear", "undo:clear" }, f.Host.Calls);
        Assert.Empty(f.Document.Variables);
        Assert.Empty(f.Document.Resources);
    }

    [Fact]
    public async Task NewFailureLeavesBaselinePartialStateAndDoesNotClearUndo()
    {
        var f = new Fixture(Document());
        var failure = new IOException("clear failed");
        f.Host.ClearStructure = () => throw failure;
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => f.Coordinator.NewDocumentAsync()));
        Assert.Empty(f.Document.Variables);
        Assert.Empty(f.Document.Resources);
        Assert.DoesNotContain("undo:clear", f.Host.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExportUsesVariableGateAndLiveDocumentAfterPicker(bool variables)
    {
        var f = new Fixture(Document());
        if (!variables) f.Document.Variables.Clear();
        f.Host.ExportPath = allow => { Assert.Equal(!variables, allow); f.Host.CurrentEditor = "changed in picker"; return "chosen.CDPICONFIG"; };
        f.Export = (document, path) =>
        {
            Assert.Same(f.Document, document);
            Assert.Equal("chosen.CDPICONFIG", path);
            Assert.Equal("changed in picker", document.CommandText);
            return Task.CompletedTask;
        };
        await f.Coordinator.SaveTextAsync();
        Assert.Equal(new[] { "export:choose", "component:get", "editor:get", "export:write", "text:ConfigMakerTextSavedMessage", "notice:False:exported" }, f.Host.Calls);
        Assert.Equal(0, f.StorageCreations);
    }

    [Fact]
    public async Task ExportCancelDoesNotSyncOrExport()
    {
        var f = new Fixture(Document());
        f.Host.ExportPath = _ => null;
        await f.Coordinator.SaveTextAsync();
        Assert.Equal(new[] { "export:choose" }, f.Host.Calls);
        Assert.Equal("--source", f.Document.CommandText);
    }

    [Theory]
    [InlineData("picker")]
    [InlineData("editor")]
    [InlineData("export")]
    [InlineData("notice")]
    public async Task ExportCatchesFailuresIncludingSuccessNotice(string point)
    {
        var f = new Fixture();
        var failure = new IOException(point);
        if (point == "picker") f.Host.ExportPath = _ => throw failure;
        if (point == "editor") f.Host.ReadEditor = () => throw failure;
        if (point == "export") f.Export = (_, _) => Task.FromException(failure);
        if (point == "notice") f.Host.Notice = (_, error) => { if (!error) throw failure; };
        await f.Coordinator.SaveTextAsync();
        Assert.Equal(("export-failed:" + point, true), f.Host.Notices.Last());
    }

    [Fact]
    public async Task ExportAwaitsBackendBeforeNotifyingAndCatchesLateFailure()
    {
        var f = new Fixture();
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Export = (_, _) => pending.Task;
        Task exporting = f.Coordinator.SaveTextAsync();
        Assert.False(exporting.IsCompleted);
        Assert.Empty(f.Host.Notices);
        pending.SetException(new IOException("late export"));
        await exporting;
        Assert.Equal(("export-failed:late export", true), Assert.Single(f.Host.Notices));
    }

    [Fact]
    public async Task ExportErrorNoticeFailureEscapesTheCatch()
    {
        var f = new Fixture();
        f.Export = (_, _) => Task.FromException(new IOException("export failed"));
        var failure = new InvalidOperationException("error notice failed");
        f.Host.Notice = (_, _) => throw failure;
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => f.Coordinator.SaveTextAsync()));
        Assert.Equal(("export-failed:export failed", true), Assert.Single(f.Host.Notices));
    }

    [Fact]
    public void SyncNormalizesNullEditorAfterReadingComponent()
    {
        var f = new Fixture(Document());
        f.Host.ReadEditor = () => null;
        f.Coordinator.SyncDocumentFromEditor();
        Assert.Equal(new[] { "component:get", "editor:get" }, f.Host.Calls);
        Assert.Equal(string.Empty, f.Document.CommandText);
    }

    private static ConfigMakerPresetDocument Document()
    {
        var document = new ConfigMakerPresetDocument { Name = "Source", ComponentId = "component", CommandText = "--source", PackId = "pack", FileName = "file", Meta = "meta", TargetVersion = "target-version" };
        var variable = new ConfigMakerVariableDefinition { Id = "variable-id", Name = "Var", Value = "one" };
        variable.Values.Add("one");
        variable.Values.Add("two");
        document.Variables.Add(variable);
        document.Resources.Add(new() { Alias = "list", Path = "resource-path", Kind = ConfigMakerResourceKind.SiteList });
        return document;
    }

    private static ConfigItem Config() => new() { name = "Source", packId = "pack", file_name = "file", target = ["component"], startup_string = "--source" };

    private sealed class Fixture
    {
        public ConfigMakerPresetDocument Document { get; }
        public Host Host { get; } = new();
        public Storage Storage { get; }
        public ConfigMakerDocumentCoordinator Coordinator { get; }
        public int StorageCreations;
        public Func<IConfigMakerDocumentStorage>? Factory;
        public Func<ConfigMakerPresetDocument, string, Task>? Export;

        public Fixture(ConfigMakerPresetDocument? document = null)
        {
            Document = document ?? new();
            Storage = new(Host.Calls);
            Coordinator = new(Document, Host, () =>
            {
                Host.Calls.Add("storage:new");
                StorageCreations++;
                return Factory == null ? Storage : Factory();
            }, (value, path) =>
            {
                Host.Calls.Add("export:write");
                return Export == null ? Task.CompletedTask : Export(value, path);
            });
        }
    }

    private sealed class Storage(List<string> calls) : IConfigMakerDocumentStorage
    {
        public ConfigMakerPresetSaveResult Result = ConfigMakerPresetSaveResult.Failed("CANCELED");
        public Func<string, ConfigMakerPresetDocument, Task<ConfigMakerPresetSaveResult>>? Save;
        public Func<ConfigMakerPresetDocument, Task<ConfigMakerPresetSaveResult>>? Overwrite;
        public Task<ConfigMakerPresetSaveResult> SaveAsync(string name, ConfigMakerPresetDocument document)
        {
            calls.Add("storage:save");
            return Save == null ? Task.FromResult(Result) : Save(name, document);
        }
        public Task<ConfigMakerPresetSaveResult> OverwriteAsync(ConfigMakerPresetDocument document)
        {
            calls.Add("storage:overwrite");
            return Overwrite == null ? Task.FromResult(Result) : Overwrite(document);
        }
    }

    private sealed class Host : IConfigMakerDocumentHost
    {
        public List<string> Calls { get; } = [];
        public List<(string Message, bool IsError)> Notices { get; } = [];
        public string CurrentComponent = "component";
        public string? CurrentEditor = "editor text";
        public bool ReadOnly;
        public Action<string>? ComponentSet;
        public Func<string?>? ReadEditor;
        public Action? Rebuild;
        public Action? Attach;
        public Action? ClearStructure;
        public Func<ConfigItem, string>? ConfigDirectory;
        public Func<ConfigItem, Task>? AutoCorrect;
        public Func<string, bool, Task<ConfigMakerSavePresetRequest?>> AskSave = (_, _) => Task.FromResult<ConfigMakerSavePresetRequest?>(new("Chosen", false));
        public Func<Task<bool>> Confirm = () => Task.FromResult(true);
        public Func<bool, string?> ExportPath = _ => "chosen.txt";
        public Action<string, bool>? Notice;
        public string ComponentId
        {
            get { Calls.Add("component:get"); return CurrentComponent; }
            set { Calls.Add("component:set:" + value); CurrentComponent = value; ComponentSet?.Invoke(value); }
        }
        public string? EditorText { get { Calls.Add("editor:get"); return ReadEditor == null ? CurrentEditor : ReadEditor(); } }
        public bool IsEditorReadOnly { get { Calls.Add("readonly:get"); return ReadOnly; } }
        public void SetEditorText(string text) { Calls.Add("editor:set:" + text); CurrentEditor = text; }
        public void SetBaseDirectory(string value) => Calls.Add("base:" + value);
        public void RebuildPresetStructure() { Calls.Add("rebuild"); Rebuild?.Invoke(); }
        public void UpdateSimpleDesignerAvailability() => Calls.Add("designer");
        public void NotifyDocumentStateChanged() => Calls.Add("document:event");
        public void AttachExistingPresetFiles() { Calls.Add("attach"); Attach?.Invoke(); }
        public Task ApplyAutoCorrectorSilentlyAsync(ConfigItem item) { Calls.Add("autocorrect"); return AutoCorrect?.Invoke(item) ?? Task.CompletedTask; }
        public string GetConfigItemBaseDirectory(ConfigItem item) { Calls.Add("base:get"); return ConfigDirectory?.Invoke(item) ?? "base-dir"; }
        public Task<ConfigMakerSavePresetRequest?> AskSavePresetAsync(string name, bool overwrite) { Calls.Add("save:ask"); return AskSave(name, overwrite); }
        public Task<bool> ConfirmNewDocumentAsync() { Calls.Add("new:confirm"); return Confirm(); }
        public string? ChooseExportPath(bool allowTextFormat) { Calls.Add("export:choose"); return ExportPath(allowTextFormat); }
        public void ClearPresetStructure() { Calls.Add("structure:clear"); ClearStructure?.Invoke(); }
        public void ClearEditorUndoHistory() => Calls.Add("undo:clear");
        public string Text(string key)
        {
            Calls.Add("text:" + key);
            return key switch
            {
                "ConfigMakerPresetSavedMessage" => "saved:{0}",
                "ConfigMakerPresetSaveFailedMessage" => "failed:{0}",
                "ConfigMakerTextSavedMessage" => "exported",
                "ConfigMakerTextSaveFailedMessage" => "export-failed:{0}",
                _ => throw new InvalidOperationException("Unexpected key " + key),
            };
        }
        public void ShowDocumentMessage(string message, bool isError)
        {
            Calls.Add($"notice:{isError}:{message}");
            Notices.Add((message, isError));
            Notice?.Invoke(message, isError);
        }
    }
}
