using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.ViewModels.ConfigMaker;

namespace CDPIUI.Tests.Presentation;

public sealed class ConfigMakerPresetFileReplacerTests
{
    [Fact]
    public async Task NullReplacementIsStillInsideTheHostReportingBoundary()
    {
        var f = new Fixture();
        await f.Replacer.ReplacePresetFileAsync(null);
        Assert.Equal(new[] { "run" }, f.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task InvalidResolvedPathShowsErrorWithoutFileQuery(string? resolved)
    {
        var f = new Fixture();
        f.Backend.Resolve = _ => resolved;
        await f.Replacer.ReplacePresetFileAsync(Original);
        Assert.Equal(("invalid", ConfigMakerPresetFileNoticeSeverity.Error), Assert.Single(f.Host.Notices));
        Assert.DoesNotContain(f.Calls, call => call.StartsWith("exists:"));
        Assert.DoesNotContain("ask", f.Calls);
    }

    [Fact]
    public async Task NowExistingFileOnlyRebuildsWithoutOpeningDialog()
    {
        var f = new Fixture();
        f.Backend.Existing.Add(Missing);
        await f.Replacer.ReplacePresetFileAsync(Original);
        Assert.Equal(new[] { "run", "resolve:" + Original.Path, "exists:" + Missing, "rebuild" }, f.Calls);
        Assert.Empty(f.Host.Notices);
    }

    [Theory]
    [InlineData(ConfigMakerFileReplacementChoice.Cancel)]
    [InlineData((ConfigMakerFileReplacementChoice)99)]
    public async Task CanceledOrUnknownChoiceDoesNotCreateCorrectorOrPickFile(ConfigMakerFileReplacementChoice choice)
    {
        var f = new Fixture();
        f.Host.Choice = Task.FromResult(choice);
        await f.Replacer.ReplacePresetFileAsync(Original);
        Assert.Equal(0, f.Backend.CorrectorCreations);
        Assert.DoesNotContain("choose:" + Missing, f.Calls);
        Assert.Empty(f.Host.Notices);
    }

    [Fact]
    public async Task ManualChoiceWaitsForDialogAndBlankPickerCancels()
    {
        var f = new Fixture();
        var answer = new TaskCompletionSource<ConfigMakerFileReplacementChoice>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Host.Choice = answer.Task;
        f.Host.Selected = " ";
        Task replacing = f.Replacer.ReplacePresetFileAsync(Original);
        Assert.False(replacing.IsCompleted);
        Assert.DoesNotContain("choose:" + Missing, f.Calls);
        answer.SetResult(ConfigMakerFileReplacementChoice.ChooseManually);
        await replacing;
        Assert.Contains("choose:" + Missing, f.Calls);
        Assert.Equal(0, f.Backend.UpdateCalls);
        Assert.Empty(f.Host.Notices);
    }

    [Fact]
    public async Task ManualReplacementUsesTheOriginalFileAndNormalizedPickerPath()
    {
        var f = new Fixture();
        f.Host.Choice = Task.FromResult(ConfigMakerFileReplacementChoice.ChooseManually);
        f.Host.Selected = RawReplacement;
        f.Backend.Update = (file, path) => { Assert.Same(Original, file); Assert.Equal(Replacement, path); return true; };
        await f.Replacer.ReplacePresetFileAsync(Original);
        Assert.Equal(("replaced:" + Replacement, ConfigMakerPresetFileNoticeSeverity.Success), Assert.Single(f.Host.Notices));
        Assert.Equal(0, f.Backend.CorrectorCreations);
        Assert.Empty(f.Host.Replacements);
    }

    [Fact]
    public async Task AutoEmptySuggestionCreatesThenRebuildsAndDoesNotSearch()
    {
        var f = AutoFixture();
        f.Backend.Corrector.Empty = true;
        await f.Replacer.ReplacePresetFileAsync(Original);
        Assert.Equal(new[] { Missing }, f.Backend.Created);
        Assert.DoesNotContain("find:" + Missing, f.Calls);
        Assert.DoesNotContain("choose:" + Missing, f.Calls);
        Assert.Equal(("empty:" + Missing, ConfigMakerPresetFileNoticeSeverity.Success), Assert.Single(f.Host.Notices));
        Assert.True(f.Calls.IndexOf("create:" + Missing) < f.Calls.IndexOf("rebuild"));
    }

    [Fact]
    public async Task AutoFactoryIsLazyAndSearchIsAwaited()
    {
        var f = new Fixture();
        var choice = new TaskCompletionSource<ConfigMakerFileReplacementChoice>(TaskCreationOptions.RunContinuationsAsynchronously);
        var suggestion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Host.Choice = choice.Task;
        f.Backend.Corrector.Finding = suggestion.Task;
        Task replacing = f.Replacer.ReplacePresetFileAsync(Original);
        Assert.Equal(0, f.Backend.CorrectorCreations);
        choice.SetResult(ConfigMakerFileReplacementChoice.AutoCorrect);
        await f.Backend.Corrector.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, f.Backend.CorrectorCreations);
        Assert.False(replacing.IsCompleted);
        Assert.Equal(0, f.Backend.UpdateCalls);
        suggestion.SetResult(Replacement);
        await replacing;
        Assert.Equal(1, f.Backend.UpdateCalls);
        Assert.DoesNotContain("choose:" + Missing, f.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task EmptySearchWarnsBeforeManualFallback(string? suggestion)
    {
        var f = AutoFixture();
        f.Backend.Corrector.Suggestion = suggestion;
        f.Host.Selected = null;
        await f.Replacer.ReplacePresetFileAsync(Original);
        Assert.Equal(("no suggestion", ConfigMakerPresetFileNoticeSeverity.Warning), Assert.Single(f.Host.Notices));
        Assert.True(f.Calls.IndexOf("notice:Warning") < f.Calls.IndexOf("choose:" + Missing));
        Assert.Equal(0, f.Backend.UpdateCalls);
    }

    [Fact]
    public async Task NonemptyButMissingSuggestionDoesNotFallBackToPicker()
    {
        var f = AutoFixture();
        f.Backend.Corrector.Suggestion = Path.Combine(Path.GetTempPath(), "missing-suggestion.txt");
        await f.Replacer.ReplacePresetFileAsync(Original);
        Assert.Equal(ConfigMakerPresetFileNoticeSeverity.Error, Assert.Single(f.Host.Notices).Severity);
        Assert.DoesNotContain("choose:" + Missing, f.Calls);
        Assert.Equal(0, f.Backend.UpdateCalls);
    }

    [Theory]
    [InlineData("resolve")]
    [InlineData("factory")]
    [InlineData("suggest")]
    [InlineData("find-sync")]
    [InlineData("find-async")]
    public async Task ErrorsOutsideTryApplyReachTheHostReportingBoundary(string point)
    {
        var f = AutoFixture();
        var error = new IOException(point);
        if (point == "resolve") f.Backend.Resolve = _ => throw error;
        if (point == "factory") f.Backend.Factory = () => throw error;
        if (point == "suggest") f.Backend.Corrector.CheckEmpty = _ => throw error;
        if (point == "find-sync") f.Backend.Corrector.Find = _ => throw error;
        if (point == "find-async") f.Backend.Corrector.Finding = Task.FromException<string?>(error);
        await f.Replacer.ReplacePresetFileAsync(Original);
        Assert.Same(error, Assert.Single(f.Host.Reported));
        Assert.Empty(f.Host.Notices);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AttachedReplacementDoesNotEditCommandExplicitArrayOrRaiseReplacement(bool show)
    {
        var f = new Fixture();
        f.Backend.Update = (_, _) => true;
        f.Host.Explicit = true;
        var files = f.Host.Files = [Original];
        Assert.True(f.Replacer.TryApplyPresetFileReplacement(Original, Replacement, show));
        Assert.Same(files, f.Host.Files);
        Assert.Empty(f.Host.Commands);
        Assert.Empty(f.Host.Replacements);
        Assert.Equal(0, f.Backend.ReferenceCalls);
        Assert.Equal(show ? 1 : 0, f.Host.Notices.Count);
        Assert.Contains("rebuild", f.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingReplacementStopsBeforeResourcesAndRespectsSilentFlag(bool show)
    {
        var f = new Fixture();
        f.Backend.Existing.Clear();
        Assert.False(f.Replacer.TryApplyPresetFileReplacement(Original, RawReplacement, show));
        Assert.Contains("exists:" + Replacement, f.Calls);
        Assert.Equal(0, f.Backend.UpdateCalls);
        Assert.Equal(show ? 1 : 0, f.Host.Notices.Count);
        if (show) Assert.Equal(("failed:" + new FileNotFoundException(null, Replacement).Message, ConfigMakerPresetFileNoticeSeverity.Error), f.Host.Notices[0]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingReferenceResultBecomesLocalizedFailureOnlyWhenShown(bool show)
    {
        var f = new Fixture();
        f.Backend.Rewrite = (_, _) => null;
        Assert.False(f.Replacer.TryApplyPresetFileReplacement(Original, Replacement, show));
        Assert.DoesNotContain("rebuild", f.Calls);
        Assert.Empty(f.Host.Replacements);
        Assert.Contains("text:ConfigMakerPresetFileReferenceNotFoundMessage", f.Calls);
        Assert.Equal(show ? 1 : 0, f.Host.Notices.Count);
        if (show) Assert.Equal(("failed:reference not found", ConfigMakerPresetFileNoticeSeverity.Error), f.Host.Notices[0]);
    }

    [Fact]
    public void ExplicitFilesMatchByIdentityOrNormalizedPathAndKindAndRetainOtherFields()
    {
        var f = new Fixture();
        f.Host.Explicit = true;
        var equivalent = Original with { Name = "second", Path = "\"C:\\MISSING\\source.txt\"", OptionName = "--other", IsAttachedResource = true };
        var otherKind = Original with { Kind = ConfigMakerPresetFileKind.Payload };
        var unrelated = Original with { Path = "C:/missing/unrelated.txt" };
        var before = f.Host.Files = [Original, equivalent, otherKind, unrelated];
        Assert.True(f.Replacer.TryApplyPresetFileReplacement(Original, RawReplacement, false));
        Assert.NotSame(before, f.Host.Files);
        Assert.Equal(4, f.Host.Files.Length);
        foreach (var updated in f.Host.Files.Take(2))
        {
            Assert.Equal("replacement.txt", updated.Name);
            Assert.Equal("preset://replacement.txt", updated.Path);
            Assert.Equal("preset:/", updated.Folder);
            Assert.Equal(ConfigMakerPresetFileKind.SiteList, updated.Kind);
        }
        Assert.Equal("--other", f.Host.Files[1].OptionName);
        Assert.True(f.Host.Files[1].IsAttachedResource);
        Assert.Same(otherKind, f.Host.Files[2]);
        Assert.Same(unrelated, f.Host.Files[3]);
        Assert.Same(Original, before[0]);
    }

    [Fact]
    public void ExplicitStateIsReadAfterReferenceRewriteCallback()
    {
        var f = new Fixture();
        var late = Original with { Name = "late" };
        f.Backend.Rewrite = (_, _) => { f.Host.Explicit = true; f.Host.Files = [late]; return DefaultRewrite; };
        Assert.True(f.Replacer.TryApplyPresetFileReplacement(Original, Replacement, false));
        Assert.Equal("preset://replacement.txt", Assert.Single(f.Host.Files).Path);
        Assert.NotSame(late, f.Host.Files[0]);
    }

    [Fact]
    public void CommandIsFormattedAfterExplicitFilesAndEventReadsThePostRebuildCommand()
    {
        var f = new Fixture();
        f.Host.Explicit = true;
        f.Host.Files = [Original];
        f.Host.CommandSet = _ => Assert.Equal("preset://replacement.txt", Assert.Single(f.Host.Files).Path);
        f.Host.Rebuild = () => f.Host.CurrentCommand = "changed by structure listener";
        Assert.True(f.Replacer.TryApplyPresetFileReplacement(Original, Replacement, true));
        Assert.Equal("--alpha 1" + Environment.NewLine + "--beta 2", Assert.Single(f.Host.Commands));
        Assert.Equal(("changed by structure listener", Original.Path, "preset://replacement.txt"), Assert.Single(f.Host.Replacements));
        Assert.True(f.Calls.IndexOf("event") < f.Calls.IndexOf("notice:Success"));
    }

    [Fact]
    public void UnchangedCommandStillRebuildsAndRaisesReplacementWithoutSetter()
    {
        var f = new Fixture();
        f.Backend.Rewrite = (_, _) => DefaultRewrite with { CommandChanged = false };
        Assert.True(f.Replacer.TryApplyPresetFileReplacement(Original, Replacement, false));
        Assert.Empty(f.Host.Commands);
        Assert.Equal(("initial command", Original.Path, "preset://replacement.txt"), Assert.Single(f.Host.Replacements));
        Assert.Contains("rebuild", f.Calls);
        Assert.DoesNotContain(f.Calls, call => call.StartsWith("text:"));
    }

    [Theory]
    [InlineData("update")]
    [InlineData("rewrite")]
    [InlineData("files")]
    [InlineData("command")]
    [InlineData("rebuild")]
    [InlineData("event")]
    [InlineData("success-notice")]
    public void TryApplyCatchesFailuresAndRetainsEarlierMutations(string point)
    {
        var f = new Fixture();
        var error = new IOException(point);
        f.Host.Explicit = true;
        f.Host.Files = [Original];
        if (point == "update") f.Backend.Update = (_, _) => throw error;
        if (point == "rewrite") f.Backend.Rewrite = (_, _) => throw error;
        if (point == "files") f.Host.FilesSet = _ => throw error;
        if (point == "command") f.Host.CommandSet = _ => throw error;
        if (point == "rebuild") f.Host.Rebuild = () => throw error;
        if (point == "event") f.Host.Replaced = (_, _) => throw error;
        if (point == "success-notice") f.Host.Notice = (_, severity) => { if (severity == ConfigMakerPresetFileNoticeSeverity.Success) throw error; };
        Assert.False(f.Replacer.TryApplyPresetFileReplacement(Original, Replacement, true));
        Assert.Equal(("failed:" + point, ConfigMakerPresetFileNoticeSeverity.Error), f.Host.Notices.Last());
        if (point is "command" or "rebuild" or "event" or "success-notice") Assert.Equal("preset://replacement.txt", Assert.Single(f.Host.Files).Path);
        if (point is "rebuild" or "event" or "success-notice") Assert.Single(f.Host.Commands);
        if (point is "event" or "success-notice") Assert.Single(f.Host.Replacements);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyFileSuccessHonorsStatusFlag(bool show)
    {
        var f = new Fixture();
        Assert.True(f.Replacer.TryCreateEmptyPresetFile(Missing, show));
        Assert.Equal(new[] { Missing }, f.Backend.Created);
        Assert.Contains("rebuild", f.Calls);
        Assert.Equal(show ? 1 : 0, f.Host.Notices.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyFileCreationFailureIsSwallowedAndSilentModeDoesNotLocalize(bool show)
    {
        var f = new Fixture();
        f.Backend.Create = _ => throw new IOException("create failed");
        Assert.False(f.Replacer.TryCreateEmptyPresetFile(Missing, show));
        Assert.DoesNotContain("rebuild", f.Calls);
        Assert.Equal(show ? 1 : 0, f.Host.Notices.Count);
        if (!show) Assert.DoesNotContain(f.Calls, call => call.StartsWith("text:"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RebuildFailureAfterEmptyCreationDoesNotRollBackTheExternalEffect(bool show)
    {
        var f = new Fixture();
        f.Host.Rebuild = () => throw new IOException("rebuild failed");
        Assert.False(f.Replacer.TryCreateEmptyPresetFile(Missing, show));
        Assert.Contains(Missing, f.Backend.Created);
        Assert.Contains(Missing, f.Backend.Existing);
        Assert.Equal(show ? 1 : 0, f.Host.Notices.Count);
    }

    [Fact]
    public async Task LaterExistingPathSkipsAutoCreationAfterPriorEmptyCreation()
    {
        var f = AutoFixture();
        f.Backend.Corrector.Empty = true;
        await f.Replacer.ReplacePresetFileAsync(Original);
        await f.Replacer.ReplacePresetFileAsync(Original);
        Assert.Equal(1, f.Backend.CorrectorCreations);
        Assert.Single(f.Backend.Created);
    }

    [Fact]
    public void ErrorNoticeFailureEscapesTryApplyCatch()
    {
        var f = new Fixture();
        f.Backend.Update = (_, _) => throw new IOException("operation");
        var error = new InvalidOperationException("notice failed");
        f.Host.Notice = (_, _) => throw error;
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => f.Replacer.TryApplyPresetFileReplacement(Original, Replacement, true)));
    }

    [Fact]
    public async Task WarningNoticeFailureStopsFallbackAndReachesOuterReporting()
    {
        var f = AutoFixture();
        var error = new IOException("warning failed");
        f.Host.Notice = (_, _) => throw error;
        await f.Replacer.ReplacePresetFileAsync(Original);
        Assert.Same(error, Assert.Single(f.Host.Reported));
        Assert.DoesNotContain("choose:" + Missing, f.Calls);
    }

    private static readonly string Missing = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "cdpiui-replacer-fixture", "missing.txt"));
    private static readonly string RawReplacement = Path.Combine(Path.GetTempPath(), "unused", "..", "replacement.txt");
    private static readonly string Replacement = Path.GetFullPath(RawReplacement);
    private static readonly ConfigMakerPresetFileInfo Original = new("source.txt", "C:/missing/source.txt", "C:/missing", ConfigMakerPresetFileKind.SiteList, "--hostlist");
    private static readonly ConfigMakerPresetFileReplacement DefaultRewrite = new("preset://replacement.txt", "--alpha 1 --beta 2", true);
    private static Fixture AutoFixture()
    {
        var f = new Fixture();
        f.Host.Choice = Task.FromResult(ConfigMakerFileReplacementChoice.AutoCorrect);
        return f;
    }

    private sealed class Fixture
    {
        public List<string> Calls { get; } = [];
        public Host Host { get; }
        public Backend Backend { get; }
        public ConfigMakerPresetFileReplacer Replacer { get; }
        public Fixture()
        {
            Host = new(Calls);
            Backend = new(Calls);
            Replacer = new(Host, Backend);
        }
    }

    private sealed class Backend(List<string> calls) : IConfigMakerPresetFileReplacementBackend
    {
        public HashSet<string> Existing { get; } = new(StringComparer.OrdinalIgnoreCase) { Replacement };
        public List<string> Created { get; } = [];
        public int CorrectorCreations, UpdateCalls, ReferenceCalls;
        public Corrector Corrector { get; } = new(calls);
        public Func<string, string?> Resolve = _ => Missing;
        public Action<string>? Create;
        public Func<ConfigMakerPresetFileInfo, string, bool> Update = (_, _) => false;
        public Func<ConfigMakerPresetFileInfo, string, ConfigMakerPresetFileReplacement?> Rewrite = (_, _) => DefaultRewrite;
        public Func<IConfigMakerPresetFileAutoCorrector>? Factory;
        public string? TryResolvePath(string source) { calls.Add("resolve:" + source); return Resolve(source); }
        public bool FileExists(string path) { calls.Add("exists:" + path); return Existing.Contains(path); }
        public void CreateEmptyFile(string path) { calls.Add("create:" + path); Create?.Invoke(path); Created.Add(path); Existing.Add(path); }
        public bool TryUpdateAttachedResource(ConfigMakerPresetFileInfo file, string path) { calls.Add("update"); UpdateCalls++; return Update(file, path); }
        public ConfigMakerPresetFileReplacement? ReplaceReferences(ConfigMakerPresetFileInfo file, string path) { calls.Add("rewrite"); ReferenceCalls++; return Rewrite(file, path); }
        public IConfigMakerPresetFileAutoCorrector CreateAutoCorrector() { calls.Add("factory"); CorrectorCreations++; return Factory == null ? Corrector : Factory(); }
    }

    private sealed class Corrector(List<string> calls) : IConfigMakerPresetFileAutoCorrector
    {
        public bool Empty;
        public string? Suggestion = Replacement;
        public Task<string?>? Finding;
        public Func<string, bool>? CheckEmpty;
        public Func<string, Task<string?>>? Find;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool ShouldSuggestEmptyFile(string path) { calls.Add("suggest:" + path); return CheckEmpty == null ? Empty : CheckEmpty(path); }
        public Task<string?> FindReplacementAsync(string path) { calls.Add("find:" + path); Started.TrySetResult(); return Find != null ? Find(path) : Finding ?? Task.FromResult(Suggestion); }
    }

    private sealed class Host(List<string> calls) : IConfigMakerPresetFileReplacementHost
    {
        public List<(string Message, ConfigMakerPresetFileNoticeSeverity Severity)> Notices { get; } = [];
        public List<Exception> Reported { get; } = [];
        public List<string> Commands { get; } = [];
        public List<(string Command, string Original, string Replacement)> Replacements { get; } = [];
        public Task<ConfigMakerFileReplacementChoice> Choice = Task.FromResult(ConfigMakerFileReplacementChoice.Cancel);
        public string? Selected = Replacement;
        public bool Explicit;
        public ConfigMakerPresetFileInfo[] Files = [];
        public string CurrentCommand = "initial command";
        public Action? Rebuild;
        public Action<ConfigMakerPresetFileInfo[]>? FilesSet;
        public Action<string>? CommandSet;
        public Action<string, string>? Replaced;
        public Action<string, ConfigMakerPresetFileNoticeSeverity>? Notice;
        public async Task RunReportingErrorsAsync(Func<Task> action)
        {
            calls.Add("run");
            try { await action(); }
            catch (Exception error) { Reported.Add(error); calls.Add("reported"); }
        }
        public Task<ConfigMakerFileReplacementChoice> AskPresetFileReplacementAsync(string path) { Assert.Equal(Missing, path); calls.Add("ask"); return Choice; }
        public string? ChooseReplacementFile(string path) { calls.Add("choose:" + path); return Selected; }
        public string Text(string key)
        {
            calls.Add("text:" + key);
            return key switch
            {
                "ConfigMakerPresetFileInvalidPathMessage" => "invalid",
                "ConfigMakerPresetFileNoSuggestionMessage" => "no suggestion",
                "ConfigMakerPresetFileReferenceNotFoundMessage" => "reference not found",
                "ConfigMakerPresetFileEmptyCreatedMessage" => "empty:{0}",
                "ConfigMakerPresetFileReplacedMessage" => "replaced:{0}",
                "ConfigMakerPresetFileReplaceFailedMessage" => "failed:{0}",
                _ => throw new InvalidOperationException("Unexpected localization key " + key),
            };
        }
        public void ShowPresetFileMessage(string message, ConfigMakerPresetFileNoticeSeverity severity) { calls.Add("notice:" + severity); Notices.Add((message, severity)); Notice?.Invoke(message, severity); }
        public void RebuildPresetStructure() { calls.Add("rebuild"); Rebuild?.Invoke(); }
        public bool UsesExplicitPresetStructure { get { calls.Add("explicit:uses"); return Explicit; } }
        public ConfigMakerPresetFileInfo[] ExplicitPresetFiles
        {
            get { calls.Add("explicit:get"); return Files; }
            set { calls.Add("explicit:set"); Files = value; FilesSet?.Invoke(value); }
        }
        public void SetCommandText(string value) { calls.Add("command"); Commands.Add(value); CurrentCommand = value; CommandSet?.Invoke(value); }
        public void NotifyPresetFileReplaced(string original, string replacement) { calls.Add("event"); Replacements.Add((CurrentCommand, original, replacement)); Replaced?.Invoke(original, replacement); }
    }
}
