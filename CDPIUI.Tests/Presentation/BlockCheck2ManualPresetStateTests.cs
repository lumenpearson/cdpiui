using System.Globalization;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.ViewModels;

namespace CDPIUI.Tests.Presentation;

public sealed class BlockCheck2ManualPresetStateTests
{
    [Fact]
    public void ConstructionAndClearResultDoNotTouchTheHostDraftOrEvidence()
    {
        var f = new Fixture();
        Assert.Equal("", f.State.Arguments);
        Assert.Empty(f.Trace);
        f.Ready(); f.State.Rebuild(false);
        f.Trace.Clear();
        f.State.ClearResult();
        Assert.Equal("", f.State.Arguments);
        Assert.Equal("--automatic", f.Draft.EffectiveArguments);
        Assert.Single(f.Draft.Assignments);
        Assert.Empty(f.Trace);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void EmptyBranchKeepsItsDistinctIndexesBeforeDraftOrder(bool noSession, bool update)
    {
        var f = new Fixture();
        f.Ready(); f.State.Rebuild(false);
        if (noSession) f.Host.Value = null; else f.Draft.Assignments.Clear();
        f.Trace.Clear();
        f.State.Rebuild(update);
        Assert.Equal(update ? new[] { "clear", "indexes", "draft:", "selections", "notify" } : new[] { "clear", "indexes", "selections", "notify" }, f.Trace);
        Assert.Equal("", f.State.Arguments);
        Assert.Equal(update ? "" : "--automatic", f.Draft.EffectiveArguments);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PopulatedBranchAssignsResultBeforeIssuesAndAppliesDraftBeforeIndexes(bool update)
    {
        var f = new Fixture(); f.Ready();
        f.Operations.Result = Result("  --manual  ", [Issue("first", "s")]);
        f.Host.AfterGroup = _ => Assert.Equal("  --manual  ", f.State.Arguments);
        f.State.Rebuild(update);
        Assert.Equal(update ? new[] { "clear", "build", "group:first:1", "draft:--manual", "indexes", "selections", "notify" } : new[] { "clear", "build", "group:first:1", "indexes", "selections", "notify" }, f.Trace);
        Assert.Equal("  --manual  ", f.State.Arguments);
        Assert.Equal(update ? "--manual" : "--automatic", f.Draft.StructuredArguments);
        Assert.Single(f.Draft.Assignments);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OnlyUpdatingTheDraftDiscardsItsExistingExpertEdit(bool update)
    {
        var f = new Fixture(); f.Ready();
        f.Draft.BeginExpertEditing(); f.Draft.SetExpertArguments("--expert"); f.Trace.Clear();
        f.State.Rebuild(update);
        Assert.Equal(!update, f.Draft.IsExpertEditingEnabled);
        Assert.Equal(update ? "--manual" : "--expert", f.Draft.EffectiveArguments);
        Assert.Equal(update, f.Draft.IsModifiedAutomatic);
    }

    [Fact]
    public void ClearingIssuesCanReplaceTheSessionBeforeItsFirstObservation()
    {
        var f = new Fixture(); f.Draft.Assignments.Add(Row());
        var current = Session();
        f.Host.Clear = () => f.Host.Value = current;
        f.State.Rebuild(false);
        Assert.Same(current.Catalog, Assert.Single(f.Operations.Calls).Catalog);
    }

    [Fact]
    public void ManualProbesReplaceOnlyMatchingStrategyCaseInsensitiveAndTargetOrdinalPairs()
    {
        var f = new Fixture();
        var replaced = Probe("STRATEGY", "t");
        var otherTargetCase = Probe("strategy", "T");
        var otherStrategy = Probe("other", "t");
        var manual = Probe("strategy", "t");
        f.Ready(Session(probes: [replaced, otherTargetCase, otherStrategy]));
        f.ManualTests.Add(manual);
        f.State.Rebuild(false);
        Assert.Equal(new[] { otherTargetCase, otherStrategy, manual }, Assert.Single(f.Operations.Calls).Evidence);
        Assert.Same(manual, Assert.Single(f.ManualTests));
    }

    [Fact]
    public void BuildArgumentsRetainDeferredLiveCollectionsAndTheOriginalMixedSessionTiming()
    {
        var f = new Fixture();
        var one = Target("one"); var two = Target("two");
        var original = Probe("s", "one"); var later = Probe("S", "one");
        var old = Session([one, two], [original], ["one"], 2, 0.9);
        f.Ready(old);
        var replacementRow = Row("next");
        f.Operations.BeforeReturn = call =>
        {
            f.Host.Value = Session(ignored: ["two"], minimumAttempts: 9, minimumRate: 0.1);
            f.ManualTests.Add(later);
            f.Draft.Assignments.Clear(); f.Draft.Assignments.Add(replacementRow);
            Assert.Same(old.Catalog, call.Catalog);
            Assert.Equal(new[] { one }, call.Targets);
            Assert.Same(replacementRow.Assignment, Assert.Single(call.Assignments));
            Assert.Same(later, Assert.Single(call.Evidence));
            Assert.Equal(2, call.Options.MinimumAttempts);
            Assert.Equal(0.9, call.Options.MinimumSuccessRate);
        };
        f.State.Rebuild(false);
        Assert.Equal(new[] { "clear", "build", "indexes", "selections", "notify" }, f.Trace);
    }

    [Theory]
    [InlineData(-5, 1, -0.5)]
    [InlineData(0, 1, 0.8)]
    [InlineData(7, 7, 1.5)]
    public void OptionsClampOnlyAttemptsAndAllocateFreshDefaults(int source, int expected, double rate)
    {
        var session = Session(minimumAttempts: source, minimumRate: rate);
        var first = BlockCheck2ManualPresetState.OptionsFor(session);
        var second = BlockCheck2ManualPresetState.OptionsFor(session);
        Assert.NotSame(first, second);
        Assert.NotSame(first.WriterOptions, second.WriterOptions);
        Assert.Equal(expected, first.MinimumAttempts);
        Assert.Equal(rate, first.MinimumSuccessRate);
    }

    [Fact]
    public void IssueGroupingUsesExactSeverityCodeAndMessageWithFirstOccurrenceOrder()
    {
        var f = new Fixture(); f.Ready();
        f.Operations.Result = Result("--manual", [
            Issue("A", "s", "same"), Issue("a", "unknown", "same"), Issue("A", "s", "same"),
            Issue("A", "s", "Same"), new(BlockCheckIssueSeverity.Warning, "A", "same", "s"),
        ]);
        f.State.Rebuild(false);
        Assert.Equal(new[] { "A", "a", "A", "A" }, f.Host.Groups.Select(group => group.Code));
        Assert.Equal(new[] { 2, 1, 1, 1 }, f.Host.Groups.Select(group => group.Count));
        Assert.Equal(new[] { "same", "same", "Same", "same" }, f.Host.Groups.Select(group => group.Message));
        Assert.Equal(new[] { "Strategy s (s)", "Strategy s (s)" }, f.Host.Groups[0].Subjects);
        Assert.Equal(BlockCheckIssueSeverity.Warning, f.Host.Groups[3].Severity);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData(" ", "")]
    [InlineData("S", "Strategy s (S)")]
    [InlineData("unknown", "unknown")]
    [InlineData("/unknown/", "/unknown/")]
    [InlineData("s/unknown/second", "Strategy s (s) / unknown / Strategy second (second)")]
    public void SubjectsRetainNullCaseSlashAndFallbackRules(string? subject, string expected)
    {
        var f = new Fixture(); f.Ready(); f.Draft.Assignments.Add(Row("second"));
        f.Operations.Result = Result("--manual", [Issue("issue", subject)]);
        f.State.Rebuild(false);
        Assert.Equal(expected, Assert.Single(Assert.Single(f.Host.Groups).Subjects));
    }

    [Fact]
    public void ScopeMatchWinsBeforeAnAssignmentWhoseStrategyIdMatchesTheSameText()
    {
        const string scope = "IPV4|TCP|443|TLS|SITE:EXAMPLE.TEST";
        var f = new Fixture(); f.Ready();
        var first = f.Draft.Assignments.Single();
        f.Draft.Assignments.Add(Row(scope, "different.test"));
        f.Operations.Result = Result("--manual", [Issue("scope", scope)]);
        f.State.Rebuild(false);
        Assert.Equal(first.Scope + " · " + first.ConnectionDetails, Assert.Single(Assert.Single(f.Host.Groups).Subjects));
    }

    [Theory]
    [InlineData("en-US", "ı (I)")]
    [InlineData("tr-TR", "I")]
    public void StrategyDescriptionsKeepTheCurrentCultureComparison(string culture, string expected)
    {
        var old = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var f = new Fixture(); f.Ready(); f.Draft.Assignments.Clear();
            f.Draft.Assignments.Add(Row("I", display: "ı"));
            f.Operations.Result = Result("--manual", [Issue("culture", "I")]);
            f.State.Rebuild(false);
            Assert.Equal(expected, Assert.Single(Assert.Single(f.Host.Groups).Subjects));
        }
        finally { CultureInfo.CurrentCulture = old; }
    }

    [Fact]
    public void DeferredSubjectDescriptionsObserveAssignmentsChangedByTheHost()
    {
        var f = new Fixture(); f.Ready();
        f.Operations.Result = Result("--manual", [Issue("late", "s")]);
        f.Host.BeforeSubjects = () => { f.Draft.Assignments.Clear(); f.Draft.Assignments.Add(Row(display: "Late")); };
        f.State.Rebuild(false);
        Assert.Equal("Late (s)", Assert.Single(Assert.Single(f.Host.Groups).Subjects));
    }

    [Fact]
    public void AReentrantIssueCallbackMakesTheOuterApplyUseTheCurrentResult()
    {
        var f = new Fixture(); f.Ready();
        var first = Result("--first", [Issue("first", "s"), Issue("tail", "s")]);
        var second = Result("--second", [Issue("nested", "s")]);
        f.Operations.Result = first;
        bool entered = false;
        f.Host.AfterGroup = _ =>
        {
            if (entered) return;
            entered = true; f.Operations.Result = second; f.State.Rebuild(false);
        };
        f.State.Rebuild(true);
        Assert.Equal("--second", f.State.Arguments);
        Assert.Equal("--second", f.Draft.EffectiveArguments);
        Assert.Equal(new[] { "nested", "tail" }, f.Host.Groups.Select(group => group.Code));
        Assert.Equal(2, f.Operations.Calls.Count);
    }

    [Theory]
    [InlineData("clear")]
    [InlineData("build")]
    [InlineData("group")]
    [InlineData("indexes")]
    [InlineData("selections")]
    [InlineData("notify")]
    public void FailuresEscapeSynchronouslyWithOriginalPartialMutations(string point)
    {
        var f = new Fixture(); f.Ready(); f.State.Rebuild(false);
        f.Operations.Result = Result("--new", [Issue("new", "s")]); f.Trace.Clear();
        var failure = new IOException(point);
        if (point == "clear") f.Host.Clear = () => throw failure;
        if (point == "build") f.Operations.BeforeReturn = _ => throw failure;
        if (point == "group") f.Host.AfterGroup = _ => throw failure;
        if (point == "indexes") f.Host.Indexes = () => throw failure;
        if (point == "selections") f.Host.Selections = () => throw failure;
        if (point == "notify") f.Host.Notify = () => throw failure;
        Assert.Same(failure, Assert.Throws<IOException>(() => f.State.Rebuild(true)));
        Assert.Equal(point is "clear" or "build" ? "--manual" : "--new", f.State.Arguments);
        Assert.Equal(point is "indexes" or "selections" or "notify" ? "--new" : "--automatic", f.Draft.EffectiveArguments);
        if (point is "clear" or "build" or "group") Assert.DoesNotContain("indexes", f.Trace);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DraftFailureKeepsTheDifferentEmptyAndPopulatedIndexesOrder(bool populated)
    {
        var f = new Fixture(); if (populated) f.Ready();
        var failure = new IOException("draft collection observer");
        f.Draft.Groups.CollectionChanged += (_, _) => throw failure;
        Assert.Same(failure, Assert.Throws<IOException>(() => f.State.Rebuild(true)));
        Assert.Equal(!populated, f.Trace.Contains("indexes"));
        Assert.DoesNotContain("selections", f.Trace);
        Assert.Equal(populated ? "--manual" : "", f.Draft.StructuredArguments);
        Assert.Equal(populated ? "--manual" : "", f.State.Arguments);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NonemptyProfilesReachTheSameDraftGroupsOnlyWhenUpdating(bool update)
    {
        var f = new Fixture(); f.Ready();
        f.Draft.ApplyStructuredChange("--before", [new() { Name = "before", Primary = new() { Id = "old" } }]);
        var oldGroup = Assert.Single(f.Draft.Groups);
        var next = new Zapret2ProfilePlan { Name = "next", Primary = new() { Id = "primary" }, Filter = new() { Domains = ["example.test"] } };
        next.Fallbacks.Add(new() { Id = "fallback" });
        f.Operations.Result = Result("--manual", profiles: [next]);
        f.State.Rebuild(update);
        var group = Assert.Single(f.Draft.Groups);
        if (update)
        {
            Assert.NotSame(oldGroup, group);
            Assert.Equal("next", group.Name);
            Assert.Equal("primary -> fallback", group.Strategies);
            Assert.Equal("example.test", group.Scope);
        }
        else Assert.Same(oldGroup, group);
        Assert.Equal(update ? "--manual" : "--before", f.Draft.EffectiveArguments);
    }

    [Fact]
    public void InvalidSiteListScopeRetainsItsPathFailureBeforeStrategyLookupButBlankSubjectsStayLazy()
    {
        var f = new Fixture(); f.Ready(); f.Draft.Assignments.Clear();
        f.Draft.Assignments.Add(new(new("s", "t", BlockCheckManualScopeKind.SiteList, ""), new() { Id = "s" }, Target("t"), "fixture"));
        f.Operations.Result = Result("--manual", [Issue("blank", null)]);
        f.State.Rebuild(false);
        Assert.Equal("", Assert.Single(Assert.Single(f.Host.Groups).Subjects));
        f.Operations.Result = Result("--manual", [Issue("lookup", "s")]); f.Trace.Clear();
        Assert.ThrowsAny<ArgumentException>(() => f.State.Rebuild(false));
        Assert.Equal("--manual", f.State.Arguments);
        Assert.Empty(f.Host.Groups);
        Assert.DoesNotContain("indexes", f.Trace);
    }

    private static ProbeResult Probe(string strategy, string target) => new() { StrategyId = strategy, TargetId = target };
    private static BlockCheckTarget Target(string id, string host = "example.test") => new() { Id = id, Host = host, Protocol = BlockCheckProtocol.Tls12, IpVersion = BlockCheckIpVersion.IPv4 };
    private static BlockCheck2ManualAssignmentItem Row(string id = "s", string host = "example.test", string? display = null) =>
        new(new(id, "t", BlockCheckManualScopeKind.Site), new() { Id = id, DisplayName = display ?? "Strategy " + id }, Target("t", host), "fixture");
    private static BlockCheckIssue Issue(string code, string? subject, string message = "fixture") => new(BlockCheckIssueSeverity.Error, code, message, subject);
    private static BlockCheckManualPresetResult Result(string command, BlockCheckIssue[]? issues = null, Zapret2ProfilePlan[]? profiles = null) => new() { Configuration = new() { CommandLine = command }, Issues = issues ?? [], Profiles = profiles ?? [] };
    private static BlockCheckResultSession Session(BlockCheckTarget[]? targets = null, ProbeResult[]? probes = null, string[]? ignored = null, int minimumAttempts = 3, double minimumRate = 0.8) => new()
    {
        Targets = targets ?? [Target("t")],
        RunResult = new() { Scan = new() { ProbeResults = probes ?? [], IgnoredTargetIds = new HashSet<string>(ignored ?? [], StringComparer.Ordinal) } },
        RunOptions = new() { Synthesis = new() { MinimumAttempts = minimumAttempts, MinimumSuccessRate = minimumRate } },
    };

    private sealed class Fixture
    {
        public List<string> Trace { get; } = [];
        public BlockCheck2PresetDraft Draft { get; } = new();
        public List<ProbeResult> ManualTests { get; } = [];
        public Host Host { get; }
        public Operations Operations { get; }
        public BlockCheck2ManualPresetState State { get; }
        public Fixture()
        {
            Draft.LoadAutomatic("--automatic", []);
            Host = new(Trace); Operations = new(Trace);
            State = new(Draft, ManualTests, Host, Operations);
            Draft.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(Draft.EffectiveArguments)) Trace.Add("draft:" + Draft.EffectiveArguments); };
        }
        public void Ready(BlockCheckResultSession? session = null) { Host.Value = session ?? Session(); Draft.Assignments.Add(Row()); }
    }
    private sealed record BuildCall(StrategyCatalog Catalog, IEnumerable<BlockCheckTarget> Targets, IEnumerable<BlockCheckManualAssignment> Assignments, IEnumerable<ProbeResult> Evidence, BlockCheckManualPresetOptions Options);
    private sealed class Operations(List<string> trace) : IBlockCheck2ManualPresetOperations
    {
        public BlockCheckManualPresetResult Result = BlockCheck2ManualPresetStateTests.Result("--manual");
        public List<BuildCall> Calls { get; } = [];
        public Action<BuildCall>? BeforeReturn;
        public BlockCheckManualPresetResult Build(StrategyCatalog catalog, IEnumerable<BlockCheckTarget> targets, IEnumerable<BlockCheckManualAssignment> assignments, IEnumerable<ProbeResult> evidence, BlockCheckManualPresetOptions options)
        {
            trace.Add("build"); var call = new BuildCall(catalog, targets, assignments, evidence, options); Calls.Add(call); BeforeReturn?.Invoke(call); return Result;
        }
    }
    private sealed record Group(BlockCheckIssueSeverity Severity, string Code, string Message, int Count, string[] Subjects);
    private sealed class Host(List<string> trace) : IBlockCheck2ManualPresetHost
    {
        public BlockCheckResultSession? Value;
        public BlockCheckResultSession? Session => Value;
        public List<Group> Groups { get; } = [];
        public Action? Clear, BeforeSubjects, Indexes, Selections, Notify;
        public Action<Group>? AfterGroup;
        public void ClearIssues() { trace.Add("clear"); Groups.Clear(); Clear?.Invoke(); }
        public void AddIssueGroup(BlockCheckIssueSeverity severity, string code, string message, int count, IEnumerable<string> subjects)
        {
            trace.Add($"group:{code}:{count}"); BeforeSubjects?.Invoke(); var group = new Group(severity, code, message, count, subjects.ToArray()); Groups.Add(group); AfterGroup?.Invoke(group);
        }
        public void RefreshAssignmentIndexes() { trace.Add("indexes"); Indexes?.Invoke(); }
        public void RefreshEvidenceSelections() { trace.Add("selections"); Selections?.Invoke(); }
        public void NotifyManualState() { trace.Add("notify"); Notify?.Invoke(); }
    }
}
