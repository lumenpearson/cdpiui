using System.Collections.Specialized;
using System.Globalization;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.AddOns.BlockCheck2.Reporting;
using CDPIUI.ViewModels;

namespace CDPIUI.Tests.Presentation;

public class BlockCheck2ManualAssignmentEditorTests
{
    [Fact]
    public void EditorUsesTheExistingDraftCollectionWithoutChangingItAtConstruction()
    {
        var f = new Fixture();
        Assert.Same(f.Draft.Assignments, f.Editor.Items);
        Assert.Empty(f.Trace);
        Assert.Empty(f.Editor.Items);
    }

    [Fact]
    public void MissingSessionRefusesAddsAndSiteListQueriesWithoutMutation()
    {
        var f = new Fixture { Session = null };
        var evidence = Evidence(Strategy("s"), Target("t"));
        Assert.False(f.Editor.AddForSite(evidence));
        Assert.False(f.Editor.AddForSiteList(evidence, "sites.txt"));
        Assert.False(f.Editor.CanAddForSiteList(evidence));
        Assert.Empty(f.Editor.GetSiteListPaths(evidence));
        Assert.Empty(f.Trace);
    }

    [Fact]
    public void AddUsesSessionDefinitionsAndAcknowledgesBeforeFormattingAndCollectionPublication()
    {
        var f = new Fixture();
        var strategy = Strategy("S");
        var target = Target("target");
        f.Session = Session([strategy], [target]);
        f.Editor.Items.CollectionChanged += (_, args) =>
        {
            var item = Assert.IsType<BlockCheck2ManualAssignmentItem>(Assert.Single(args.NewItems!));
            Assert.Same(strategy, item.Strategy);
            Assert.Same(target, item.Target);
            Assert.Equal("formatted:S", item.StrategyArguments);
            f.Trace.Add("collection:" + args.Action);
        };

        Assert.True(f.Editor.AddForSite(Evidence(Strategy("s"), Target("target"))));

        Assert.Equal(["ack", "format:S", "collection:Add", "rebuild:True"], f.Trace);
        Assert.Equal(new BlockCheckManualAssignment("s", "target", BlockCheckManualScopeKind.Site),
            Assert.Single(f.Editor.Items).Assignment);
    }

    [Theory]
    [InlineData("S", "target", true)]
    [InlineData("s", "TARGET", false)]
    public void DuplicateStrategyMatchingIsInsensitiveButAnchorMatchingIsOrdinal(string strategyId, string targetId, bool duplicate)
    {
        var f = new Fixture();
        var strategy = Strategy("s");
        f.Session = Session([strategy], [Target("target"), Target("TARGET")]);
        Assert.True(f.Editor.AddForSite(Evidence(strategy, Target("target"))));
        f.Trace.Clear();

        Assert.Equal(!duplicate, f.Editor.AddForSite(Evidence(Strategy(strategyId), Target(targetId))));

        Assert.Equal(duplicate ? 1 : 2, f.Editor.Items.Count);
        if (duplicate) Assert.Empty(f.Trace);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MissingStrategyOrAnchorRefusesTheAssignmentWithoutCallbacks(bool missingStrategy)
    {
        var f = new Fixture { Session = Session([Strategy("s")], [Target("t")]) };
        Assert.False(f.Editor.AddForSite(Evidence(Strategy(missingStrategy ? "missing" : "s"), Target(missingStrategy ? "t" : "missing"))));
        Assert.Empty(f.Editor.Items);
        Assert.Empty(f.Trace);
    }

    [Fact]
    public void SiteListAddUsesTheFirstCompatibleAnchorAndPreservesTheRequestedPathCasing()
    {
        var first = Target("first", "EXAMPLE.TEST.", ["Lists/Alpha.txt"]);
        var second = Target("second", "example.test", ["lists/alpha.TXT"]);
        var strategy = Strategy("s");
        var f = new Fixture { Session = Session([strategy], [first, second]) };
        var evidence = Evidence(strategy, Target("source"));

        Assert.True(f.Editor.AddForSiteList(evidence, "LISTS/ALPHA.txt"));
        Assert.False(f.Editor.AddForSiteList(evidence, "lists/alpha.TXT"));

        var item = Assert.Single(f.Editor.Items);
        Assert.Same(first, item.Target);
        Assert.Equal("first", item.Assignment.AnchorTargetId);
        Assert.Equal("LISTS/ALPHA.txt", item.Assignment.SiteListPath);
        Assert.Equal(BlockCheckManualScopeKind.SiteList, item.Assignment.ScopeKind);
    }

    [Theory]
    [InlineData(BlockCheckProtocol.Tls12, BlockCheckIpVersion.IPv4, 443, "example.test", true)]
    [InlineData(BlockCheckProtocol.Tls13, BlockCheckIpVersion.IPv4, 443, "EXAMPLE.TEST.", true)]
    [InlineData(BlockCheckProtocol.Tls12, BlockCheckIpVersion.IPv6, 443, "example.test", false)]
    [InlineData(BlockCheckProtocol.Tls12, BlockCheckIpVersion.IPv4, 8443, "example.test", false)]
    [InlineData(BlockCheckProtocol.Http, BlockCheckIpVersion.IPv4, 443, "example.test", false)]
    [InlineData(BlockCheckProtocol.Quic, BlockCheckIpVersion.IPv4, 443, "example.test", false)]
    [InlineData(BlockCheckProtocol.Tls12, BlockCheckIpVersion.IPv4, 443, "other.test", false)]
    public void SiteListCompatibilityRetainsHostAndConnectionRules(BlockCheckProtocol protocol, BlockCheckIpVersion ip, int port, string host, bool expected)
    {
        var target = new BlockCheckTarget { Id = "candidate", Host = host, Protocol = protocol, IpVersion = ip, CustomPort = port, HostListPaths = ["sites.txt"] };
        var f = new Fixture { Session = Session([Strategy("s")], [target]) };
        var evidence = Evidence(Strategy("s"), Target("source"));
        Assert.Equal(expected, f.Editor.CanAddForSiteList(evidence));
        Assert.Equal(expected ? new[] { "sites.txt" } : [], f.Editor.GetSiteListPaths(evidence));
    }

    [Fact]
    public void IgnoredAndNonSiteListTargetsDoNotOfferPathsOrAnchors()
    {
        var ignored = Target("ignored", paths: ["ignored.txt"]);
        var plain = Target("plain");
        var f = new Fixture { Session = Session([Strategy("s")], [ignored, plain], ignored: ["ignored"]) };
        var evidence = Evidence(Strategy("s"), Target("source"));
        Assert.False(f.Editor.CanAddForSiteList(evidence));
        Assert.Empty(f.Editor.GetSiteListPaths(evidence));
        Assert.False(f.Editor.AddForSiteList(evidence, "ignored.txt"));
    }

    [Theory]
    [InlineData("en-US", "ä.txt", "z.txt")]
    [InlineData("sv-SE", "z.txt", "ä.txt")]
    public void SiteListPathsFilterBlankDeduplicateAndSortUsingTheCurrentCulture(string culture, string first, string second)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var f = new Fixture { Session = Session([Strategy("s")], [Target("t", paths: ["z.txt", "ä.txt", "Z.TXT", "", "  "])]) };
            Assert.Equal(new[] { first, second }, f.Editor.GetSiteListPaths(Evidence(Strategy("s"), Target("source"))));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void RemoveUnknownAndInvalidMovesDoNotClearAcknowledgementsOrRebuild()
    {
        var f = new Fixture();
        var unknown = Assignment(Strategy("s"), Target("t"));
        f.Editor.RemoveAssignment(unknown);
        Assert.False(f.Editor.MoveAssignment(unknown, 1));
        f.Editor.Items.Add(unknown);
        Assert.False(f.Editor.MoveAssignment(unknown, -1));
        Assert.False(f.Editor.MoveAssignment(unknown, 1));
        Assert.Empty(f.Trace);
    }

    [Fact]
    public void MoveAndRemovePublishCollectionChangesBeforeRebuilding()
    {
        var f = new Fixture();
        var first = Assignment(Strategy("first"), Target("t"));
        var second = Assignment(Strategy("second"), Target("t"));
        f.Editor.Items.Add(first);
        f.Editor.Items.Add(second);
        f.Editor.Items.CollectionChanged += (_, args) => f.Trace.Add("collection:" + args.Action);

        Assert.True(f.Editor.MoveAssignment(first, 1));
        f.Editor.RemoveAssignment(first);

        Assert.Same(second, Assert.Single(f.Editor.Items));
        Assert.Equal(["ack", "collection:Move", "rebuild:True", "ack", "collection:Remove", "rebuild:True"], f.Trace);
    }

    [Fact]
    public void AZeroMoveAndClearingAnEmptyListStillPublishAndRebuild()
    {
        var f = new Fixture();
        var item = Assignment(Strategy("s"), Target("t"));
        f.Editor.Items.Add(item);
        f.Editor.Items.CollectionChanged += (_, args) => f.Trace.Add("collection:" + args.Action);

        Assert.True(f.Editor.MoveAssignment(item, 0));
        f.Editor.ClearManualAssignments();
        f.Editor.ClearManualAssignments();

        Assert.Equal(["ack", "collection:Move", "rebuild:True", "ack", "collection:Reset", "rebuild:True", "ack", "collection:Reset", "rebuild:True"], f.Trace);
    }

    [Fact]
    public void FormatterFailureOccursAfterAcknowledgementButBeforeCollectionMutation()
    {
        var failure = new InvalidOperationException("fixture format failure");
        var f = new Fixture { Session = Session([Strategy("s")], [Target("t")]), FormatFailure = failure };
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => f.Editor.AddForSite(Evidence(Strategy("s"), Target("t")))));
        Assert.Empty(f.Editor.Items);
        Assert.Equal(["ack", "format:s"], f.Trace);
    }

    [Fact]
    public void CollectionSubscriberFailureRetainsTheAddedItemAndDoesNotRebuild()
    {
        var f = new Fixture { Session = Session([Strategy("s")], [Target("t")]) };
        var failure = new InvalidOperationException("fixture collection failure");
        f.Editor.Items.CollectionChanged += (_, _) => throw failure;

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => f.Editor.AddForSite(Evidence(Strategy("s"), Target("t")))));

        Assert.Single(f.Editor.Items);
        Assert.Equal(["ack", "format:s"], f.Trace);
    }

    [Fact]
    public void IndexRefreshUsesTheLiveCollectionAfterAnOrderSubscriberRemovesAnItem()
    {
        var f = new Fixture();
        var first = Assignment(Strategy("s1"), Target("t"));
        var second = Assignment(Strategy("s2"), Target("t"));
        f.Editor.Items.Add(first);
        f.Editor.Items.Add(second);
        first.PropertyChanged += (_, _) => f.Editor.Items.Remove(second);

        f.Editor.RefreshAssignmentIndexes();

        Assert.Same(first, Assert.Single(f.Editor.Items));
        Assert.Equal(1, first.Order);
        Assert.Equal(0, second.Order);
    }

    [Fact]
    public void SelectionRefreshReadsAssignmentsForEachRowButKeepsTheOriginalEvidenceEnumeration()
    {
        var f = new Fixture();
        var first = Evidence(Strategy("s1"), Target("t1", "one.test"));
        var second = Evidence(Strategy("s2"), Target("t2", "two.test"));
        var replacement = Evidence(Strategy("s1"), Target("other", "one.test"));
        f.Evidence = [first, second];
        f.Editor.Items.Add(Assignment(first.Evidence.Strategy, first.Evidence.Target));
        bool changed = false;
        first.PropertyChanged += (_, args) =>
        {
            if (changed || args.PropertyName != nameof(first.IsSelectedForPreset)) return;
            changed = true;
            f.Evidence = [replacement];
            f.Editor.Items.Add(Assignment(second.Evidence.Strategy, second.Evidence.Target));
        };

        f.Editor.RefreshEvidenceSelections();

        Assert.True(first.IsSelectedForPreset);
        Assert.True(second.IsSelectedForPreset);
        Assert.False(replacement.IsSelectedForPreset);
        Assert.False(first.IsAutomaticSelection);
    }

    [Fact]
    public void AutomaticSeedPreservesScopePrimaryFallbackOrderAndSkipsMissingOrDuplicateAssignments()
    {
        var f = new Fixture();
        var s1 = Strategy("s1");
        var s2 = Strategy("s2");
        var list = Target("list", "list.test", ["a.txt", "b.txt"]);
        var site = Target("site", "site.test");
        BlockCheckReportProfile listProfile = Profile(["list", "missing-target"], ["a.txt", "b.txt"], "s1", ["s2", "s1", "missing-strategy"]);
        BlockCheckReportProfile siteProfile = Profile(["site"], [], "s1", []);
        var supplied = Session([s1, s2], [list, site], [listProfile, siteProfile, siteProfile]);
        f.Session = supplied;
        f.Evidence = [Evidence(s1, list), Evidence(s2, list), Evidence(s1, site)];
        f.Draft.LoadAutomatic("--previous", []);
        f.OnRebuild = update =>
        {
            Assert.False(update);
            Assert.Equal("--previous", f.Draft.EffectiveArguments);
            Assert.Equal(5, f.Editor.Items.Count);
        };

        f.Editor.SeedAutomaticAssignments(supplied);

        Assert.Equal(new[] { "s1:a.txt", "s2:a.txt", "s1:b.txt", "s2:b.txt", "s1:" },
            f.Editor.Items.Select(item => item.Assignment.StrategyId + ":" + item.Assignment.SiteListPath));
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, f.Editor.Items.Select(item => item.Order));
        Assert.Equal("--automatic", f.Draft.EffectiveArguments);
        Assert.True(f.Draft.IsAutomatic);
        Assert.All(f.Evidence, evidence => { Assert.True(evidence.IsSelectedForPreset); Assert.True(evidence.IsAutomaticSelection); });
        Assert.Equal("ack", f.Trace[0]);
        Assert.Equal("rebuild:False", f.Trace[^1]);
    }

    [Fact]
    public void SeedUsesTheSuppliedSessionEvenIfClearingTheCollectionChangesTheCurrentSession()
    {
        var f = new Fixture();
        var strategy = Strategy("s");
        var target = Target("t");
        var supplied = Session([strategy], [target], [Profile(["t"], [], "s", [])]);
        var other = Session([], []);
        f.Session = supplied;
        f.Editor.Items.CollectionChanged += (_, args) => { if (args.Action == NotifyCollectionChangedAction.Reset) f.Session = other; };

        f.Editor.SeedAutomaticAssignments(supplied);

        Assert.Same(strategy, Assert.Single(f.Editor.Items).Strategy);
        Assert.Same(other, f.Session);
        Assert.Equal("--automatic", f.Draft.EffectiveArguments);
    }

    [Fact]
    public void DuplicateCatalogIdsThrowAfterAcknowledgementAndCollectionClear()
    {
        var f = new Fixture();
        f.Editor.Items.Add(Assignment(Strategy("old"), Target("old")));
        var supplied = Session([Strategy("s"), Strategy("S")], []);
        f.Editor.Items.CollectionChanged += (_, args) => f.Trace.Add("collection:" + args.Action);

        Assert.Throws<ArgumentException>(() => f.Editor.SeedAutomaticAssignments(supplied));

        Assert.Empty(f.Editor.Items);
        Assert.Equal(["ack", "collection:Reset"], f.Trace);
    }

    [Fact]
    public void ClearingAutomaticKeysIsQuietUntilSelectionsAreRefreshed()
    {
        var f = new Fixture();
        var strategy = Strategy("s");
        var target = Target("t");
        f.Session = Session([strategy], [target], [Profile(["t"], [], "s", [])]);
        var row = Evidence(strategy, target);
        f.Evidence = [row];
        f.Editor.SeedAutomaticAssignments(f.Session);
        f.Trace.Clear();

        f.Editor.ClearAutomaticKeys();
        Assert.True(row.IsAutomaticSelection);
        Assert.Empty(f.Trace);
        f.Editor.RefreshEvidenceSelections();

        Assert.True(row.IsSelectedForPreset);
        Assert.False(row.IsAutomaticSelection);
    }

    [Fact]
    public void EqualSiteListFilenamesUseTheFullPathAsTheSecondarySortKey()
    {
        var f = new Fixture { Session = Session([Strategy("s")], [Target("t", paths: ["z/alpha.txt", "a/alpha.txt"])]) };
        Assert.Equal(new[] { "a/alpha.txt", "z/alpha.txt" },
            f.Editor.GetSiteListPaths(Evidence(Strategy("s"), Target("source"))));
    }

    [Fact]
    public void RebuildFailureLeavesTheAlreadyPublishedAssignmentInTheDraft()
    {
        var failure = new InvalidOperationException("fixture rebuild failure");
        var f = new Fixture { Session = Session([Strategy("s")], [Target("t")]), OnRebuild = _ => throw failure };

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => f.Editor.AddForSite(Evidence(Strategy("s"), Target("t")))));

        Assert.Single(f.Editor.Items);
        Assert.Equal(["ack", "format:s", "rebuild:True"], f.Trace);
    }

    [Fact]
    public void SeedSubscriberFailureLeavesAPublishedItemWithoutItsAutomaticKeyOrDraftReload()
    {
        var f = new Fixture();
        var strategy = Strategy("s");
        var target = Target("t");
        f.Session = Session([strategy], [target], [Profile(["t"], [], "s", [])]);
        var row = Evidence(strategy, target);
        f.Evidence = [row];
        f.Draft.LoadAutomatic("--previous", []);
        var failure = new InvalidOperationException("fixture seed subscriber failure");
        f.Editor.Items.CollectionChanged += (_, args) => { if (args.Action == NotifyCollectionChangedAction.Add) throw failure; };

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => f.Editor.SeedAutomaticAssignments(f.Session)));
        f.Editor.RefreshEvidenceSelections();

        Assert.Single(f.Editor.Items);
        Assert.True(row.IsSelectedForPreset);
        Assert.False(row.IsAutomaticSelection);
        Assert.Equal("--previous", f.Draft.EffectiveArguments);
        Assert.Equal(["ack", "format:s"], f.Trace);
    }

    [Fact]
    public void LegacySeedRecordEqualityIsCaseSensitiveWhileAutomaticKeysRemainInsensitive()
    {
        var f = new Fixture();
        var strategy = Strategy("s");
        var target = Target("t");
        f.Session = Session([strategy], [target], [Profile(["t"], [], "s", ["S"])]);
        var row = Evidence(strategy, target);
        f.Evidence = [row];

        f.Editor.SeedAutomaticAssignments(f.Session);

        Assert.Equal(new[] { "s", "S" }, f.Editor.Items.Select(item => item.Assignment.StrategyId));
        f.Editor.Items.Clear();
        f.Editor.Items.Add(Assignment(Strategy("S"), Target("T")));
        f.Editor.RefreshEvidenceSelections();
        Assert.True(row.IsSelectedForPreset);
        Assert.True(row.IsAutomaticSelection);
    }

    [Fact]
    public void AutomaticScopesRetainTheFirstTargetFallbackAndNormalizedHostGrouping()
    {
        var first = Target("first", "example.test");
        var second = new BlockCheckTarget { Id = "second", Host = "EXAMPLE.TEST.", Protocol = BlockCheckProtocol.Tls13, IpVersion = BlockCheckIpVersion.IPv6 };
        var f = new Fixture();
        var supplied = Session([Strategy("s")], [first, second],
            [Profile(["first", "second"], ["missing-list.txt"], "s", []), Profile(["first", "second"], [], "s", [])]);
        f.Session = supplied;

        f.Editor.SeedAutomaticAssignments(supplied);

        Assert.Equal(2, f.Editor.Items.Count);
        Assert.All(f.Editor.Items, item => Assert.Same(first, item.Target));
        Assert.Equal(BlockCheckManualScopeKind.SiteList, f.Editor.Items[0].Assignment.ScopeKind);
        Assert.Equal("missing-list.txt", f.Editor.Items[0].Assignment.SiteListPath);
        Assert.Equal(BlockCheckManualScopeKind.Site, f.Editor.Items[1].Assignment.ScopeKind);
    }

    private static StrategyDefinition Strategy(string id) => new() { Id = id, DisplayName = "Strategy " + id };
    private static BlockCheckTarget Target(string id, string host = "example.test", string[]? paths = null) => new()
    {
        Id = id, Host = host, Protocol = BlockCheckProtocol.Tls12, IpVersion = BlockCheckIpVersion.IPv4, HostListPaths = paths ?? [],
    };
    private static BlockCheck2StrategyEvidenceItem Evidence(StrategyDefinition strategy, BlockCheckTarget target) => new(new() { Strategy = strategy, Target = target });
    private static BlockCheck2ManualAssignmentItem Assignment(StrategyDefinition strategy, BlockCheckTarget target) =>
        new(new(strategy.Id, target.Id, BlockCheckManualScopeKind.Site), strategy, target, "fixture");
    private static BlockCheckReportProfile Profile(string[] targets, string[] paths, string primary, string[] fallbacks) =>
        new("fixture", BlockCheckIpVersion.IPv4, BlockCheckTransport.Tcp, 443, "tls", [], paths, primary, fallbacks, targets);
    private static BlockCheckResultSession Session(StrategyDefinition[] strategies, BlockCheckTarget[] targets,
        BlockCheckReportProfile[]? profiles = null, string[]? ignored = null) => new()
        {
            Catalog = new() { Strategies = strategies.ToList() }, Targets = targets,
            Report = new() { PresetArguments = "--automatic", Profiles = profiles ?? [] },
            RunResult = new() { Scan = new() { IgnoredTargetIds = new HashSet<string>(ignored ?? [], StringComparer.Ordinal) } },
        };

    private sealed class Fixture
    {
        public BlockCheck2PresetDraft Draft { get; } = new();
        public BlockCheckResultSession? Session;
        public IReadOnlyList<BlockCheck2StrategyEvidenceItem> Evidence = [];
        public List<string> Trace { get; } = [];
        public Action<bool>? OnRebuild;
        public Exception? FormatFailure;
        public BlockCheck2ManualAssignmentEditor Editor { get; }

        public Fixture()
        {
            Editor = new(Draft, () => Session, () => Evidence,
                () => Trace.Add("ack"),
                update => { Trace.Add("rebuild:" + update); OnRebuild?.Invoke(update); },
                strategy =>
                {
                    Trace.Add("format:" + strategy.Id);
                    if (FormatFailure != null) throw FormatFailure;
                    return "formatted:" + strategy.Id;
                });
        }
    }
}
