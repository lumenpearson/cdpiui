using System.Globalization;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.ViewModels;
using CDPIUI.ViewModels.BlockCheck2;

namespace CDPIUI.Tests.Presentation;

public class BlockCheck2ReviewPresentationTests
{
    [Fact]
    public void ConstructorKeepsSixEmptyValuesWithoutReadingDependencies()
    {
        var owner = new BlockCheck2ReviewPresentation(null!,
            _ => throw new InvalidOperationException("Early text read"),
            _ => throw new InvalidOperationException("Early duration format"));

        Assert.Equal(string.Empty, owner.ReviewSourcesText);
        Assert.Equal(string.Empty, owner.ReviewConnectionsText);
        Assert.Equal(string.Empty, owner.ReviewModeText);
        Assert.Equal(string.Empty, owner.ReviewRequestsText);
        Assert.Equal(string.Empty, owner.ReviewTimeoutsText);
        Assert.Equal(string.Empty, owner.ReviewMaximumTimeText);
    }

    [Theory]
    [InlineData(0, "C:|")]
    [InlineData(1, "C:HTTP|")]
    [InlineData(2, "C:TLS 1.2|")]
    [InlineData(3, "C:HTTP, TLS 1.2|")]
    [InlineData(4, "C:TLS 1.3|")]
    [InlineData(5, "C:HTTP, TLS 1.3|")]
    [InlineData(6, "C:TLS 1.2, TLS 1.3|")]
    [InlineData(7, "C:HTTP, TLS 1.2, TLS 1.3|")]
    [InlineData(8, "C:auto|")]
    [InlineData(9, "C:HTTP, auto|")]
    [InlineData(10, "C:TLS 1.2, auto|")]
    [InlineData(11, "C:HTTP, TLS 1.2, auto|")]
    [InlineData(12, "C:TLS 1.3, auto|")]
    [InlineData(13, "C:HTTP, TLS 1.3, auto|")]
    [InlineData(14, "C:TLS 1.2, TLS 1.3, auto|")]
    [InlineData(15, "C:HTTP, TLS 1.2, TLS 1.3, auto|")]
    [InlineData(16, "C:QUIC / HTTP/3|")]
    [InlineData(17, "C:HTTP, QUIC / HTTP/3|")]
    [InlineData(18, "C:TLS 1.2, QUIC / HTTP/3|")]
    [InlineData(19, "C:HTTP, TLS 1.2, QUIC / HTTP/3|")]
    [InlineData(20, "C:TLS 1.3, QUIC / HTTP/3|")]
    [InlineData(21, "C:HTTP, TLS 1.3, QUIC / HTTP/3|")]
    [InlineData(22, "C:TLS 1.2, TLS 1.3, QUIC / HTTP/3|")]
    [InlineData(23, "C:HTTP, TLS 1.2, TLS 1.3, QUIC / HTTP/3|")]
    [InlineData(24, "C:auto, QUIC / HTTP/3|")]
    [InlineData(25, "C:HTTP, auto, QUIC / HTTP/3|")]
    [InlineData(26, "C:TLS 1.2, auto, QUIC / HTTP/3|")]
    [InlineData(27, "C:HTTP, TLS 1.2, auto, QUIC / HTTP/3|")]
    [InlineData(28, "C:TLS 1.3, auto, QUIC / HTTP/3|")]
    [InlineData(29, "C:HTTP, TLS 1.3, auto, QUIC / HTTP/3|")]
    [InlineData(30, "C:TLS 1.2, TLS 1.3, auto, QUIC / HTTP/3|")]
    [InlineData(31, "C:HTTP, TLS 1.2, TLS 1.3, auto, QUIC / HTTP/3|")]
    [InlineData(32, "C:|IPv4")]
    [InlineData(33, "C:HTTP|IPv4")]
    [InlineData(34, "C:TLS 1.2|IPv4")]
    [InlineData(35, "C:HTTP, TLS 1.2|IPv4")]
    [InlineData(36, "C:TLS 1.3|IPv4")]
    [InlineData(37, "C:HTTP, TLS 1.3|IPv4")]
    [InlineData(38, "C:TLS 1.2, TLS 1.3|IPv4")]
    [InlineData(39, "C:HTTP, TLS 1.2, TLS 1.3|IPv4")]
    [InlineData(40, "C:auto|IPv4")]
    [InlineData(41, "C:HTTP, auto|IPv4")]
    [InlineData(42, "C:TLS 1.2, auto|IPv4")]
    [InlineData(43, "C:HTTP, TLS 1.2, auto|IPv4")]
    [InlineData(44, "C:TLS 1.3, auto|IPv4")]
    [InlineData(45, "C:HTTP, TLS 1.3, auto|IPv4")]
    [InlineData(46, "C:TLS 1.2, TLS 1.3, auto|IPv4")]
    [InlineData(47, "C:HTTP, TLS 1.2, TLS 1.3, auto|IPv4")]
    [InlineData(48, "C:QUIC / HTTP/3|IPv4")]
    [InlineData(49, "C:HTTP, QUIC / HTTP/3|IPv4")]
    [InlineData(50, "C:TLS 1.2, QUIC / HTTP/3|IPv4")]
    [InlineData(51, "C:HTTP, TLS 1.2, QUIC / HTTP/3|IPv4")]
    [InlineData(52, "C:TLS 1.3, QUIC / HTTP/3|IPv4")]
    [InlineData(53, "C:HTTP, TLS 1.3, QUIC / HTTP/3|IPv4")]
    [InlineData(54, "C:TLS 1.2, TLS 1.3, QUIC / HTTP/3|IPv4")]
    [InlineData(55, "C:HTTP, TLS 1.2, TLS 1.3, QUIC / HTTP/3|IPv4")]
    [InlineData(56, "C:auto, QUIC / HTTP/3|IPv4")]
    [InlineData(57, "C:HTTP, auto, QUIC / HTTP/3|IPv4")]
    [InlineData(58, "C:TLS 1.2, auto, QUIC / HTTP/3|IPv4")]
    [InlineData(59, "C:HTTP, TLS 1.2, auto, QUIC / HTTP/3|IPv4")]
    [InlineData(60, "C:TLS 1.3, auto, QUIC / HTTP/3|IPv4")]
    [InlineData(61, "C:HTTP, TLS 1.3, auto, QUIC / HTTP/3|IPv4")]
    [InlineData(62, "C:TLS 1.2, TLS 1.3, auto, QUIC / HTTP/3|IPv4")]
    [InlineData(63, "C:HTTP, TLS 1.2, TLS 1.3, auto, QUIC / HTTP/3|IPv4")]
    [InlineData(64, "C:|IPv6")]
    [InlineData(65, "C:HTTP|IPv6")]
    [InlineData(66, "C:TLS 1.2|IPv6")]
    [InlineData(67, "C:HTTP, TLS 1.2|IPv6")]
    [InlineData(68, "C:TLS 1.3|IPv6")]
    [InlineData(69, "C:HTTP, TLS 1.3|IPv6")]
    [InlineData(70, "C:TLS 1.2, TLS 1.3|IPv6")]
    [InlineData(71, "C:HTTP, TLS 1.2, TLS 1.3|IPv6")]
    [InlineData(72, "C:auto|IPv6")]
    [InlineData(73, "C:HTTP, auto|IPv6")]
    [InlineData(74, "C:TLS 1.2, auto|IPv6")]
    [InlineData(75, "C:HTTP, TLS 1.2, auto|IPv6")]
    [InlineData(76, "C:TLS 1.3, auto|IPv6")]
    [InlineData(77, "C:HTTP, TLS 1.3, auto|IPv6")]
    [InlineData(78, "C:TLS 1.2, TLS 1.3, auto|IPv6")]
    [InlineData(79, "C:HTTP, TLS 1.2, TLS 1.3, auto|IPv6")]
    [InlineData(80, "C:QUIC / HTTP/3|IPv6")]
    [InlineData(81, "C:HTTP, QUIC / HTTP/3|IPv6")]
    [InlineData(82, "C:TLS 1.2, QUIC / HTTP/3|IPv6")]
    [InlineData(83, "C:HTTP, TLS 1.2, QUIC / HTTP/3|IPv6")]
    [InlineData(84, "C:TLS 1.3, QUIC / HTTP/3|IPv6")]
    [InlineData(85, "C:HTTP, TLS 1.3, QUIC / HTTP/3|IPv6")]
    [InlineData(86, "C:TLS 1.2, TLS 1.3, QUIC / HTTP/3|IPv6")]
    [InlineData(87, "C:HTTP, TLS 1.2, TLS 1.3, QUIC / HTTP/3|IPv6")]
    [InlineData(88, "C:auto, QUIC / HTTP/3|IPv6")]
    [InlineData(89, "C:HTTP, auto, QUIC / HTTP/3|IPv6")]
    [InlineData(90, "C:TLS 1.2, auto, QUIC / HTTP/3|IPv6")]
    [InlineData(91, "C:HTTP, TLS 1.2, auto, QUIC / HTTP/3|IPv6")]
    [InlineData(92, "C:TLS 1.3, auto, QUIC / HTTP/3|IPv6")]
    [InlineData(93, "C:HTTP, TLS 1.3, auto, QUIC / HTTP/3|IPv6")]
    [InlineData(94, "C:TLS 1.2, TLS 1.3, auto, QUIC / HTTP/3|IPv6")]
    [InlineData(95, "C:HTTP, TLS 1.2, TLS 1.3, auto, QUIC / HTTP/3|IPv6")]
    [InlineData(96, "C:|IPv4, IPv6")]
    [InlineData(97, "C:HTTP|IPv4, IPv6")]
    [InlineData(98, "C:TLS 1.2|IPv4, IPv6")]
    [InlineData(99, "C:HTTP, TLS 1.2|IPv4, IPv6")]
    [InlineData(100, "C:TLS 1.3|IPv4, IPv6")]
    [InlineData(101, "C:HTTP, TLS 1.3|IPv4, IPv6")]
    [InlineData(102, "C:TLS 1.2, TLS 1.3|IPv4, IPv6")]
    [InlineData(103, "C:HTTP, TLS 1.2, TLS 1.3|IPv4, IPv6")]
    [InlineData(104, "C:auto|IPv4, IPv6")]
    [InlineData(105, "C:HTTP, auto|IPv4, IPv6")]
    [InlineData(106, "C:TLS 1.2, auto|IPv4, IPv6")]
    [InlineData(107, "C:HTTP, TLS 1.2, auto|IPv4, IPv6")]
    [InlineData(108, "C:TLS 1.3, auto|IPv4, IPv6")]
    [InlineData(109, "C:HTTP, TLS 1.3, auto|IPv4, IPv6")]
    [InlineData(110, "C:TLS 1.2, TLS 1.3, auto|IPv4, IPv6")]
    [InlineData(111, "C:HTTP, TLS 1.2, TLS 1.3, auto|IPv4, IPv6")]
    [InlineData(112, "C:QUIC / HTTP/3|IPv4, IPv6")]
    [InlineData(113, "C:HTTP, QUIC / HTTP/3|IPv4, IPv6")]
    [InlineData(114, "C:TLS 1.2, QUIC / HTTP/3|IPv4, IPv6")]
    [InlineData(115, "C:HTTP, TLS 1.2, QUIC / HTTP/3|IPv4, IPv6")]
    [InlineData(116, "C:TLS 1.3, QUIC / HTTP/3|IPv4, IPv6")]
    [InlineData(117, "C:HTTP, TLS 1.3, QUIC / HTTP/3|IPv4, IPv6")]
    [InlineData(118, "C:TLS 1.2, TLS 1.3, QUIC / HTTP/3|IPv4, IPv6")]
    [InlineData(119, "C:HTTP, TLS 1.2, TLS 1.3, QUIC / HTTP/3|IPv4, IPv6")]
    [InlineData(120, "C:auto, QUIC / HTTP/3|IPv4, IPv6")]
    [InlineData(121, "C:HTTP, auto, QUIC / HTTP/3|IPv4, IPv6")]
    [InlineData(122, "C:TLS 1.2, auto, QUIC / HTTP/3|IPv4, IPv6")]
    [InlineData(123, "C:HTTP, TLS 1.2, auto, QUIC / HTTP/3|IPv4, IPv6")]
    [InlineData(124, "C:TLS 1.3, auto, QUIC / HTTP/3|IPv4, IPv6")]
    [InlineData(125, "C:HTTP, TLS 1.3, auto, QUIC / HTTP/3|IPv4, IPv6")]
    [InlineData(126, "C:TLS 1.2, TLS 1.3, auto, QUIC / HTTP/3|IPv4, IPv6")]
    [InlineData(127, "C:HTTP, TLS 1.2, TLS 1.3, auto, QUIC / HTTP/3|IPv4, IPv6")]
    public void ProtocolAndIpTruthTableRetainsLiteralOrderAndSeparators(int mask, string expected)
    {
        using var f = new Fixture(); f.Source.Mask = mask;
        f.Update();
        Assert.Equal(expected, f.Owner.ReviewConnectionsText);
        Assert.Equal((mask & 8) != 0, f.Trace.Contains("text:BlockCheck2TlsAutoShortName"));
    }

    [Fact]
    public void UpdateReadsAndPublishesInTheOriginalOrder()
    {
        using var f = new Fixture(); f.Update();
        Assert.Equal(["text:BlockCheck2ReviewSourcesFormat", "lists", "domains", "changed:ReviewSourcesText",
            "http", "tls12", "tls13", "tlsauto", "quic", "ipv4", "ipv6", "text:BlockCheck2ReviewConnectionsFormat",
            "changed:ReviewConnectionsText", "preset", "text:BlockCheck2QuickModeName", "all", "preset", "changed:ReviewModeText",
            "text:BlockCheck2ReviewRequestsFormat", "requests", "changed:ReviewRequestsText",
            "text:BlockCheck2ReviewTimeoutsFormat", "connect", "request", "grace", "changed:ReviewTimeoutsText",
            "text:BlockCheck2ReviewMaximumTimeUnavailable", "changed:ReviewMaximumTimeText"], f.Trace);
        Assert.Equal(["S:2/5/11", "C:HTTP|IPv4", "quick", "R:2.5", "T:1.25|3.5|0.75", "none"], f.Values);
    }

    [Theory]
    [InlineData(BlockCheckRunPreset.Quick, "quick")]
    [InlineData(BlockCheckRunPreset.Balanced, "balanced")]
    [InlineData(BlockCheckRunPreset.Exhaustive, "exhaustive. all")]
    [InlineData((BlockCheckRunPreset)999, "balanced")]
    public void PresetBranchesAndUnknownFallbackRemainStable(BlockCheckRunPreset preset, string expected)
    {
        using var f = new Fixture(); f.Source.Preset = preset; f.Update();
        Assert.Equal(expected, f.Owner.ReviewModeText);
        Assert.Equal(2, f.Trace.Count(p => p == "preset"));
    }

    [Fact]
    public void AllStrategiesShortCircuitsTheSecondPresetRead()
    {
        using var f = new Fixture(); f.Source.All = true;
        f.OnStep = point => { if (point == "preset" && f.Trace.Count(p => p == point) > 1) throw new Sentinel(); };
        f.Update();
        Assert.Equal("quick. all", f.Owner.ReviewModeText);
        Assert.Equal(1, f.Trace.Count(p => p == "preset"));
    }

    [Fact]
    public void ModeLocalizationCanReplacePresetBeforeTheSecondRead()
    {
        using var f = new Fixture();
        f.OnStep = point => { if (point == "text:BlockCheck2QuickModeName") f.Source.Preset = BlockCheckRunPreset.Exhaustive; };
        f.Update();
        Assert.Equal("quick. all", f.Owner.ReviewModeText);
    }

    [Fact]
    public void EarlierPublicationsChangeLaterInputsWithoutAnEarlySnapshot()
    {
        using var f = new Fixture();
        f.OnStep = point =>
        {
            if (point == "changed:ReviewSourcesText") f.Source.Mask = 16 | 64;
            if (point == "changed:ReviewConnectionsText") f.Source.Preset = BlockCheckRunPreset.Exhaustive;
            if (point == "changed:ReviewModeText") f.Source.Requests = 7.5;
            if (point == "changed:ReviewRequestsText") f.Source.Connect = 19.25;
        };
        f.Update();
        Assert.Equal("C:QUIC / HTTP/3|IPv6", f.Owner.ReviewConnectionsText);
        Assert.Equal("exhaustive. all", f.Owner.ReviewModeText);
        Assert.Equal("R:7.5", f.Owner.ReviewRequestsText);
        Assert.Equal("T:19.25|3.5|0.75", f.Owner.ReviewTimeoutsText);
    }

    [Fact]
    public void SourceLocalizationPrecedesCountReads()
    {
        using var f = new Fixture();
        f.OnStep = point => { if (point == "text:BlockCheck2ReviewSourcesFormat") { f.Source.Lists = 7; f.Source.Domains = 19; } };
        f.Update();
        Assert.Equal("S:7/19/11", f.Owner.ReviewSourcesText);
    }

    [Fact]
    public void AutomaticTlsLocalizationCanChangeLaterProtocolAndIpReads()
    {
        using var f = new Fixture(); f.Source.Mask = 8;
        f.OnStep = point => { if (point == "text:BlockCheck2TlsAutoShortName") f.Source.Mask |= 16 | 64; };
        f.Update();
        Assert.Equal("C:auto, QUIC / HTTP/3|IPv6", f.Owner.ReviewConnectionsText);
        Assert.True(f.Trace.IndexOf("text:BlockCheck2TlsAutoShortName") < f.Trace.IndexOf("quic"));
        Assert.True(f.Trace.IndexOf("ipv6") < f.Trace.IndexOf("text:BlockCheck2ReviewConnectionsFormat"));
    }

    [Theory]
    [InlineData(null, "C:QUIC / HTTP/3|")]
    [InlineData("", "C:, QUIC / HTTP/3|")]
    [InlineData("automatic", "C:automatic, QUIC / HTTP/3|")]
    public void AutomaticTlsRetainsNullFilteringAndEmptyStringJoining(string? name, string expected)
    {
        using var f = new Fixture(); f.Source.Mask = 8 | 16; f.AutoName = name; f.Update();
        Assert.Equal(expected, f.Owner.ReviewConnectionsText);
    }

    [Theory]
    [InlineData(null)] [InlineData("{")] [InlineData("{9}")]
    public void InvalidSourcesFormatStillReadsCountsBeforeFormattingThrows(string? format)
    {
        using var f = new Fixture(); f.SourcesFormat = format;
        var error = Record.Exception(() => f.Update());
        if (format is null) Assert.IsType<ArgumentNullException>(error); else Assert.IsType<FormatException>(error);
        Assert.Equal(["text:BlockCheck2ReviewSourcesFormat", "lists", "domains"], f.Trace);
        Assert.All(f.Values, value => Assert.Equal(string.Empty, value));
    }

    [Fact]
    public void FormatCultureIsReadAfterTheNumericGetters()
    {
        using var f = new Fixture();
        f.OnStep = point => { if (point == "grace") CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE"); };
        f.Update();
        Assert.Equal("R:2.5", f.Owner.ReviewRequestsText);
        Assert.Equal("T:1,25|3,5|0,75", f.Owner.ReviewTimeoutsText);
    }

    [Fact]
    public void PositiveMaximumPassesDurationAndDistinctCountsToTheFormatter()
    {
        using var f = new Fixture(); var duration = TimeSpan.FromMinutes(61);
        f.Update(Preview(23, new() { Duration = duration, EstimatedStrategyJobs = 17, EstimatedRequests = 199 }));
        Assert.Equal("S:2/5/23", f.Owner.ReviewSourcesText);
        Assert.Equal($"M:D:{duration.Ticks}|17|199", f.Owner.ReviewMaximumTimeText);
        Assert.Contains("duration:" + duration.Ticks, f.Trace);
    }

    [Fact]
    public void DurationCallbackCanReplaceTheEstimateBeforeLaterCountReads()
    {
        using var f = new Fixture(); var original = TimeSpan.FromMinutes(3);
        var preview = Preview(11, new() { Duration = original, EstimatedStrategyJobs = 1, EstimatedRequests = 2 });
        f.OnStep = point =>
        {
            if (point.StartsWith("duration:", StringComparison.Ordinal))
                typeof(BlockCheck2PreparationPreview).GetProperty(nameof(BlockCheck2PreparationPreview.MaximumDuration))!
                    .SetValue(preview, new BlockCheckMaximumDurationEstimate { Duration = TimeSpan.FromDays(2), EstimatedStrategyJobs = 29, EstimatedRequests = 307 });
        };
        f.Update(preview);
        Assert.Equal($"M:D:{original.Ticks}|29|307", f.Owner.ReviewMaximumTimeText);
    }

    [Fact]
    public void MaximumLocalizationCanRemoveTheEstimateBeforeDurationIsRead()
    {
        using var f = new Fixture(); var preview = Preview(11, new() { Duration = TimeSpan.FromMinutes(3) });
        f.OnStep = point =>
        {
            if (point == "text:BlockCheck2ReviewMaximumTimeFormat")
                typeof(BlockCheck2PreparationPreview).GetProperty(nameof(BlockCheck2PreparationPreview.MaximumDuration))!.SetValue(preview, null);
        };
        Assert.Throws<NullReferenceException>(() => f.Update(preview));
        Assert.All(f.Values.Take(5), value => Assert.NotEqual(string.Empty, value));
        Assert.Equal(string.Empty, f.Owner.ReviewMaximumTimeText);
        Assert.DoesNotContain(f.Trace, p => p.StartsWith("duration:", StringComparison.Ordinal));
    }

    [Fact]
    public void NestedUpdatesRetainEachFramesPreviewAndCurrentSource()
    {
        using var f = new Fixture(); bool nested = false;
        var outer = Preview(11, new() { Duration = TimeSpan.FromMinutes(3), EstimatedStrategyJobs = 17, EstimatedRequests = 199 });
        var inner = Preview(37, new() { Duration = TimeSpan.FromMinutes(5), EstimatedStrategyJobs = 41, EstimatedRequests = 503 });
        f.OnStep = point =>
        {
            if (point == "changed:ReviewConnectionsText" && !nested)
            { nested = true; f.Source.Requests = 99; f.Update(inner); }
        };
        f.Update(outer);
        Assert.Equal("S:2/5/37", f.Owner.ReviewSourcesText);
        Assert.Equal("R:99", f.Owner.ReviewRequestsText);
        Assert.Equal($"M:D:{outer.MaximumDuration!.Duration.Ticks}|17|199", f.Owner.ReviewMaximumTimeText);
        Assert.Equal(2, f.Trace.Count(p => p == "changed:ReviewMaximumTimeText"));
    }

    [Fact]
    public void NullPreviewRetainsLocalizationAndCountReadsBeforeItsOriginalFailure()
    {
        using var f = new Fixture();
        Assert.Throws<NullReferenceException>(() => f.Owner.UpdateReviewSummary(null!));
        Assert.Equal(["text:BlockCheck2ReviewSourcesFormat", "lists", "domains"], f.Trace);
        Assert.All(f.Values, value => Assert.Equal(string.Empty, value));
    }

    [Fact]
    public void EqualUpdatesStillReadInputsAndLocalizeButSuppressEvents()
    {
        using var f = new Fixture(); f.Update();
        var first = f.Trace.Where(p => !p.StartsWith("changed:", StringComparison.Ordinal)).ToArray();
        f.Trace.Clear(); f.Update();
        Assert.Equal(first, f.Trace);
    }

    [Theory]
    [InlineData("ReviewSourcesText")]
    [InlineData("ReviewConnectionsText")]
    [InlineData("ReviewModeText")]
    [InlineData("ReviewRequestsText")]
    [InlineData("ReviewTimeoutsText")]
    [InlineData("ReviewMaximumTimeText")]
    public void EachSetterPreservesEqualityAndCurrentFieldBeforeNotification(string name)
    {
        using var f = new Fixture(); var property = typeof(BlockCheck2ReviewPresentation).GetProperty(name)!;
        f.OnStep = point => { if (point == "changed:" + name) Assert.Equal("value", property.GetValue(f.Owner)); };
        property.SetValue(f.Owner, "value"); property.SetValue(f.Owner, "value");
        Assert.Equal(["changed:" + name], f.Trace);
    }

    [Theory]
    [InlineData("text:BlockCheck2ReviewSourcesFormat", 0)]
    [InlineData("text:BlockCheck2ReviewConnectionsFormat", 1)]
    [InlineData("text:BlockCheck2QuickModeName", 2)]
    [InlineData("text:BlockCheck2ReviewRequestsFormat", 3)]
    [InlineData("text:BlockCheck2ReviewTimeoutsFormat", 4)]
    [InlineData("text:BlockCheck2ReviewMaximumTimeUnavailable", 5)]
    public void LocalizationFailuresRetainExceptionIdentityAndOnlyEarlierPublications(string point, int published)
    {
        using var f = new Fixture(); var failure = new Sentinel(); f.OnStep = p => { if (p == point) throw failure; };
        Assert.Same(failure, Assert.Throws<Sentinel>(() => f.Update()));
        Assert.Equal(published, f.Trace.Count(p => p.StartsWith("changed:", StringComparison.Ordinal)));
        Assert.All(f.Values.Take(published), value => Assert.NotEqual(string.Empty, value));
        Assert.All(f.Values.Skip(published), value => Assert.Equal(string.Empty, value));
    }

    [Theory]
    [InlineData("lists", 0)] [InlineData("domains", 0)]
    [InlineData("http", 1)] [InlineData("tls12", 1)] [InlineData("tls13", 1)]
    [InlineData("tlsauto", 1)] [InlineData("quic", 1)] [InlineData("ipv4", 1)] [InlineData("ipv6", 1)]
    [InlineData("preset", 2)] [InlineData("all", 2)] [InlineData("requests", 3)]
    [InlineData("connect", 4)] [InlineData("request", 4)] [InlineData("grace", 4)]
    public void EverySourceGetterCanFailAtItsOriginalPoint(string point, int published)
    {
        using var f = new Fixture(); var failure = new Sentinel(); f.OnStep = p => { if (p == point) throw failure; };
        Assert.Same(failure, Assert.Throws<Sentinel>(() => f.Update()));
        Assert.Equal(published, f.Trace.Count(p => p.StartsWith("changed:", StringComparison.Ordinal)));
        Assert.All(f.Values.Skip(published), value => Assert.Equal(string.Empty, value));
    }

    [Theory]
    [InlineData("ReviewSourcesText", 1)] [InlineData("ReviewConnectionsText", 2)]
    [InlineData("ReviewModeText", 3)] [InlineData("ReviewRequestsText", 4)]
    [InlineData("ReviewTimeoutsText", 5)] [InlineData("ReviewMaximumTimeText", 6)]
    public void ObserverFailureRetainsAssignedFieldAndCanRetry(string property, int assigned)
    {
        using var f = new Fixture(); var failure = new Sentinel();
        f.OnStep = p => { if (p == "changed:" + property) throw failure; };
        Assert.Same(failure, Assert.Throws<Sentinel>(() => f.Update()));
        Assert.All(f.Values.Take(assigned), value => Assert.NotEqual(string.Empty, value));
        Assert.All(f.Values.Skip(assigned), value => Assert.Equal(string.Empty, value));
        f.OnStep = null; f.Update(); Assert.All(f.Values, value => Assert.NotEqual(string.Empty, value));
    }

    [Fact]
    public void DurationFailureLeavesMaximumUnassignedWithoutWrappingTheException()
    {
        using var f = new Fixture(); var failure = new Sentinel();
        f.OnStep = point => { if (point.StartsWith("duration:", StringComparison.Ordinal)) throw failure; };
        Assert.Same(failure, Assert.Throws<Sentinel>(() => f.Update(Preview(11, new() { Duration = TimeSpan.FromMinutes(3) }))));
        Assert.All(f.Values.Take(5), value => Assert.NotEqual(string.Empty, value));
        Assert.Equal(string.Empty, f.Owner.ReviewMaximumTimeText);
    }

    [Fact]
    public void OwnersHaveIndependentValuesAndSourceReads()
    {
        using var first = new Fixture(); using var second = new Fixture(); first.Update();
        Assert.Empty(second.Trace); Assert.All(second.Values, value => Assert.Equal(string.Empty, value));
    }

    private static BlockCheck2PreparationPreview Preview(int count = 11, BlockCheckMaximumDurationEstimate? estimate = null) =>
        new(count, estimate, []);
    private sealed class Sentinel : Exception;

    private sealed class Fixture : IDisposable
    {
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        internal readonly List<string> Trace = [];
        internal Action<string>? OnStep;
        internal string? AutoName = "auto", SourcesFormat = "S:{0}/{1}/{2}";
        internal readonly Source Source;
        internal readonly BlockCheck2ReviewPresentation Owner;
        internal Fixture()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Source = new Source(this); Owner = new BlockCheck2ReviewPresentation(Source, Text, Duration);
            Owner.PropertyChanged += (sender, e) => { Assert.Same(Owner, sender); Step("changed:" + e.PropertyName); };
        }
        internal string[] Values => [Owner.ReviewSourcesText, Owner.ReviewConnectionsText, Owner.ReviewModeText,
            Owner.ReviewRequestsText, Owner.ReviewTimeoutsText, Owner.ReviewMaximumTimeText];
        internal void Update(BlockCheck2PreparationPreview? preview = null) => Owner.UpdateReviewSummary(preview ?? Preview());
        internal void Step(string point) { Trace.Add(point); OnStep?.Invoke(point); }
        internal T Read<T>(string point, Func<T> value) { Step(point); return value(); }
        private string Text(string key)
        {
            Step("text:" + key);
            return key switch
            {
                "BlockCheck2ReviewSourcesFormat" => SourcesFormat!,
                "BlockCheck2ReviewConnectionsFormat" => "C:{0}|{1}",
                "BlockCheck2TlsAutoShortName" => AutoName!,
                "BlockCheck2QuickModeName" => "quick", "BlockCheck2BalancedModeName" => "balanced",
                "BlockCheck2ExhaustiveModeName" => "exhaustive", "BlockCheck2ReviewAllStrategies" => "all",
                "BlockCheck2ReviewRequestsFormat" => "R:{0}", "BlockCheck2ReviewTimeoutsFormat" => "T:{0}|{1}|{2}",
                "BlockCheck2ReviewMaximumTimeUnavailable" => "none", "BlockCheck2ReviewMaximumTimeFormat" => "M:{0}|{1}|{2}",
                _ => throw new InvalidOperationException("Unexpected review key: " + key),
            };
        }
        private string Duration(TimeSpan duration) { Step("duration:" + duration.Ticks); return "D:" + duration.Ticks; }
        public void Dispose() => CultureInfo.CurrentCulture = _culture;
    }

    private sealed class Source(Fixture f) : IBlockCheck2ReviewSource
    {
        internal int Lists = 2, Domains = 5, Mask = 1 | 32;
        internal BlockCheckRunPreset Preset = BlockCheckRunPreset.Quick;
        internal bool All;
        internal double Requests = 2.5, Connect = 1.25, Request = 3.5, Grace = 0.75;
        public int SelectedSiteListCount => f.Read("lists", () => Lists);
        public int SelectedDomainCount => f.Read("domains", () => Domains);
        public bool IncludeHttp => f.Read("http", () => (Mask & 1) != 0);
        public bool IncludeTls12 => f.Read("tls12", () => (Mask & 2) != 0);
        public bool IncludeTls13 => f.Read("tls13", () => (Mask & 4) != 0);
        public bool IncludeTlsAuto => f.Read("tlsauto", () => (Mask & 8) != 0);
        public bool IncludeQuic => f.Read("quic", () => (Mask & 16) != 0);
        public bool IncludeIpv4 => f.Read("ipv4", () => (Mask & 32) != 0);
        public bool IncludeIpv6 => f.Read("ipv6", () => (Mask & 64) != 0);
        public BlockCheckRunPreset SelectedRunPreset => f.Read("preset", () => Preset);
        public bool TestAllStrategies => f.Read("all", () => All);
        public double RequestsPerDomain => f.Read("requests", () => Requests);
        public double ConnectTimeoutSeconds => f.Read("connect", () => Connect);
        public double RequestTimeoutSeconds => f.Read("request", () => Request);
        public double StartupGraceSeconds => f.Read("grace", () => Grace);
    }
}
