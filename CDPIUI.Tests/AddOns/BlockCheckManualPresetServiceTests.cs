using CDPIUI.AddOns.BlockCheck2.Catalog;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Synthesis;

namespace CDPIUI.Tests.AddOns;

public class BlockCheckManualPresetServiceTests
{
    private static readonly StrategyCatalog Catalog = StrategyCatalogLoader.LoadBuiltIn();
    private static readonly BlockCheckManualPresetService Service = new();

    private static readonly BlockCheckTarget Target = new()
    {
        Id = "t1",
        Host = "Example.com",
        Protocol = BlockCheckProtocol.Tls12,
        IpVersion = BlockCheckIpVersion.IPv4,
    };

    private static StrategyDefinition[] ApplicableStrategies(bool circular) => Catalog.Strategies
        .Where(strategy => strategy.AppliesTo(Target) && strategy.SupportsCircular == circular)
        .ToArray();

    private static BlockCheckManualAssignment Site(string strategyId, string targetId = "t1") =>
        new(strategyId, targetId, BlockCheckManualScopeKind.Site);

    private static string[] Codes(BlockCheckManualPresetResult result) =>
        result.Issues.Select(issue => issue.Code).ToArray();

    [Fact]
    public void SiteAssignmentBuildsOneProfileForTheSite()
    {
        StrategyDefinition strategy = ApplicableStrategies(circular: true)[0];

        var result = Service.Build(Catalog, [Target], [Site(strategy.Id)]);

        Zapret2ProfilePlan profile = Assert.Single(result.Profiles);
        Assert.Equal(strategy.Id, profile.Primary.Id);
        Assert.Empty(profile.Fallbacks);
        Assert.Equal(["example.com"], profile.Filter.Domains);
        Assert.Equal(["t1"], profile.TargetIds);
        Assert.True(result.Configuration.Success);
        Assert.DoesNotContain(result.Issues, issue => issue.Severity == BlockCheckIssueSeverity.Error);
    }

    [Fact]
    public void SecondStrategyInTheSameScopeBecomesAFallback()
    {
        StrategyDefinition[] strategies = ApplicableStrategies(circular: true);

        var result = Service.Build(Catalog, [Target], [Site(strategies[0].Id), Site(strategies[1].Id)]);

        Zapret2ProfilePlan profile = Assert.Single(result.Profiles);
        Assert.Equal(strategies[0].Id, profile.Primary.Id);
        Assert.Equal([strategies[1].Id], profile.Fallbacks.Select(fallback => fallback.Id));
    }

    [Fact]
    public void DuplicateStrategyIsIgnoredWithAWarning()
    {
        StrategyDefinition strategy = ApplicableStrategies(circular: true)[0];

        var result = Service.Build(Catalog, [Target], [Site(strategy.Id), Site(strategy.Id)]);

        Assert.Empty(Assert.Single(result.Profiles).Fallbacks);
        Assert.Contains("MANUAL_DUPLICATE_STRATEGY", Codes(result));
    }

    [Fact]
    public void UnknownStrategyAndTargetAreRejected()
    {
        StrategyDefinition strategy = ApplicableStrategies(circular: true)[0];

        var result = Service.Build(Catalog, [Target], [Site("missing-strategy"), Site(strategy.Id, "missing-target")]);

        Assert.Empty(result.Profiles);
        Assert.Equal(["MANUAL_STRATEGY_UNKNOWN", "MANUAL_TARGET_UNKNOWN"], Codes(result));
    }

    [Fact]
    public void InapplicableStrategyIsRejected()
    {
        StrategyDefinition strategy = Catalog.Strategies.First(item => !item.AppliesTo(Target));

        var result = Service.Build(Catalog, [Target], [Site(strategy.Id)]);

        Assert.Empty(result.Profiles);
        Assert.Equal(["MANUAL_STRATEGY_INAPPLICABLE"], Codes(result));
    }

    [Fact]
    public void NonCircularStrategyCannotBeCombined()
    {
        StrategyDefinition nonCircular = new()
        {
            Id = "test-non-circular",
            Protocols = [BlockCheckProtocol.Tls12],
            IpVersions = [BlockCheckIpVersion.IPv4],
            SupportsCircular = false,
            Actions = [new LuaActionDefinition { Function = "fake" }],
        };
        StrategyCatalog catalog = new()
        {
            SchemaVersion = Catalog.SchemaVersion,
            CatalogVersion = Catalog.CatalogVersion,
            Strategies = [.. Catalog.Strategies, nonCircular],
        };
        StrategyDefinition circular = ApplicableStrategies(circular: true)[0];

        var result = Service.Build(catalog, [Target], [Site(circular.Id), Site(nonCircular.Id)]);

        Assert.Empty(result.Profiles);
        Assert.Contains("MANUAL_CIRCULAR_UNSUPPORTED", Codes(result));
        Assert.DoesNotContain("MANUAL_ASSIGNMENTS_EMPTY", Codes(result));
    }

    [Fact]
    public void NoAssignmentsGiveTheEmptyError()
    {
        var result = Service.Build(Catalog, [Target], []);

        Assert.Empty(result.Profiles);
        Assert.Equal(["MANUAL_ASSIGNMENTS_EMPTY"], Codes(result));
    }
}
