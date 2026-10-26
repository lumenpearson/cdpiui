using CDPIUI.AddOns.BlockCheck2.Catalog;
using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.Tests.AddOns;

public class StrategyCatalogValidatorTests
{
    private static readonly StrategyCatalogValidator Validator = new();

    private static StrategyDefinition Strategy(
        string id = "fake-1",
        List<BlobDefinition>? blobs = null,
        LuaActionDefinition? action = null,
        List<BlockCheckProtocol>? protocols = null,
        List<BlockCheckIpVersion>? ipVersions = null,
        double cost = 1d,
        BlockCheckScanTier tier = BlockCheckScanTier.Quick,
        bool preHost = false,
        bool circular = true,
        bool noActions = false) => new()
        {
            Id = id,
            Protocols = protocols ?? [BlockCheckProtocol.Tls12],
            IpVersions = ipVersions ?? [BlockCheckIpVersion.IPv4],
            BaseCost = cost,
            ScanTier = tier,
            RequiresPreHost = preHost,
            SupportsCircular = circular,
            Blobs = blobs ?? [],
            Actions = noActions ? [] : [action ?? new LuaActionDefinition { Function = "fake" }],
        };

    private static StrategyCatalog Catalog(params StrategyDefinition[] strategies) => new()
    {
        SchemaVersion = 1,
        CatalogVersion = "test",
        Strategies = [.. strategies],
    };

    private static string[] Codes(StrategyDefinition strategy) =>
        Validator.Validate(Catalog(strategy)).Select(issue => issue.Code).ToArray();

    [Fact]
    public void BuiltInCatalogIsValid()
    {
        StrategyCatalog catalog = StrategyCatalogLoader.LoadBuiltIn();

        Assert.DoesNotContain(Validator.Validate(catalog), issue => issue.Severity == BlockCheckIssueSeverity.Error);
    }

    [Fact]
    public void ValidStrategyHasNoIssues()
    {
        Assert.Empty(Codes(Strategy()));
    }

    [Fact]
    public void CatalogHeaderIsChecked()
    {
        var issues = Validator.Validate(new StrategyCatalog { SchemaVersion = 2, CatalogVersion = " " });

        Assert.Equal(
            ["UNSUPPORTED_CATALOG_SCHEMA", "CATALOG_VERSION_MISSING", "CATALOG_EMPTY"],
            issues.Select(issue => issue.Code));
        Assert.Throws<ArgumentNullException>(() => Validator.Validate(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a b")]
    [InlineData("id\n")]
    [InlineData("../x")]
    public void StrategyIdMustBeSafe(string id)
    {
        Assert.Contains("STRATEGY_ID_INVALID", Codes(Strategy(id)));
    }

    [Fact]
    public void StrategyIdsAreUniqueIgnoringCase()
    {
        var issues = Validator.Validate(Catalog(Strategy("Fake-1"), Strategy("fake-1")));

        var issue = Assert.Single(issues);
        Assert.Equal("STRATEGY_ID_DUPLICATE", issue.Code);
        Assert.Equal("fake-1", issue.SubjectId);
    }

    [Fact]
    public void StrategyFieldsAreChecked()
    {
        Assert.Contains("STRATEGY_PROTOCOLS_EMPTY", Codes(Strategy(protocols: [])));
        Assert.Contains("STRATEGY_PROTOCOL_INVALID", Codes(Strategy(protocols: [(BlockCheckProtocol)99])));
        Assert.Contains("STRATEGY_IP_VERSIONS_EMPTY", Codes(Strategy(ipVersions: [])));
        Assert.Contains("STRATEGY_IP_VERSION_INVALID", Codes(Strategy(ipVersions: [(BlockCheckIpVersion)7])));
        Assert.Contains("STRATEGY_SCAN_TIER_INVALID", Codes(Strategy(tier: (BlockCheckScanTier)9)));
        Assert.Contains("STRATEGY_ACTIONS_EMPTY", Codes(Strategy(noActions: true)));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void BaseCostMustBePositiveAndFinite(double cost)
    {
        Assert.Contains("STRATEGY_COST_INVALID", Codes(Strategy(cost: cost)));
    }

    [Fact]
    public void PreHostCircularStrategyIsOnlyAWarning()
    {
        var issue = Assert.Single(Validator.Validate(Catalog(Strategy(preHost: true))));

        Assert.Equal(BlockCheckIssueSeverity.Warning, issue.Severity);
        Assert.Equal("PREHOST_CIRCULAR_DISABLED", issue.Code);
        Assert.Empty(Codes(Strategy(preHost: true, circular: false)));
    }

    [Theory]
    [InlineData("0x1603")]
    [InlineData("0XABCdef")]
    [InlineData("files/tls.bin")]
    [InlineData("@files/tls.bin")]
    [InlineData("files\\tls.bin")]
    [InlineData("a..b.bin")]
    public void SafeBlobSourcesAreAccepted(string source)
    {
        Assert.Empty(Codes(Strategy(blobs: [new BlobDefinition { Name = "tls", Source = source }])));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0x")]
    [InlineData("0x12g4")]
    [InlineData("files/tls bin")]
    [InlineData("@")]
    [InlineData("../tls.bin")]
    [InlineData("@files/../../tls.bin")]
    [InlineData("files\\..\\tls.bin")]
    [InlineData("C:tls.bin")]
    [InlineData("C:/Windows/tls.bin")]
    [InlineData("/etc/tls.bin")]
    public void UnsafeBlobSourcesAreRejected(string source)
    {
        Assert.Contains(
            "BLOB_SOURCE_UNSAFE",
            Codes(Strategy(blobs: [new BlobDefinition { Name = "tls", Source = source }])));
    }

    [Fact]
    public void BlobNamesAreCheckedAndCaseSensitive()
    {
        Assert.Contains("BLOB_NAME_INVALID", Codes(Strategy(blobs: [new BlobDefinition { Name = "a b", Source = "0x00" }])));
        Assert.Contains("BLOB_NAME_DUPLICATE", Codes(Strategy(blobs:
        [
            new BlobDefinition { Name = "tls", Source = "0x00" },
            new BlobDefinition { Name = "tls", Source = "0x01" },
        ])));
        Assert.Empty(Codes(Strategy(blobs:
        [
            new BlobDefinition { Name = "tls", Source = "0x00" },
            new BlobDefinition { Name = "TLS", Source = "0x01" },
        ])));
    }

    [Theory]
    [InlineData("FAKE")]
    [InlineData("multidisorder_legacy")]
    [InlineData("udplen")]
    public void AllowedFunctionsIgnoreCase(string function)
    {
        Assert.Empty(Codes(Strategy(action: new LuaActionDefinition { Function = function })));
    }

    [Theory]
    [InlineData("")]
    [InlineData("os.execute")]
    [InlineData("fake2")]
    public void OtherFunctionsAreRejected(string function)
    {
        Assert.Contains(
            "LUA_FUNCTION_NOT_ALLOWED",
            Codes(Strategy(action: new LuaActionDefinition { Function = function })));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("x")]
    [InlineData("n2-")]
    [InlineData("-d10")]
    [InlineData("s1<s-5")]
    [InlineData("p0-b3")]
    public void ValidRangesAreAccepted(string range)
    {
        Assert.Empty(Codes(Strategy(action: new LuaActionDefinition { Function = "fake", InRange = range, OutRange = range })));
    }

    [Theory]
    [InlineData("")]
    [InlineData("-")]
    [InlineData("<")]
    [InlineData("A")]
    [InlineData("n")]
    [InlineData("q1-")]
    [InlineData("n1-n2 ")]
    [InlineData("n1-n2\n")]
    public void InvalidRangesAreRejected(string range)
    {
        Assert.Contains(
            "ACTION_RANGE_INVALID",
            Codes(Strategy(action: new LuaActionDefinition { Function = "fake", OutRange = range })));
    }

    [Fact]
    public void PayloadFiltersAreChecked()
    {
        Assert.Empty(Codes(Strategy(action: new LuaActionDefinition { Function = "fake", Payloads = ["TLS_CLIENT_HELLO", "quic_initial"] })));
        Assert.Contains("ACTION_PAYLOAD_INVALID", Codes(Strategy(action: new LuaActionDefinition { Function = "fake", Payloads = [] })));
        Assert.Contains("ACTION_PAYLOAD_INVALID", Codes(Strategy(action: new LuaActionDefinition { Function = "fake", Payloads = ["all", "smtp"] })));
    }

    [Fact]
    public void ArgumentsAreChecked()
    {
        LuaActionDefinition WithArgument(string name, string? value) => new()
        {
            Function = "fake",
            Arguments = [new LuaArgumentDefinition { Name = name, Value = value }],
        };

        Assert.Empty(Codes(Strategy(action: WithArgument("blob", "fake_default_tls"))));
        Assert.Empty(Codes(Strategy(action: WithArgument("_flag", null))));
        Assert.Empty(Codes(Strategy(action: WithArgument("pos", "host+1,midsld-2,<x>=@a%b#c:d/e\\f"))));
        Assert.Contains("ACTION_ARGUMENT_NAME_INVALID", Codes(Strategy(action: WithArgument("1x", null))));
        Assert.Contains("ACTION_ARGUMENT_NAME_INVALID", Codes(Strategy(action: WithArgument("a-b", null))));
        Assert.Contains("ACTION_ARGUMENT_RESERVED", Codes(Strategy(action: WithArgument("Strategy", null))));
        Assert.Contains("ACTION_ARGUMENT_RESERVED", Codes(Strategy(action: WithArgument("final", null))));
        Assert.Contains("ACTION_ARGUMENT_VALUE_UNSAFE", Codes(Strategy(action: WithArgument("blob", "a b"))));
        Assert.Contains("ACTION_ARGUMENT_VALUE_UNSAFE", Codes(Strategy(action: WithArgument("blob", "a\"b"))));
        Assert.Contains("ACTION_ARGUMENT_VALUE_UNSAFE", Codes(Strategy(action: WithArgument("blob", "a&b"))));
        Assert.Contains("ACTION_ARGUMENT_VALUE_UNSAFE", Codes(Strategy(action: WithArgument("blob", "a\n"))));
    }

    private static BlockCheckTarget Target(
        string id = "t1",
        string host = "example.com",
        string path = "/",
        BlockCheckProtocol protocol = BlockCheckProtocol.Tls12,
        BlockCheckIpVersion ipVersion = BlockCheckIpVersion.IPv4,
        int? port = null) => new()
        {
            Id = id,
            Host = host,
            Path = path,
            Protocol = protocol,
            IpVersion = ipVersion,
            CustomPort = port,
        };

    [Fact]
    public void ValidTargetsHaveNoIssues()
    {
        Assert.Empty(Validator.ValidateTargets([Target(), Target("t2", port: 1), Target("t3", port: 65535)]));
        Assert.Throws<ArgumentNullException>(() => Validator.ValidateTargets(null!));
    }

    [Fact]
    public void TargetFieldsAreChecked()
    {
        string[] TargetCodes(BlockCheckTarget target) =>
            Validator.ValidateTargets([target]).Select(issue => issue.Code).ToArray();

        Assert.Equal(["TARGET_ID_INVALID"], TargetCodes(Target(id: "")));
        Assert.Equal(["TARGET_ID_INVALID"], TargetCodes(Target(id: "a/b")));
        Assert.Equal(["TARGET_HOST_INVALID"], TargetCodes(Target(host: "")));
        Assert.Equal(["TARGET_HOST_INVALID"], TargetCodes(Target(host: "10.0.0.1")));
        Assert.Equal(["TARGET_PROTOCOL_INVALID"], TargetCodes(Target(protocol: (BlockCheckProtocol)42)));
        Assert.Equal(["TARGET_IP_VERSION_INVALID"], TargetCodes(Target(ipVersion: (BlockCheckIpVersion)42)));
        Assert.Equal(["TARGET_PORT_INVALID"], TargetCodes(Target(port: 0)));
        Assert.Equal(["TARGET_PORT_INVALID"], TargetCodes(Target(port: 65536)));
        Assert.Equal(["TARGET_PATH_INVALID"], TargetCodes(Target(path: "")));
        Assert.Equal(["TARGET_PATH_INVALID"], TargetCodes(Target(path: "index.html")));
    }

    [Fact]
    public void TargetIdsAreUniqueAndCaseSensitive()
    {
        var issue = Assert.Single(Validator.ValidateTargets([Target("t1"), Target("t1")]));

        Assert.Equal("TARGET_ID_DUPLICATE", issue.Code);
        Assert.Empty(Validator.ValidateTargets([Target("t1"), Target("T1")]));
    }
}
