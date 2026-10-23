using CDPIUI.Core.Proxy;

namespace CDPIUI.Tests.Core;

public class ProxyHelperTests
{
    [Theory]
    [InlineData("--port 1080 --mode=split", "--mode=split ")]
    [InlineData("--ip=127.0.0.1 --port=1080 --auto=a=b", "--auto=a=b ")]
    [InlineData("-addr 0.0.0.0 -f \"C:\\Program Files\\list.txt\"", "-f \"C:\\Program Files\\list.txt\" ")]
    [InlineData("  --disorder 1   --host example.com  ", "--disorder 1 ")]
    public void AddressAndPortArgumentsAreRemoved(string args, string expected)
    {
        Assert.Equal(expected, ProxyHelper.ReplaseIp(args));
    }
}
