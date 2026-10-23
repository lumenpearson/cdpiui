using CDPIUI.AddOns.ConfigShare;

namespace CDPIUI.Tests.AddOns;

public class ConfigShareWriteOptionsTests
{
    private static readonly string Package = Path.Combine(Path.GetTempPath(), "cdpiui-share-test");

    [Theory]
    [InlineData("--debug=@C:\\Windows\\System32\\drivers\\etc\\hosts")]
    [InlineData("--wf-save=C:\\Windows\\evil.sys")]
    [InlineData("--hostlist-auto ..\\..\\autohostlist.txt")]
    [InlineData("--hostlist-auto=autohostlist.txt")]
    public void WritesOutsideThePackageAreRejected(string args)
    {
        var exception = Assert.Throws<ConfigShareException>(() =>
            ConfigShareService.EnsureNoOutsideWrites($"--wf-tcp=443 {args} --dpi-desync=fake", Package));
        Assert.Equal("SHARE_UNSAFE_OPTION", exception.Code);
    }

    [Fact]
    public void WritesInsideThePackageAndPlainDebugAreAccepted()
    {
        string inside = Path.Combine(Package, "resources", "autohostlist.txt");

        ConfigShareService.EnsureNoOutsideWrites($"--debug=1 --hostlist-auto=\"{inside}\" --dpi-desync=fake", Package);
    }
}
