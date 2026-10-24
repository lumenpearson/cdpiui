using CDPIUI.Core.ComponentServices.Helpers.Configuration;

namespace CDPIUI.Tests.Core;

public class ConfigCommandLineTests
{
    [Fact]
    public void TokenizeKeepsQuotedPathTogether()
    {
        var tokens = ConfigCommandLine.Tokenize("--wf-tcp=80 --hostlist=\"C:\\My Lists\\a.txt\" --new");

        Assert.Equal(3, tokens.Count);
        Assert.Equal("--wf-tcp=80", tokens[0]);
        Assert.Equal("--new", tokens[2]);
    }

    [Fact]
    public void QuotedFolderEndingWithBackslashIsClosed()
    {
        var tokens = ConfigCommandLine.Tokenize("--lua-init=@\"C:\\dir\\\" --new --wf-udp=443");

        Assert.Equal(["--lua-init=@\"C:\\dir\\\"", "--new", "--wf-udp=443"], tokens);
    }

    [Fact]
    public void TokenizeEmptyReturnsEmpty()
    {
        Assert.Empty(ConfigCommandLine.Tokenize(null));
        Assert.Empty(ConfigCommandLine.Tokenize("   "));
    }

    [Fact]
    public void ApostropheDoesNotGroupArguments()
    {
        Assert.Equal(["--hostlist=O'Brien.txt", "--wf-tcp=443"],
            CDPIUI.Core.ComponentServices.Helpers.Configuration.ConfigCommandLine.Tokenize("--hostlist=O'Brien.txt --wf-tcp=443"));
    }
}
