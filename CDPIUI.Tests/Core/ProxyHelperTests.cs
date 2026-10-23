using CDPIUI.Core.Proxy;

namespace CDPIUI.Tests.Core;

public class ProxyHelperTests
{
    [Theory]
    [InlineData("--port 1080 --mode=split", "--mode=split ")]
    [InlineData("--ip=127.0.0.1 --port=1080 --auto=a=b", "--auto=a=b ")]
    [InlineData("-addr 0.0.0.0 -f \"C:\\Program Files\\list.txt\"", "-f \"C:\\Program Files\\list.txt\" ")]
    [InlineData("  --disorder 1   --host example.com  ", "--disorder 1 ")]
    [InlineData("-addr=127.0.0.1 -port=8080 -dns-addr 8.8.8.8", "-dns-addr 8.8.8.8 ")]
    public void AddressAndPortArgumentsAreRemoved(string args, string expected)
    {
        Assert.Equal(expected, ProxyHelper.ReplaseIp(args));
    }

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("127.0.0.2", true)]
    [InlineData("::1", true)]
    [InlineData("[::1]", true)]
    [InlineData("localhost", true)]
    [InlineData("0.0.0.0", false)]
    [InlineData("192.168.1.10", false)]
    [InlineData("::", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void OnlyLoopbackAddressesAreLocal(string? ip, bool expected)
    {
        Assert.Equal(expected, ProxyHelper.IsLoopbackAddress(ip));
    }

    [Theory]
    [InlineData("1080", true)]
    [InlineData(" 65535 ", true)]
    [InlineData("0", false)]
    [InlineData("65536", false)]
    [InlineData("-1", false)]
    [InlineData("80;evil", false)]
    [InlineData(null, false)]
    public void PortMustBeANumberInRange(string? port, bool expected)
    {
        Assert.Equal(expected, ProxyHelper.IsValidPort(port));
    }
}
