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
}
