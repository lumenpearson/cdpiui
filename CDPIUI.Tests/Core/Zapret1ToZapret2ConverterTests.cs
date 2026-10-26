using CDPIUI.Core.ComponentServices.Helpers.Configuration.Converters;

namespace CDPIUI.Tests.Core;

public class Zapret1ToZapret2ConverterTests
{
    [Theory]
    [InlineData("split2", "multisplit")]
    [InlineData("disorder2", "multidisorder")]
    [InlineData("split", "fakedsplit")]
    [InlineData("disorder", "fakeddisorder")]
    public void OldStrategyNamesAreConverted(string oldName, string newName)
    {
        var result = new Zapret1ToZapret2Converter().Convert(
            $"--wf-tcp=443 --filter-tcp=443 --dpi-desync=fake,{oldName} --dpi-desync-split-pos=1");

        Assert.True(result.IsSuccessful, string.Join("; ", result.Issues.Select(i => i.Message)));
        Assert.Contains(newName, result.StartupString);
    }

    [Fact]
    public void WindowSizeIsDroppedWithWarning()
    {
        var result = new Zapret1ToZapret2Converter().Convert(
            "--wf-tcp=443 --filter-tcp=443 --wssize=1:6 --dpi-desync=fake");

        Assert.True(result.IsSuccessful, string.Join("; ", result.Issues.Select(i => i.Message)));
        Assert.Contains(result.Issues, i => i.Code == "DROPPED_OPTION");
        Assert.DoesNotContain("wssize", result.StartupString);
    }

    [Fact]
    public void FlowsealGeneralMatchesExpectedFile()
    {
        GoldenFile input = GoldenFile.Load("Zapret1ToZapret2", "flowseal-general.winws1.txt");
        GoldenFile expected = GoldenFile.Load("Zapret1ToZapret2", "flowseal-general.winws2.expected.txt");

        var result = new Zapret1ToZapret2Converter().Convert(input.CommandLine("arguments"));

        Assert.True(result.IsSuccessful, string.Join("; ", result.Issues.Select(i => i.Message)));
        Assert.Equal(expected.Lines("issues"), result.Issues.Select(i => i.Code).Order(StringComparer.Ordinal));
        Assert.Equal(expected.CommandLine("startup"), result.StartupString);
    }

    [Theory]
    [InlineData("fake", "fake")]
    [InlineData("fakeknown", "fake")]
    [InlineData("multisplit", "multisplit")]
    [InlineData("MultiDisorder", "multidisorder")]
    [InlineData("fakedsplit", "fakedsplit")]
    [InlineData("fakeddisorder", "fakeddisorder")]
    [InlineData("hostfakesplit", "hostfakesplit")]
    [InlineData("syndata", "syndata")]
    [InlineData("synack", "synack")]
    [InlineData("rst", "rst")]
    [InlineData("rstack", "rst")]
    [InlineData("ipfrag2", "send")]
    [InlineData("udplen", "udplen")]
    [InlineData("tamper", "dht_dn")]
    public void EveryStrategyHasAConversionRule(string mode, string function)
    {
        var result = new Zapret1ToZapret2Converter().Convert(
            $"--wf-tcp=443 --filter-tcp=443 --dpi-desync={mode}");

        Assert.True(result.IsSuccessful, string.Join("; ", result.Issues.Select(i => i.Message)));
        Assert.Contains($"z1_func={function}", result.StartupString);
    }

    [Fact]
    public void UnknownStrategyIsAnError()
    {
        var result = new Zapret1ToZapret2Converter().Convert("--wf-tcp=443 --filter-tcp=443 --dpi-desync=teleport");

        Assert.False(result.IsSuccessful);
        Assert.Contains(result.Issues, i => i.Code == "UNSUPPORTED_STRATEGY");
    }

    [Fact]
    public void ZeroPhaseStrategyRunsBeforeDataStrategies()
    {
        var result = new Zapret1ToZapret2Converter().Convert(
            "--wf-tcp=443 --filter-tcp=443 --dpi-desync=fake,syndata,multisplit");

        Assert.True(result.IsSuccessful, string.Join("; ", result.Issues.Select(i => i.Message)));
        int syndata = result.StartupString.IndexOf("z1_func=syndata", StringComparison.Ordinal);
        int fake = result.StartupString.IndexOf("z1_func=fake", StringComparison.Ordinal);
        int split = result.StartupString.IndexOf("z1_func=multisplit", StringComparison.Ordinal);
        Assert.True(syndata >= 0 && syndata < fake && fake < split, result.StartupString);
    }

    [Fact]
    public void AnyProtocolSkipsEmptyPayloadExceptForZeroPhaseAndFakeKnown()
    {
        var result = new Zapret1ToZapret2Converter().Convert(
            "--wf-tcp=443 --filter-tcp=443 --dpi-desync=synack,fakeknown,rst --dpi-desync-any-protocol=1");

        string[] actions = result.StartupString.Split(' ').Where(a => a.StartsWith("--lua-desync=cdpi_z1_desync", StringComparison.Ordinal)).ToArray();
        Assert.Equal(3, actions.Length);
        Assert.DoesNotContain("payload=~empty", actions[0]);
        Assert.DoesNotContain("payload=~empty", actions[1]);
        Assert.Contains("payload=~empty", actions[2]);
        Assert.Contains("--payload=all", result.StartupString);
    }

    [Fact]
    public void FakeTlsModIsKeptUnlessNone()
    {
        var withMod = new Zapret1ToZapret2Converter().Convert(
            "--wf-tcp=443 --filter-tcp=443 --dpi-desync=fake --dpi-desync-fake-tls-mod=rnd,sni=x.com");
        var withNone = new Zapret1ToZapret2Converter().Convert(
            "--wf-tcp=443 --filter-tcp=443 --dpi-desync=fake --dpi-desync-fake-tls-mod=none");

        Assert.Contains("tls_mod=rnd,sni=x.com", withMod.StartupString);
        Assert.DoesNotContain("tls_mod", withNone.StartupString);
    }

    [Theory]
    [InlineData(true, "z1_func=multidisorder_legacy")]
    [InlineData(false, "z1_func=multidisorder:")]
    public void MultidisorderVariantFollowsTheOption(bool legacy, string expected)
    {
        var result = new Zapret1ToZapret2Converter(new ZapretConversionOptions { PreferLegacyMultidisorder = legacy })
            .Convert("--wf-tcp=443 --filter-tcp=443 --dpi-desync=multidisorder --dpi-desync-repeats=3");

        Assert.Contains(expected, result.StartupString);
        Assert.DoesNotContain("repeats=", result.StartupString);
    }

    [Fact]
    public void ModeOptionsBecomeArguments()
    {
        var result = new Zapret1ToZapret2Converter().Convert(
            "--wf-udp=443 --filter-udp=443 --dpi-desync=ipfrag2,udplen --dpi-desync-ipfrag-pos-udp=8 " +
            "--dpi-desync-udplen-increment=2 --dpi-desync-udplen-pattern=0x0F0F");

        Assert.True(result.IsSuccessful, string.Join("; ", result.Issues.Select(i => i.Message)));
        Assert.Contains("ipfrag_pos_udp=8", result.StartupString);
        Assert.Contains("z1_func=drop", result.StartupString);
        Assert.Contains("increment=2", result.StartupString);
        Assert.Contains("pattern=", result.StartupString);
    }
}
