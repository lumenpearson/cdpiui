using CDPIUI.Core.Proxy;

namespace CDPIUI.Tests.Core;

public class ProxySettingsTests
{
    [Theory]
    [InlineData("socks=127.0.0.1:1080", "127.0.0.1", "1080")]
    [InlineData("127.0.0.1:8080", "127.0.0.1", "8080")]
    [InlineData("socks=[::1]:1080", "::1", "1080")]
    [InlineData("http=proxy.local:3128;https=proxy.local:3129", "proxy.local", "3128")]
    public void ParsesKnownFormats(string value, string host, string port)
    {
        Assert.True(WindowsNativeProxySettingsService.TryParseProxyServer(value, out string parsedHost, out string parsedPort));
        Assert.Equal(host, parsedHost);
        Assert.Equal(port, parsedPort);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("proxy.local")]
    [InlineData("[::1]")]
    [InlineData("::1")]
    public void RejectsValuesWithoutPort(string? value)
    {
        Assert.False(WindowsNativeProxySettingsService.TryParseProxyServer(value, out _, out _));
    }
}
