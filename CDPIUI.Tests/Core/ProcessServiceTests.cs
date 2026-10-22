using CDPIUI.Core.ComponentServices;

namespace CDPIUI.Tests.Core;

public class ProcessServiceTests
{
    [Theory]
    [InlineData("-p 1080", "-p 1080 --quiet")]
    [InlineData("-p 1080 --quiet", "-p 1080 --quiet")]
    [InlineData("--quiet -p 1080", "-p 1080 --quiet")]
    [InlineData("", "--quiet")]
    [InlineData("--quieter", "--quieter --quiet")]
    public void AppendQuietFlagKeepsSingleSeparatedFlag(string args, string expected)
    {
        Assert.Equal(expected, ProcessService.AppendQuietFlag(args));
    }
}
