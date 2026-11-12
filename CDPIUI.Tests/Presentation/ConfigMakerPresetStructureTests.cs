using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Helper.CreateConfigHelper;

namespace CDPIUI.Tests.Presentation;

public sealed class ConfigMakerPresetStructureTests
{
    [Fact]
    public void ConstructorStoresPortsWithoutReadingAndPreservesColdVisibility()
    {
        var f = new Fixture();
        Assert.Empty(f.Trace); Assert.Empty(f.Owner.PresetFileNodes); Assert.Empty(f.Owner.PresetGroupNodes);
        Assert.False(f.Owner.HasPresetFiles); Assert.True(f.Owner.HasPresetGroups); Assert.True(f.Owner.IsPresetStructureVisible);
    }

    [Fact]
    public void DetectedThenAttachedFilesUseFirstKindAndOrdinalIgnoreCasePath()
    {
        var a = File("A.txt"); var duplicate = File("a.TXT");
        var library = File("A.txt") with { Kind = ConfigMakerPresetFileKind.Library };
        var attached = File("B.txt") with { IsAttachedResource = true };
        var f = new Fixture { Detected = [a, duplicate, library], Attached = [duplicate, attached] };
        f.Owner.Rebuild();
        Assert.Equal([a, library, attached], f.FileInputs.Single());
        Assert.Same(a, f.FileInputs[0][0]); Assert.Same(attached, f.FileInputs[0][2]);
        Assert.True(f.Owner.HasPresetFiles);
    }

    [Fact]
    public void ExplicitFilesFilterBlanksAndDeduplicateWithoutLosingFirstObject()
    {
        var a = File("a"); var b = File("b");
        var f = new Fixture { Attached = [File("A"), b] };
        f.Owner.Set([File(" "), File(null!), a, File("A"), a with { Kind = ConfigMakerPresetFileKind.Payload }], []);
        Assert.Equal([a, a with { Kind = ConfigMakerPresetFileKind.Payload }, b], f.FileInputs.Single());
        Assert.Same(a, f.FileInputs[0][0]);
    }

    [Fact]
    public void EnrichmentCopiesOnlyMissingOptionFromFirstMatchingKindAndPath()
    {
        var e = File("A.txt"); var withOption = File("b") with { OptionName = "--kept" };
        var noMatch = File("c");
        var f = new Fixture { Detected = [File("a.TXT") with { OptionName = "--first" }, File("a.txt") with { OptionName = "--second" }, File("b") with { OptionName = "--ignored" }, File("c") with { Kind = ConfigMakerPresetFileKind.Library, OptionName = "--wrong-kind" }] };
        f.Owner.Set([e, withOption, noMatch], []);
        var actual = f.FileInputs.Single();
        Assert.NotSame(e, actual[0]); Assert.Equal(e with { OptionName = "--first" }, actual[0]);
        Assert.Same(withOption, actual[1]); Assert.Same(noMatch, actual[2]);
        Assert.Equal("", e.OptionName);
    }

    [Fact]
    public void ExplicitEmptyDoesNotFallBackToDetectedButStillIncludesAttached()
    {
        var a = File("attached"); var f = new Fixture { Detected = [File("detected")], Attached = [a] };
        f.Owner.Set([], []);
        Assert.Same(a, Assert.Single(f.FileInputs.Single()));
    }

    [Fact]
    public void GroupsAreMaterializedAndRetainOriginalGroupObjects()
    {
        var group = new ConfigMakerPresetGroupInfo("original", ["detail"]);
        var input = new List<ConfigMakerPresetGroupInfo> { group };
        var f = new Fixture(); f.Owner.Set([], input); input.Clear(); f.Owner.Rebuild();
        Assert.Same(group, Assert.Single(f.GroupInputs.Last().Groups));
        Assert.Equal("detail", Assert.Single(f.GroupInputs.Last().Groups[0].Details));
    }

    [Fact]
    public void RealParserReceivesSecondLiveCommandAndPreservesCoordinates()
    {
        var f = new Fixture { Command = "--old" };
        f.Owner.PropertyChanged += (_, e) => { if (e.PropertyName == "PresetFileNodes") { f.Command = "--dns-addr 1.1.1.1\n--new --name=second"; f.Zapret = true; } };
        f.Owner.Rebuild();
        Assert.Equal("extract:--old", f.Trace[1]);
        var call = f.GroupInputs.Single();
        Assert.True(call.Zapret);
        Assert.Equal(["--dns-addr", "--new", "--name"], call.Options.Select(o => o.Name));
        Assert.Equal([0, 1, 1], call.Options.Select(o => o.SourceLine));
        Assert.Equal("second", call.Options[2].Value);
        Assert.Equal(6, call.Options[2].SourceColumn);
    }

    [Fact]
    public void FilePublicationSeesOldFlagsAndGroupPublicationSeesBothNewFlags()
    {
        var f = new Fixture { Detected = [File("a")], GroupNodes = [new("group")] };
        var observations = new List<string>();
        f.Owner.PropertyChanged += (sender, e) =>
        {
            Assert.Same(f.Owner, sender);
            observations.Add($"{e.PropertyName}:{f.Owner.HasPresetFiles}:{f.Owner.HasPresetGroups}:{f.Owner.IsPresetStructureVisible}");
        };
        f.Owner.AvailabilityChanged += (sender, args) => { Assert.Same(f.Owner, sender); Assert.Same(EventArgs.Empty, args); observations.Add("availability"); };
        f.Owner.Set([File("a")], []);
        Assert.Equal(["PresetFileNodes:False:False:False", "PresetGroupNodes:True:True:True", "HasPresetFiles:True:True:True", "HasPresetGroups:True:True:True", "IsPresetStructureVisible:True:True:True", "availability"], observations);
        Assert.Equal(["text", "extract:", "attached", "files", "event:PresetFileNodes", "text", "zapret", "groups", "event:PresetGroupNodes", "event:HasPresetFiles", "event:HasPresetGroups", "event:IsPresetStructureVisible", "availability"], f.Trace);
    }

    [Fact]
    public void RebuildAlwaysRaisesThreeDerivedNotificationsButOnlyRawAvailabilityDeltas()
    {
        var f = new Fixture(); f.Owner.Rebuild();
        Assert.DoesNotContain("availability", f.Trace);
        Assert.True(f.Owner.HasPresetGroups); Assert.True(f.Owner.IsPresetStructureVisible);
        f.Trace.Clear(); f.Owner.Set([], []);
        Assert.False(f.Owner.HasPresetGroups); Assert.False(f.Owner.IsPresetStructureVisible);
        Assert.DoesNotContain("availability", f.Trace); // Changing only explicit mode never raised this event.
        Assert.Equal(["event:HasPresetFiles", "event:HasPresetGroups", "event:IsPresetStructureVisible"], f.Trace.Where(x => x.StartsWith("event:")).TakeLast(3));
    }

    [Fact]
    public void ClearResetsExplicitModeAndArraysThenRebuildsWithLiveDetectedFiles()
    {
        var f = new Fixture { Detected = [File("detected")] };
        f.Owner.Set([File("explicit")], [new("explicit group", [])]);
        f.Owner.Clear();
        Assert.Equal("detected", Assert.Single(f.FileInputs.Last()).Path);
        Assert.Empty(f.GroupInputs.Last().Groups); Assert.True(f.Owner.HasPresetGroups);
    }

    [Fact]
    public void SuccessfulEmptyRebuildPublishesFreshProjectionCollections()
    {
        var f = new Fixture(); f.Owner.Rebuild();
        var files = f.Owner.PresetFileNodes; var groups = f.Owner.PresetGroupNodes;
        f.Owner.Rebuild();
        Assert.NotSame(files, f.Owner.PresetFileNodes); Assert.NotSame(groups, f.Owner.PresetGroupNodes);
        Assert.Empty(f.Owner.PresetFileNodes); Assert.Empty(f.Owner.PresetGroupNodes);
    }

    [Fact]
    public void SameProjectionReferencesSuppressOnlyNodePropertyNotifications()
    {
        var f = new Fixture(); IReadOnlyList<Node> files = [new("file")], groups = [new("group")];
        f.ProjectFiles = _ => files; f.ProjectGroups = (_, _, _) => groups;
        f.Owner.Rebuild(); f.Trace.Clear(); f.Owner.Rebuild();
        Assert.Equal(["event:HasPresetFiles", "event:HasPresetGroups", "event:IsPresetStructureVisible"], f.Trace.Where(x => x.StartsWith("event:")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NullArgumentsAreCheckedBeforeEitherInputIsEnumerated(bool first)
    {
        var f = new Fixture(); int enumerations = 0;
        IEnumerable<T> Input<T>() { enumerations++; yield break; }
        var error = Assert.Throws<ArgumentNullException>(() => f.Owner.Set(first ? null! : Input<ConfigMakerPresetFileInfo>(), first ? Input<ConfigMakerPresetGroupInfo>() : null!));
        Assert.Equal(first ? "files" : "groups", error.ParamName);
        Assert.Equal(0, enumerations); Assert.Empty(f.Trace);
    }

    [Fact]
    public void ThrowingFileEnumerationPreservesPreviousExplicitFilesAndMode()
    {
        var f = new Fixture(); f.Owner.Set([File("old")], [new("old", [])]);
        Assert.Throws<ProbeFailure>(() => f.Owner.Set(ThrowingFiles(), []));
        f.Owner.Rebuild(); Assert.Equal("old", Assert.Single(f.FileInputs.Last()).Path);
        Assert.Equal("old", Assert.Single(f.GroupInputs.Last().Groups).Name);
    }

    [Fact]
    public void ThrowingGroupEnumerationKeepsNewFilesAndPreviousGroupsWithoutRebuilding()
    {
        var f = new Fixture(); f.Owner.Set([File("old")], [new("old", [])]);
        int count = f.FileInputs.Count;
        Assert.Throws<ProbeFailure>(() => f.Owner.Set([File("new")], ThrowingGroups()));
        Assert.Equal(count, f.FileInputs.Count);
        f.Owner.Rebuild(); Assert.Equal("new", Assert.Single(f.FileInputs.Last()).Path);
        Assert.Equal("old", Assert.Single(f.GroupInputs.Last().Groups).Name);
    }

    [Fact]
    public void FirstSetGroupFailureDoesNotEnableExplicitMode()
    {
        var f = new Fixture { Detected = [File("detected")] };
        Assert.Throws<ProbeFailure>(() => f.Owner.Set([File("uncommitted explicit")], ThrowingGroups()));
        f.Owner.Rebuild();
        Assert.Equal("detected", Assert.Single(f.FileInputs.Last()).Path);
        Assert.Empty(f.GroupInputs.Last().Groups); Assert.True(f.Owner.HasPresetGroups);
    }

    [Fact]
    public void FirstCommandReadFailureDoesNotExtractOrPublishAnything()
    {
        var f = new Fixture { Failure = "text1" };
        var files = f.Owner.PresetFileNodes; var groups = f.Owner.PresetGroupNodes;
        Assert.Throws<ProbeFailure>(f.Owner.Rebuild);
        Assert.Equal(["text"], f.Trace);
        Assert.Same(files, f.Owner.PresetFileNodes); Assert.Same(groups, f.Owner.PresetGroupNodes);
    }

    [Theory]
    [InlineData("extract")]
    [InlineData("attached")]
    [InlineData("files")]
    public void FailureBeforeFileAssignmentKeepsPublishedNodesAndFlags(string phase)
    {
        var f = new Fixture { Detected = [File("old")] }; f.Owner.Rebuild();
        var files = f.Owner.PresetFileNodes; var groups = f.Owner.PresetGroupNodes;
        f.Failure = phase;
        Assert.Throws<ProbeFailure>(f.Owner.Rebuild);
        Assert.Same(files, f.Owner.PresetFileNodes); Assert.Same(groups, f.Owner.PresetGroupNodes);
        Assert.True(f.Owner.HasPresetFiles);
        f.Failure = null; f.Detected = []; f.Owner.Rebuild(); Assert.False(f.Owner.HasPresetFiles);
    }

    [Theory]
    [InlineData("text2")]
    [InlineData("zapret")]
    [InlineData("groups")]
    public void FailureAfterFileAssignmentKeepsNewFilesButPreviousFlagsAndGroups(string phase)
    {
        var f = new Fixture(); f.Owner.Rebuild();
        var files = f.Owner.PresetFileNodes; var groups = f.Owner.PresetGroupNodes;
        f.Detected = [File("new")]; f.Failure = phase; f.TextReads = 0;
        Assert.Throws<ProbeFailure>(f.Owner.Rebuild);
        Assert.NotSame(files, f.Owner.PresetFileNodes); Assert.Same(groups, f.Owner.PresetGroupNodes);
        Assert.False(f.Owner.HasPresetFiles);
        f.Failure = null; f.Owner.Rebuild(); Assert.True(f.Owner.HasPresetFiles);
    }

    [Theory]
    [InlineData("PresetFileNodes", false)]
    [InlineData("PresetGroupNodes", true)]
    [InlineData("HasPresetFiles", true)]
    [InlineData("HasPresetGroups", true)]
    [InlineData("IsPresetStructureVisible", true)]
    public void ThrowingObserverRetainsExactlyTheAlreadyPublishedPartialState(string property, bool newFlags)
    {
        var f = new Fixture { Detected = [File("new")], GroupNodes = [new("group")] };
        var initialGroups = f.Owner.PresetGroupNodes;
        f.Owner.PropertyChanged += (_, e) => { if (e.PropertyName == property) throw new ProbeFailure(); };
        Assert.Throws<ProbeFailure>(f.Owner.Rebuild);
        Assert.Single(f.Owner.PresetFileNodes); Assert.Equal(newFlags, f.Owner.HasPresetFiles);
        if (newFlags) Assert.Single(f.Owner.PresetGroupNodes); else Assert.Same(initialGroups, f.Owner.PresetGroupNodes);
        Assert.DoesNotContain("availability", f.Trace);
    }

    [Fact]
    public void AvailabilityObserverThrowsAfterEveryStateAndPropertyHasBeenPublished()
    {
        var f = new Fixture { Detected = [File("new")], GroupNodes = [new("group")] };
        f.Owner.AvailabilityChanged += (_, _) => throw new ProbeFailure();
        Assert.Throws<ProbeFailure>(f.Owner.Rebuild);
        Assert.True(f.Owner.HasPresetFiles); Assert.True(f.Owner.HasPresetGroups);
        Assert.Single(f.Owner.PresetGroupNodes); Assert.Equal("availability", f.Trace.Last());
    }

    [Fact]
    public void ExplicitEnrichmentIsRecomputedEachTimeWithoutMutatingStoredInput()
    {
        var f = new Fixture { Detected = [File("a") with { OptionName = "--one" }] };
        f.Owner.Set([File("a")], []); f.Detected = [File("a") with { OptionName = "--two" }]; f.Owner.Rebuild();
        Assert.Equal("--one", f.FileInputs[0][0].OptionName); Assert.Equal("--two", f.FileInputs[1][0].OptionName);
    }

    [Fact]
    public void FileObserverReentryPublishesNestedStateThenOuterReadsCurrentCommandAndGroups()
    {
        var f = new Fixture { Command = "--old", Detected = [File("outer")] };
        bool nested = false;
        f.Owner.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != "PresetFileNodes" || nested) return;
            nested = true; f.Command = "--new"; f.Zapret = true;
            f.Owner.Set([File("inner")], [new("inner group", [])]);
        };
        f.Owner.Rebuild();
        Assert.Equal(["outer", "inner"], f.FileInputs.Select(x => Assert.Single(x).Path));
        Assert.Equal("inner", Assert.Single(f.Owner.PresetFileNodes).Label);
        Assert.Equal(2, f.GroupInputs.Count);
        Assert.All(f.GroupInputs, x => { Assert.Equal("--new", Assert.Single(x.Options).Name); Assert.Equal("inner group", Assert.Single(x.Groups).Name); Assert.True(x.Zapret); });
        Assert.Equal(1, f.Trace.Count(x => x == "availability"));
    }

    [Fact]
    public void ReentryFromGroupPublicationCanReplaceFlagsBeforeOuterDerivedEvents()
    {
        var f = new Fixture { Detected = [File("outer")] }; bool nested = false;
        var flags = new List<bool>();
        f.Owner.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "PresetGroupNodes" && !nested)
            { nested = true; f.Detected = []; f.Owner.Set([], []); }
            if (e.PropertyName == "HasPresetFiles") flags.Add(f.Owner.HasPresetFiles);
        };
        f.Owner.Rebuild();
        Assert.Equal([false, false], flags); Assert.False(f.Owner.HasPresetFiles); Assert.False(f.Owner.HasPresetGroups);
        Assert.Equal(2, f.Trace.Count(x => x == "availability")); // Outer delta was captured before nested mutation.
    }

    [Fact]
    public void ClearFailureStillLeavesExplicitModeAndArraysCleared()
    {
        var f = new Fixture(); f.Owner.Set([File("explicit")], [new("group", [])]);
        f.Failure = "extract"; Assert.Throws<ProbeFailure>(f.Owner.Clear);
        f.Failure = null; f.Detected = [File("detected")]; f.Owner.Rebuild();
        Assert.Equal("detected", Assert.Single(f.FileInputs.Last()).Path); Assert.Empty(f.GroupInputs.Last().Groups);
    }

    [Fact]
    public void ReplacementArrayAssignmentKeepsIdentityWithoutPublishingOrChangingMode()
    {
        var f = new Fixture(); f.Owner.Set([File("old")], []); f.Trace.Clear();
        var nodes = f.Owner.PresetFileNodes;
        ConfigMakerPresetFileInfo[] replacement = [File("replacement")];
        f.Owner.ExplicitPresetFiles = replacement;
        Assert.Same(replacement, f.Owner.ExplicitPresetFiles); Assert.True(f.Owner.UsesExplicitPresetStructure);
        Assert.Same(nodes, f.Owner.PresetFileNodes); Assert.Empty(f.Trace);
        replacement[0] = File("mutated"); f.Owner.Rebuild();
        Assert.Equal("mutated", Assert.Single(f.FileInputs.Last()).Path);
    }

    [Fact]
    public void ReplacementArrayBeforeFirstSetDoesNotTurnOnExplicitMode()
    {
        var f = new Fixture { Detected = [File("detected")] };
        ConfigMakerPresetFileInfo[] replacement = [File("replacement")];
        f.Owner.ExplicitPresetFiles = replacement;
        Assert.False(f.Owner.UsesExplicitPresetStructure); Assert.Empty(f.Trace);
        f.Owner.Rebuild();
        Assert.Equal("detected", Assert.Single(f.FileInputs.Last()).Path);
        Assert.Same(replacement, f.Owner.ExplicitPresetFiles);
        f.Owner.Clear(); Assert.Empty(f.Owner.ExplicitPresetFiles); Assert.False(f.Owner.UsesExplicitPresetStructure);
    }

    private static ConfigMakerPresetFileInfo File(string path) => new(path, path, "folder");
    private static IEnumerable<ConfigMakerPresetFileInfo> ThrowingFiles() { yield return File("partial"); throw new ProbeFailure(); }
    private static IEnumerable<ConfigMakerPresetGroupInfo> ThrowingGroups() { yield return new("partial", []); throw new ProbeFailure(); }
    private sealed class ProbeFailure : Exception;
    private sealed record Node(string Label);
    private sealed record GroupCall(IReadOnlyList<ConfigCommandOption> Options, IReadOnlyList<ConfigMakerPresetGroupInfo> Groups, bool Zapret);

    private sealed class Fixture
    {
        public List<string> Trace { get; } = [];
        public string Command { get; set; } = "";
        public bool Zapret { get; set; }
        public IEnumerable<ConfigMakerPresetFileInfo> Detected { get; set; } = [];
        public IEnumerable<ConfigMakerPresetFileInfo> Attached { get; set; } = [];
        public IReadOnlyList<Node> GroupNodes { get; set; } = [];
        public List<IReadOnlyList<ConfigMakerPresetFileInfo>> FileInputs { get; } = [];
        public List<GroupCall> GroupInputs { get; } = [];
        public Func<IReadOnlyList<ConfigMakerPresetFileInfo>, IReadOnlyList<Node>>? ProjectFiles;
        public Func<IReadOnlyList<ConfigCommandOption>, IReadOnlyList<ConfigMakerPresetGroupInfo>, bool, IReadOnlyList<Node>>? ProjectGroups;
        public string? Failure;
        public int TextReads;
        public ConfigMakerPresetStructure<Node> Owner { get; }
        public Fixture()
        {
            Owner = new(
                () => { Trace.Add("text"); TextReads++; if (Failure == "text1" || (Failure == "text2" && TextReads == 2)) throw new ProbeFailure(); return Command; },
                () => { Trace.Add("zapret"); Fail("zapret"); return Zapret; },
                command => { Trace.Add("extract:" + command); Fail("extract"); return Detected; },
                () => { Trace.Add("attached"); Fail("attached"); return Attached; },
                files => { Trace.Add("files"); Fail("files"); FileInputs.Add(files); return ProjectFiles?.Invoke(files) ?? files.Select(f => new Node(f.Path)).ToList(); },
                (options, groups, zapret) => { Trace.Add("groups"); Fail("groups"); GroupInputs.Add(new(options, groups, zapret)); return ProjectGroups?.Invoke(options, groups, zapret) ?? GroupNodes.ToList(); });
            Owner.PropertyChanged += (_, e) => Trace.Add("event:" + e.PropertyName);
            Owner.AvailabilityChanged += (_, _) => Trace.Add("availability");
        }
        private void Fail(string phase) { if (Failure == phase) throw new ProbeFailure(); }
    }
}
