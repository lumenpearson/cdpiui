using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;

namespace CDPIUI.Tests.AddOns;

public class BlockCheckTargetInputParserTests
{
    private static readonly BlockCheckTargetInputParser Parser = new();

    private static BlockCheckTargetInputOptions Options(
        BlockCheckProtocol[]? protocols = null,
        BlockCheckIpVersion[]? ipVersions = null,
        int maximumTargets = 200) => new()
    {
        Protocols = new HashSet<BlockCheckProtocol>(protocols ?? [BlockCheckProtocol.TlsAuto]),
        IpVersions = new HashSet<BlockCheckIpVersion>(ipVersions ?? [BlockCheckIpVersion.IPv4]),
        MaximumTargets = maximumTargets,
    };

    [Fact]
    public void DefaultOptionsCreateTls12AndAutomaticTlsTargets()
    {
        var result = Parser.Parse("example.com");

        Assert.True(result.Success);
        Assert.Empty(result.Issues);
        Assert.Equal(
            ["target-001-tls12-ipv4", "target-002-tlsauto-ipv4"],
            result.Targets.Select(target => target.Id));
        Assert.All(result.Targets, target =>
        {
            Assert.Equal("example.com", target.Host);
            Assert.Equal("/", target.Path);
            Assert.Null(target.CustomPort);
        });
    }

    [Fact]
    public void EmptyProtocolIpVersionAndLimitAreAllReported()
    {
        var result = Parser.Parse("example.com", Options([], [], 0));

        Assert.False(result.Success);
        Assert.Empty(result.Targets);
        Assert.Equal(
            ["TARGET_INPUT_PROTOCOLS_EMPTY", "TARGET_INPUT_IP_VERSIONS_EMPTY", "TARGET_INPUT_LIMIT_INVALID"],
            result.Issues.Select(issue => issue.Code));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \r\n\t\n")]
    [InlineData("# only a comment\n  # another one")]
    public void EmptyInputIsAnError(string? input)
    {
        var result = Parser.Parse(input!);

        Assert.False(result.Success);
        Assert.Equal("TARGET_INPUT_EMPTY", Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void CommentsAndBlankLinesAreSkippedAndLineNumbersCountOnlyRealLines()
    {
        var result = Parser.Parse("# header\r\n\r\nexample.com\r\nbad host\r\n", Options());

        var issue = Assert.Single(result.Issues);
        Assert.Equal("TARGET_INPUT_INVALID", issue.Code);
        Assert.Equal("line-2", issue.SubjectId);
        Assert.Equal("example.com", Assert.Single(result.Targets).Host);
        Assert.False(result.Success);
    }

    [Theory]
    [InlineData("ftp://example.com/file", "TARGET_INPUT_SCHEME_UNSUPPORTED")]
    [InlineData("file://server/share", "TARGET_INPUT_SCHEME_UNSUPPORTED")]
    [InlineData("1.2.3.4", "TARGET_INPUT_HOST_INVALID")]
    [InlineData("https://[::1]/", "TARGET_INPUT_HOST_INVALID")]
    [InlineData("exa mple.com", "TARGET_INPUT_INVALID")]
    [InlineData("https://", "TARGET_INPUT_INVALID")]
    public void InvalidLinesAreRejected(string line, string code)
    {
        var result = Parser.Parse(line, Options());

        Assert.Empty(result.Targets);
        Assert.Contains(result.Issues, issue => issue.Code == code && issue.SubjectId == "line-1");
        Assert.False(result.Success);
    }

    [Fact]
    public void HttpUrlUsesOnlyHttpProtocol()
    {
        var result = Parser.Parse(
            "HTTP://Example.COM./Path?q=1",
            Options([BlockCheckProtocol.Http, BlockCheckProtocol.Tls12]));

        var target = Assert.Single(result.Targets);
        Assert.Equal(BlockCheckProtocol.Http, target.Protocol);
        Assert.Equal("example.com", target.Host);
        Assert.Equal("/Path?q=1", target.Path);
        Assert.Equal("target-001-http-ipv4", target.Id);
    }

    [Fact]
    public void HttpUrlWithHttpDisabledIsOnlyAWarning()
    {
        var result = Parser.Parse(
            "http://example.com\nhttps://example.org",
            Options([BlockCheckProtocol.Tls13]));

        var issue = Assert.Single(result.Issues);
        Assert.Equal(BlockCheckIssueSeverity.Warning, issue.Severity);
        Assert.Equal("TARGET_INPUT_PROTOCOL_DISABLED", issue.Code);
        Assert.Equal("line-1", issue.SubjectId);
        Assert.True(result.Success);
        Assert.Equal(BlockCheckProtocol.Tls13, Assert.Single(result.Targets).Protocol);
    }

    [Fact]
    public void HttpsUrlNeverUsesPlainHttp()
    {
        var result = Parser.Parse(
            "https://example.com",
            Options([BlockCheckProtocol.Http, BlockCheckProtocol.Quic, BlockCheckProtocol.Tls12]));

        Assert.Equal(
            [BlockCheckProtocol.Tls12, BlockCheckProtocol.Quic],
            result.Targets.Select(target => target.Protocol));
    }

    [Fact]
    public void OnlyDisabledProtocolsLeaveNoTargetsAndAnError()
    {
        var result = Parser.Parse("http://example.com", Options([BlockCheckProtocol.Tls12]));

        Assert.Empty(result.Targets);
        Assert.Equal(
            ["TARGET_INPUT_PROTOCOL_DISABLED", "TARGET_INPUT_NO_TARGETS"],
            result.Issues.Select(issue => issue.Code));
        Assert.False(result.Success);
    }

    [Theory]
    [InlineData("example.com:8443", 8443)]
    [InlineData("example.com:8443/path", 8443)]
    [InlineData("https://example.com:8443/", 8443)]
    [InlineData("https://example.com:443/", null)]
    [InlineData("example.com/a:b", null)]
    [InlineData("example.com?x=1:2", null)]
    public void CustomPortIsKeptOnlyWhenGiven(string line, int? port)
    {
        var target = Assert.Single(Parser.Parse(line, Options()).Targets);

        Assert.Equal(port, target.CustomPort);
    }

    [Fact]
    public void DuplicateLinesAreMergedCaseInsensitivelyForTheHost()
    {
        var result = Parser.Parse("example.com\nEXAMPLE.com\nhttps://example.com/\nexample.com.", Options());

        Assert.Single(result.Targets);
    }

    [Fact]
    public void IdnHostIsConvertedToPunycode()
    {
        var target = Assert.Single(Parser.Parse("пример.рф", Options()).Targets);

        Assert.Equal("xn--e1afmkfd.xn--p1ai", target.Host);
    }

    [Fact]
    public void TargetsAreOrderedByProtocolThenIpVersion()
    {
        var result = Parser.Parse(
            "example.com",
            Options(
                [BlockCheckProtocol.Quic, BlockCheckProtocol.Http],
                [BlockCheckIpVersion.IPv6, BlockCheckIpVersion.IPv4]));

        Assert.Equal(
            ["target-001-http-ipv4", "target-002-http-ipv6", "target-003-quic-ipv4", "target-004-quic-ipv6"],
            result.Targets.Select(target => target.Id));
    }

    [Fact]
    public void LimitIsCheckedAfterDuplicatesAreRemoved()
    {
        Assert.Single(Parser.Parse("a.com\na.com", Options(maximumTargets: 1)).Targets);

        var result = Parser.Parse("a.com\nb.com", Options(maximumTargets: 1));

        Assert.Empty(result.Targets);
        Assert.Equal("TARGET_INPUT_LIMIT_EXCEEDED", Assert.Single(result.Issues).Code);
        Assert.Contains("2 targets", result.Issues[0].Message);
    }
}
