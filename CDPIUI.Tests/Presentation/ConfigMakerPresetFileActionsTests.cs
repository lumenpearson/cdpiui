using System.Collections.ObjectModel;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.ViewModels;

namespace CDPIUI.Tests.Presentation;

public sealed class ConfigMakerPresetFileActionsTests
{
    [Fact]
    public void AddNullThrowsSynchronouslyBeforeAnyPortCall()
    {
        var f = new Fixture();
        Assert.Equal("filePaths", Assert.Throws<ArgumentNullException>(() => f.Actions.AddPresetFiles(null!)).ParamName);
        Assert.Empty(f.Calls);
    }

    [Fact]
    public void AddPassesTheOriginalLazyEnumerableWithoutEarlyEnumeration()
    {
        var f = new Fixture();
        int enumerations = 0;
        IEnumerable<string> Values() { enumerations++; yield return "first"; yield return "second"; }
        IEnumerable<string> values = Values();
        f.Backend.Add = paths =>
        {
            Assert.Same(values, paths);
            Assert.Equal(0, enumerations);
            Assert.Equal(new[] { "first", "second" }, paths);
            return 2;
        };
        f.Actions.AddPresetFiles(values);
        Assert.Equal(1, enumerations);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonpositiveAddDoesNotAttachOrShowStatus(int count)
    {
        var f = new Fixture();
        f.Backend.Add = _ => count;
        f.Actions.AddPresetFiles(["path"]);
        Assert.Equal(new[] { "add" }, f.Calls);
    }

    [Fact]
    public void AddKeepsAttachRebuildPanelNoticeOrderAndOriginalCount()
    {
        var f = new Fixture();
        f.Backend.Add = _ => 2;
        f.Host.Attach = () => { f.Document.Resources.Add(new()); f.Document.Resources.Add(new()); f.Document.Resources.Add(new()); };
        f.Actions.AddPresetFiles(["a", "b"]);
        Assert.Equal(new[] { "add", "attach", "rebuild", "panel:True", "text:ConfigMakerPresetFilesAddedMessage", "notice:Success:added:2" }, f.Calls);
        Assert.Equal(3, f.Document.Resources.Count);
    }

    [Theory]
    [InlineData("attach")]
    [InlineData("rebuild")]
    [InlineData("notice")]
    public void AddFailureEscapesSynchronouslyWithoutReportingCatch(string point)
    {
        var f = new Fixture();
        var failure = new IOException(point);
        if (point == "attach") f.Host.Attach = () => throw failure;
        if (point == "rebuild") f.Host.Rebuild = () => throw failure;
        if (point == "notice") f.Host.Notice = (_, _) => throw failure;
        Assert.Same(failure, Assert.Throws<IOException>(() => f.Actions.AddPresetFiles(["a"])));
        Assert.Empty(f.Host.Reported);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PickerOnlyAddsNonemptyOriginalSelection(bool selected)
    {
        var f = new Fixture();
        var paths = f.Host.Picked = selected ? new[] { "b", "a", "b" } : [];
        f.Backend.Add = value => { Assert.Same(paths, value); return 0; };
        f.Actions.AddPresetFilesFromPicker();
        Assert.Equal(selected ? new[] { "picker", "add" } : new[] { "picker" }, f.Calls);
    }

    [Fact]
    public async Task OpenNullStaysInsideReportingBoundary()
    {
        var f = new Fixture();
        await f.Actions.OpenPresetFileAsync(null);
        Assert.Equal(new[] { "run" }, f.Calls);
    }

    [Fact]
    public async Task OpenMissingWarnsBeforePreferencesOrShell()
    {
        var f = new Fixture();
        f.Backend.Exists = _ => false;
        await f.Actions.OpenPresetFileAsync(File());
        Assert.Equal(("missing:" + Resolved, ConfigMakerPresetFileNoticeSeverity.Warning), Assert.Single(f.Host.Notices));
        Assert.DoesNotContain("ask-policy", f.Calls);
        Assert.DoesNotContain(f.Calls, call => call.StartsWith("shell:"));
    }

    [Theory]
    [InlineData(ConfigMakerPresetFileKind.SiteList, "data.bin")]
    [InlineData(ConfigMakerPresetFileKind.Payload, "DATA.TXT")]
    [InlineData(ConfigMakerPresetFileKind.Library, "data.txt")]
    public async Task SiteListOrTxtUsesTextRoute(ConfigMakerPresetFileKind kind, string path)
    {
        var f = new Fixture();
        f.Backend.Resolve = _ => path;
        await f.Actions.OpenPresetFileAsync(File(kind));
        Assert.Equal(new[] { "run", "resolve", "exists:" + path, "ask-policy", "shell:text:" + path }, f.Calls);
    }

    [Fact]
    public async Task OtherFilesUseDefaultAppWithoutTextPreferences()
    {
        var f = new Fixture();
        await f.Actions.OpenPresetFileAsync(File(ConfigMakerPresetFileKind.Payload));
        Assert.Contains("shell:default:" + Resolved, f.Calls);
        Assert.DoesNotContain("ask-policy", f.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApplicationDialogIsAwaitedRemembersOnlyTrueAndNeverOpensShell(bool answer)
    {
        var f = new Fixture();
        f.Backend.AskApplication = true;
        var response = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Host.ApplicationAnswer = response.Task;
        Task opening = f.Actions.OpenPresetFileAsync(File());
        Assert.False(opening.IsCompleted);
        Assert.DoesNotContain("remember", f.Calls);
        response.SetResult(answer);
        await opening;
        Assert.Equal(answer ? 1 : 0, f.Calls.Count(call => call == "remember"));
        Assert.DoesNotContain(f.Calls, call => call.StartsWith("shell:"));
    }

    [Theory]
    [InlineData("resolve")]
    [InlineData("dialog")]
    [InlineData("remember")]
    [InlineData("shell")]
    public async Task OpenFailuresReachTheOriginalReportingBoundary(string point)
    {
        var f = new Fixture();
        var failure = new IOException(point);
        if (point == "resolve") f.Backend.Resolve = _ => throw failure;
        if (point == "dialog") { f.Backend.AskApplication = true; f.Host.ApplicationAnswer = Task.FromException<bool>(failure); }
        if (point == "remember") { f.Backend.AskApplication = true; f.Backend.Remember = () => throw failure; }
        if (point == "shell") f.Backend.Shell = _ => throw failure;
        await f.Actions.OpenPresetFileAsync(File());
        Assert.Same(failure, Assert.Single(f.Host.Reported));
    }

    [Fact]
    public void ShowNullHasNoEffects()
    {
        var f = new Fixture();
        f.Actions.ShowPresetFileInFolder(null);
        Assert.Empty(f.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShowUsesResolvedPathAndOnlyExistingFileCallsShell(bool exists)
    {
        var f = new Fixture();
        f.Backend.Exists = _ => exists;
        f.Actions.ShowPresetFileInFolder(File());
        if (exists) Assert.Equal(new[] { "resolve", "exists:" + Resolved, "shell:folder:" + Resolved }, f.Calls);
        else Assert.Equal(("missing:" + Resolved, ConfigMakerPresetFileNoticeSeverity.Warning), Assert.Single(f.Host.Notices));
        Assert.DoesNotContain("run", f.Calls);
    }

    [Fact]
    public void ShowFailurePropagatesSynchronously()
    {
        var f = new Fixture();
        var failure = new IOException("show failed");
        f.Backend.Shell = _ => throw failure;
        Assert.Same(failure, Assert.Throws<IOException>(() => f.Actions.ShowPresetFileInFolder(File())));
        Assert.Empty(f.Host.Reported);
    }

    [Fact]
    public async Task RemoveNullAndUnattachedStopWithoutConfirmation()
    {
        var f = new Fixture();
        await f.Actions.RemovePresetFileAsync(null);
        await f.Actions.RemovePresetFileAsync(File());
        Assert.Equal(new[] { "run", "run", "find" }, f.Calls);
    }

    [Fact]
    public async Task RemoveUnreferencedPublishesCollectionChangeBeforeRebuildAndDiagnostics()
    {
        var f = new Fixture();
        var resource = new ConfigMakerPresetResource { Alias = "list" };
        f.Document.Resources.Add(resource);
        f.Backend.Attached = resource;
        f.Document.ContentChanged += (_, _) => f.Calls.Add("document");
        await f.Actions.RemovePresetFileAsync(File());
        Assert.Empty(f.Document.Resources);
        Assert.Equal(new[] { "run", "find", "referenced", "document", "rebuild", "diagnostics" }, f.Calls);
        Assert.DoesNotContain("confirm", f.Calls);
    }

    [Fact]
    public async Task RemoveReferencedCancellationKeepsTheSameObject()
    {
        var f = new Fixture();
        var resource = new ConfigMakerPresetResource { Alias = "list" };
        f.Document.Resources.Add(resource);
        f.Backend.Attached = resource;
        f.Backend.Referenced = true;
        f.Host.RemovalAnswer = Task.FromResult(false);
        await f.Actions.RemovePresetFileAsync(File());
        Assert.Same(resource, Assert.Single(f.Document.Resources));
        Assert.DoesNotContain("rebuild", f.Calls);
    }

    [Fact]
    public async Task RemoveUsesThePreDialogObjectEvenWhenCollectionChangesDuringAwait()
    {
        var f = new Fixture();
        var old = new ConfigMakerPresetResource { Alias = "list" };
        var newer = new ConfigMakerPresetResource { Alias = "list" };
        f.Document.Resources.Add(old);
        f.Backend.Attached = old;
        f.Backend.Referenced = true;
        var response = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Host.RemovalAnswer = response.Task;
        Task removing = f.Actions.RemovePresetFileAsync(File());
        Assert.False(removing.IsCompleted);
        f.Document.Resources.Remove(old);
        f.Document.Resources.Add(newer);
        f.Backend.Attached = newer;
        response.SetResult(true);
        await removing;
        Assert.Same(newer, Assert.Single(f.Document.Resources));
        Assert.Equal(1, f.Calls.Count(call => call == "find"));
        Assert.Equal(new[] { "rebuild", "diagnostics" }, f.Calls.TakeLast(2));
    }

    [Fact]
    public async Task RemoveSubscriberFailureKeepsTheRemovalButSkipsLaterEffects()
    {
        var f = new Fixture();
        var resource = new ConfigMakerPresetResource { Alias = "list" };
        f.Document.Resources.Add(resource);
        f.Backend.Attached = resource;
        var failure = new IOException("subscriber failed");
        f.Document.ContentChanged += (_, _) => throw failure;
        await f.Actions.RemovePresetFileAsync(File());
        Assert.Empty(f.Document.Resources);
        Assert.Same(failure, Assert.Single(f.Host.Reported));
        Assert.DoesNotContain("rebuild", f.Calls);
        Assert.DoesNotContain("diagnostics", f.Calls);
    }

    [Theory]
    [InlineData("confirm")]
    [InlineData("rebuild")]
    [InlineData("diagnostics")]
    public async Task RemoveFailuresKeepTheOriginalPartialState(string point)
    {
        var f = new Fixture();
        var resource = new ConfigMakerPresetResource { Alias = "list" };
        f.Document.Resources.Add(resource);
        f.Backend.Attached = resource;
        var failure = new IOException(point);
        if (point == "confirm") { f.Backend.Referenced = true; f.Host.RemovalAnswer = Task.FromException<bool>(failure); }
        if (point == "rebuild") f.Host.Rebuild = () => throw failure;
        if (point == "diagnostics") f.Host.Diagnostics = () => throw failure;
        await f.Actions.RemovePresetFileAsync(File());
        Assert.Same(failure, Assert.Single(f.Host.Reported));
        Assert.Equal(point == "confirm" ? 1 : 0, f.Document.Resources.Count);
    }

    [Fact]
    public void ReadonlyInsertDoesNotReadAnInvalidFilePath()
    {
        var f = new Fixture();
        f.Host.ReadOnly = true;
        f.Actions.InsertPresetFileReference(new("invalid", null!, ""));
        Assert.Equal(new[] { "readonly" }, f.Calls);
    }

    [Fact]
    public void NullInsertOnlyReadsReadonly()
    {
        var f = new Fixture();
        f.Actions.InsertPresetFileReference(null);
        Assert.Equal(new[] { "readonly" }, f.Calls);
    }

    [Fact]
    public void PresetUriKeepsCaseAndBypassesLookupAfterOriginalTrimming()
    {
        var f = new Fixture();
        f.Actions.InsertPresetFileReference(File() with { Path = "  \"PRESET://Mixed Alias\"  " });
        Assert.Equal(new[] { "readonly", "insert:\"PRESET://Mixed Alias\"" }, f.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void UnresolvedInsertKeepsTheTrimmedOriginalReference(string? resolved)
    {
        var f = new Fixture();
        f.Backend.TryResolve = _ => resolved;
        f.Actions.InsertPresetFileReference(File() with { Path = "  \"relative.txt\"  " });
        Assert.Equal("\"relative.txt\"", Assert.Single(f.Host.Inserted));
        Assert.DoesNotContain(f.Calls, call => call.StartsWith("exists:"));
        Assert.Empty(f.Backend.Creates);
    }

    [Fact]
    public void ExistingFileUsesFullPathAndKindAndReadsAliasAfterAddedResourceRebuild()
    {
        var f = new Fixture();
        string raw = Path.Combine(Path.GetTempPath(), "unused", "..", "reference.txt");
        string full = Path.GetFullPath(raw);
        f.Backend.TryResolve = _ => raw;
        f.Backend.Added = true;
        f.Host.Rebuild = () => f.Backend.Resource.Alias = "late.txt";
        f.Actions.InsertPresetFileReference(File(ConfigMakerPresetFileKind.Payload));
        Assert.Equal((full, ConfigMakerPresetFileKind.Payload), Assert.Single(f.Backend.Creates));
        Assert.Contains("exists:" + raw, f.Calls);
        Assert.Equal("\"preset://late.txt\"", Assert.Single(f.Host.Inserted));
        Assert.Same(f.Backend.Resource, Assert.Single(f.Document.Resources));
    }

    [Fact]
    public void ExistingResourceDoesNotRebuild()
    {
        var f = new Fixture();
        f.Document.Resources.Add(f.Backend.Resource);
        f.Actions.InsertPresetFileReference(File());
        Assert.DoesNotContain("rebuild", f.Calls);
        Assert.Equal("\"preset://existing.txt\"", Assert.Single(f.Host.Inserted));
        Assert.Same(f.Backend.Resource, Assert.Single(f.Document.Resources));
    }

    [Theory]
    [InlineData("resolve")]
    [InlineData("exists")]
    [InlineData("create")]
    [InlineData("rebuild")]
    [InlineData("insert")]
    public void InsertFailuresRemainSynchronousAndPreservePriorRegistration(string point)
    {
        var f = new Fixture();
        var failure = new IOException(point);
        if (point == "resolve") f.Backend.TryResolve = _ => throw failure;
        if (point == "exists") f.Backend.Exists = _ => throw failure;
        if (point == "create") f.Backend.Create = (_, _) => throw failure;
        if (point == "rebuild") { f.Backend.Added = true; f.Host.Rebuild = () => throw failure; }
        if (point == "insert") f.Host.Insert = _ => throw failure;
        Assert.Same(failure, Assert.Throws<IOException>(() => f.Actions.InsertPresetFileReference(File())));
        Assert.Empty(f.Host.Reported);
        if (point == "rebuild") Assert.Same(f.Backend.Resource, Assert.Single(f.Document.Resources));
    }

    private static readonly string Resolved = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "cdpiui-file-actions", "data.bin"));
    private static ConfigMakerPresetFileInfo File(ConfigMakerPresetFileKind kind = ConfigMakerPresetFileKind.SiteList) => new("file", "source-path", "folder", kind);

    private sealed class Fixture
    {
        public List<string> Calls { get; } = [];
        public ConfigMakerPresetDocument Document { get; } = new();
        public Host Host { get; }
        public Backend Backend { get; }
        public ConfigMakerPresetFileActions Actions { get; }
        public Fixture()
        {
            Host = new(Calls);
            Backend = new(Calls, Document.Resources);
            Actions = new(Document.Resources, Host, Backend);
        }
    }

    private sealed class Backend(List<string> calls, ObservableCollection<ConfigMakerPresetResource> resources) : IConfigMakerPresetFileActionsBackend
    {
        public Func<IEnumerable<string>, int> Add = _ => 1;
        public Func<string, string> Resolve = _ => Resolved;
        public Func<string, string?> TryResolve = _ => Resolved;
        public Func<string, bool> Exists = _ => true;
        public bool AskApplication, Referenced, Added;
        public ConfigMakerPresetResource? Attached;
        public ConfigMakerPresetResource Resource { get; } = new() { Alias = "existing.txt", Path = Resolved };
        public List<(string Path, ConfigMakerPresetFileKind Kind)> Creates { get; } = [];
        public Action<string, ConfigMakerPresetFileKind>? Create;
        public Action? Remember;
        public Action<string>? Shell;
        public int AddFiles(IEnumerable<string> paths) { calls.Add("add"); return Add(paths); }
        public string ResolvePath(string path) { calls.Add("resolve"); return Resolve(path); }
        public string? TryResolvePath(string path) { calls.Add("try-resolve"); return TryResolve(path); }
        public bool FileExists(string path) { calls.Add("exists:" + path); return Exists(path); }
        public ConfigMakerPresetResource GetOrCreate(string path, ConfigMakerPresetFileKind kind, out bool added)
        {
            calls.Add("create"); Creates.Add((path, kind)); Create?.Invoke(path, kind); added = Added;
            if (added) resources.Add(Resource);
            return Resource;
        }
        public ConfigMakerPresetResource? FindAttached(string path) { calls.Add("find"); return Attached; }
        public bool IsReferenced(ConfigMakerPresetResource resource) { Assert.Same(Attached, resource); calls.Add("referenced"); return Referenced; }
        public bool ShouldAskForTextApplication() { calls.Add("ask-policy"); return AskApplication; }
        public void RememberTextFileDialogShown() { calls.Add("remember"); Remember?.Invoke(); }
        public void OpenTextFile(string path) { calls.Add("shell:text:" + path); Shell?.Invoke(path); }
        public void OpenFileInDefaultApp(string path) { calls.Add("shell:default:" + path); Shell?.Invoke(path); }
        public void ShowFileInFolder(string path) { calls.Add("shell:folder:" + path); Shell?.Invoke(path); }
    }

    private sealed class Host(List<string> calls) : IConfigMakerPresetFileActionsHost
    {
        public List<(string Message, ConfigMakerPresetFileNoticeSeverity Severity)> Notices { get; } = [];
        public List<Exception> Reported { get; } = [];
        public List<string> Inserted { get; } = [];
        public IReadOnlyList<string> Picked = [];
        public bool ReadOnly;
        public Task<bool> ApplicationAnswer = Task.FromResult(true), RemovalAnswer = Task.FromResult(true);
        public Action? Attach, Rebuild, Diagnostics;
        public Action<string>? Insert;
        public Action<string, ConfigMakerPresetFileNoticeSeverity>? Notice;
        public async Task RunReportingErrorsAsync(Func<Task> action) { calls.Add("run"); try { await action(); } catch (Exception error) { Reported.Add(error); } }
        public IReadOnlyList<string> ChoosePresetFilesToAdd() { calls.Add("picker"); return Picked; }
        public void AttachExistingPresetFiles() { calls.Add("attach"); Attach?.Invoke(); }
        public void RebuildPresetStructure() { calls.Add("rebuild"); Rebuild?.Invoke(); }
        public void SetPresetFilesPanelVisible(bool visible) => calls.Add("panel:" + visible);
        public string Text(string key) { calls.Add("text:" + key); return key switch { "ConfigMakerPresetFilesAddedMessage" => "added:{0}", "ConfigMakerPresetFileMissingMessage" => "missing:{0}", _ => throw new InvalidOperationException("Unexpected key " + key) }; }
        public void ShowPresetFileMessage(string message, ConfigMakerPresetFileNoticeSeverity severity) { calls.Add($"notice:{severity}:{message}"); Notices.Add((message, severity)); Notice?.Invoke(message, severity); }
        public Task<bool> AskTextFileApplicationAsync(string path) { Assert.Equal(Resolved, path); calls.Add("application"); return ApplicationAnswer; }
        public Task<bool> ConfirmRemoveReferencedFileAsync() { calls.Add("confirm"); return RemovalAnswer; }
        public void ScheduleDiagnostics() { calls.Add("diagnostics"); Diagnostics?.Invoke(); }
        public bool IsEditorReadOnly { get { calls.Add("readonly"); return ReadOnly; } }
        public void InsertAtCursor(string reference) { calls.Add("insert:" + reference); Inserted.Add(reference); Insert?.Invoke(reference); }
    }
}
