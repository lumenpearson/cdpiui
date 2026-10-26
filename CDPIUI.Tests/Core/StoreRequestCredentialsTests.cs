using CDPIUI.Core.Store.Network;

namespace CDPIUI.Tests.Core;

public class StoreRequestCredentialsTests
{
    private static HttpRequestMessage Request(string source, bool viaProxy, string? token = "repo-token")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://proxy.example/v1/fetch");
        RequestWorker.AddCredentials(request.Headers, new Uri(source), viaProxy, token, "Bearer", "proxy-key");
        return request;
    }

    [Theory]
    [InlineData("https://api.github.com/repos/o/r/releases/latest")]
    [InlineData("https://GitLab.com/api/v4/projects/o%2Fr/releases")]
    [InlineData("https://objects.githubusercontent.com/file")]
    public void DirectRequestToARepositoryHostGetsTheToken(string source)
    {
        using HttpRequestMessage request = Request(source, viaProxy: false);

        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("repo-token", request.Headers.Authorization?.Parameter);
        Assert.False(request.Headers.Contains("X-Key"));
    }

    [Fact]
    public void DirectRequestToAnotherHostGetsNoToken()
    {
        using HttpRequestMessage request = Request("https://github.com.evil.example/file", viaProxy: false);

        Assert.Null(request.Headers.Authorization);
        Assert.False(request.Headers.Contains("X-Key"));
    }

    [Fact]
    public void DirectRequestWithoutTokenIsAnonymous()
    {
        using HttpRequestMessage request = Request("https://api.github.com/rate_limit", viaProxy: false, token: "");

        Assert.Null(request.Headers.Authorization);
    }

    [Theory]
    [InlineData("https://api.github.com/repos/o/r/releases/latest")]
    [InlineData("https://raw.githubusercontent.com/o/r/main/file")]
    [InlineData("https://example.com/file")]
    public void ProxyRequestGetsOnlyTheProxyKey(string source)
    {
        // The proxy has a token of its own; the repository token must not reach it.
        using HttpRequestMessage request = Request(source, viaProxy: true);

        Assert.Null(request.Headers.Authorization);
        Assert.Equal(["proxy-key"], request.Headers.GetValues("X-Key"));
    }
}
