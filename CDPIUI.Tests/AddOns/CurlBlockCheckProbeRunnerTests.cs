using CDPIUI.AddOns.BlockCheck2.Execution;
using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.Tests.AddOns;

public class CurlBlockCheckProbeRunnerTests
{
    private const string Marker = "__CDPIUI_BLOCKCHECK2__";

    private sealed class FakeExecutor(Func<IReadOnlyList<string>, CommandExecutionResult> handler) : ICommandExecutor
    {
        public List<IReadOnlyList<string>> Calls { get; } = [];

        public Task<CommandExecutionResult> ExecuteAsync(
            string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            Calls.Add(arguments);
            return Task.FromResult(handler(arguments));
        }
    }

    private static BlockCheckTarget Target(
        BlockCheckProtocol protocol, string host = "example.com", int? port = null,
        BlockCheckIpVersion ip = BlockCheckIpVersion.IPv4) => new()
        {
            Id = "t1",
            Host = host,
            Path = "/path?q=1",
            Protocol = protocol,
            IpVersion = ip,
            CustomPort = port,
        };

    private static Task<ProbeAttempt> Probe(
        CommandExecutionResult result, BlockCheckProtocol protocol = BlockCheckProtocol.Tls12, string host = "example.com") =>
        new CurlBlockCheckProbeRunner(executor: new FakeExecutor(_ => result)).ProbeAsync(Target(protocol, host), CancellationToken.None);

    [Fact]
    public void ArgumentsDependOnProtocol()
    {
        CurlBlockCheckProbeRunner runner = new(new CurlBlockCheckProbeRunnerOptions
        {
            ConnectTimeout = TimeSpan.FromMilliseconds(1),
            RequestTimeout = TimeSpan.FromSeconds(2.5),
            UseHeadForTls = true,
            UseHeadForQuic = false,
        }, new FakeExecutor(_ => new CommandExecutionResult(0, "", "")));

        IReadOnlyList<string> http = runner.BuildArguments(Target(BlockCheckProtocol.Http, "Example.COM."));
        Assert.Equal("http://example.com:80/path?q=1", http[^1]);
        Assert.Equal("0.1", http[http.ToList().IndexOf("--connect-timeout") + 1]);
        Assert.Equal("2.5", http[http.ToList().IndexOf("--max-time") + 1]);
        Assert.Contains("-4", http);
        Assert.Contains("--http1.1", http);
        Assert.DoesNotContain("--head", http);

        IReadOnlyList<string> tls12 = runner.BuildArguments(Target(BlockCheckProtocol.Tls12, ip: BlockCheckIpVersion.IPv6, port: 8443));
        Assert.Equal("https://example.com:8443/path?q=1", tls12[^1]);
        Assert.Contains("-6", tls12);
        Assert.Equal(["--head", "--http1.1", "--tlsv1.2", "--tls-max", "1.2"], tls12.Skip(tls12.Count - 6).Take(5));

        IReadOnlyList<string> tls13 = runner.BuildArguments(Target(BlockCheckProtocol.Tls13));
        Assert.Equal(["--head", "--http1.1", "--tlsv1.3", "--tls-max", "1.3"], tls13.Skip(tls13.Count - 6).Take(5));

        IReadOnlyList<string> auto = runner.BuildArguments(Target(BlockCheckProtocol.TlsAuto));
        Assert.Equal("--head", auto[^2]);
        Assert.DoesNotContain("--http1.1", auto);

        IReadOnlyList<string> quic = runner.BuildArguments(Target(BlockCheckProtocol.Quic));
        Assert.Equal("--http3-only", quic[^2]);
        Assert.DoesNotContain("--head", quic);

        Assert.Throws<ArgumentOutOfRangeException>(() => runner.BuildArguments(Target((BlockCheckProtocol)42)));
        Assert.Throws<ArgumentNullException>(() => runner.BuildArguments(null!));
    }

    [Fact]
    public void DefaultOptionsUseHeadOnlyForQuic()
    {
        CurlBlockCheckProbeRunner runner = new(executor: new FakeExecutor(_ => new CommandExecutionResult(0, "", "")));

        Assert.DoesNotContain("--head", runner.BuildArguments(Target(BlockCheckProtocol.Tls12)));
        Assert.Contains("--head", runner.BuildArguments(Target(BlockCheckProtocol.Quic)));
    }

    [Fact]
    public async Task SuccessfulProbeReportsTiming()
    {
        ProbeAttempt attempt = await Probe(new CommandExecutionResult(0, $"{Marker}200|0.125|", ""));

        Assert.True(attempt.Success);
        Assert.Equal(125d, attempt.TimeToFirstByteMs, 6);
        Assert.Equal(200, attempt.HttpStatusCode);
        Assert.Equal("", attempt.FailureCode);
        Assert.Equal("", attempt.Diagnostic);
    }

    [Theory]
    [InlineData(6, "dns-resolution-failed")]
    [InlineData(2, "curl-feature-unsupported")]
    [InlineData(4, "curl-feature-unsupported")]
    [InlineData(5, "proxy-resolution-failed")]
    [InlineData(7, "connection-failed")]
    [InlineData(28, "timeout")]
    [InlineData(35, "tls-handshake-failed")]
    [InlineData(60, "certificate-validation-failed")]
    [InlineData(56, "curl-exit-56")]
    public async Task CurlExitCodesAreNamed(int exitCode, string failure)
    {
        ProbeAttempt attempt = await Probe(new CommandExecutionResult(exitCode, $"{Marker}000|0.000|", "\n  curl: (x) failed  \nmore"));

        Assert.False(attempt.Success);
        Assert.Equal(-1, attempt.TimeToFirstByteMs);
        Assert.Equal(exitCode, attempt.ExitCode);
        Assert.Equal(failure, attempt.FailureCode);
        Assert.Equal("curl: (x) failed", attempt.Diagnostic);
    }

    [Fact]
    public async Task MissingStatusIsAFailure()
    {
        ProbeAttempt attempt = await Probe(new CommandExecutionResult(0, "garbage output\nsecond", ""));

        Assert.False(attempt.Success);
        Assert.Equal("http-status-missing", attempt.FailureCode);
        Assert.Equal("garbage output", attempt.Diagnostic);
    }

    [Fact]
    public async Task MarkerWithBadValuesIsAFailure()
    {
        ProbeAttempt attempt = await Probe(new CommandExecutionResult(0, $"{Marker}abc|x", ""));

        Assert.False(attempt.Success);
        Assert.Equal(0, attempt.HttpStatusCode);
        Assert.Equal("http-status-missing", attempt.FailureCode);
        Assert.Equal("", attempt.Diagnostic);
    }

    [Theory]
    [InlineData(400, "", "http-400-possible-fake-leak")]
    [InlineData(451, "", "http-451")]
    [InlineData(302, "http://blocked.provider.net/stop", "http-302")]
    public async Task ProviderStubsOverPlainHttpAreFailures(int status, string redirect, string failure)
    {
        ProbeAttempt attempt = await Probe(
            new CommandExecutionResult(0, $"{Marker}{status}|0.01|{redirect}", ""),
            BlockCheckProtocol.Http);

        Assert.False(attempt.Success);
        Assert.Equal(failure, attempt.FailureCode);
    }

    [Theory]
    [InlineData(301, "https://example.com/")]
    [InlineData(302, "https://www.example.com/path")]
    [InlineData(302, "HTTPS://EXAMPLE.COM./")]
    [InlineData(302, "")]
    [InlineData(302, "not a url")]
    [InlineData(404, "")]
    public async Task NormalHttpAnswersAreSuccesses(int status, string redirect)
    {
        ProbeAttempt attempt = await Probe(
            new CommandExecutionResult(0, $"{Marker}{status}|0.01|{redirect}", ""),
            BlockCheckProtocol.Http);

        Assert.True(attempt.Success);
    }

    [Fact]
    public async Task RedirectFromSubdomainToParentIsASuccess()
    {
        ProbeAttempt attempt = await Probe(
            new CommandExecutionResult(0, $"{Marker}301|0.01|https://example.com/", ""),
            BlockCheckProtocol.Http,
            "www.example.com");

        Assert.True(attempt.Success);
    }

    [Fact]
    public async Task Http400OverTlsIsNotAProviderStub()
    {
        ProbeAttempt attempt = await Probe(new CommandExecutionResult(0, $"{Marker}400|0.01|", ""));

        Assert.True(attempt.Success);
    }

    [Fact]
    public async Task StartFailureIsReported()
    {
        CurlBlockCheckProbeRunner runner = new(executor: new FakeExecutor(_ => throw new InvalidOperationException(new string('x', 2000))));

        ProbeAttempt attempt = await runner.ProbeAsync(Target(BlockCheckProtocol.Tls12), CancellationToken.None);

        Assert.False(attempt.Success);
        Assert.Equal("curl-start-failed", attempt.FailureCode);
        Assert.Equal(1024, attempt.Diagnostic.Length);
    }

    [Fact]
    public async Task CancellationIsNotSwallowed()
    {
        CurlBlockCheckProbeRunner runner = new(executor: new FakeExecutor(_ => throw new OperationCanceledException()));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            runner.ProbeAsync(Target(BlockCheckProtocol.Tls12), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => runner.ProbeAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task PreflightReportsMissingFeatures()
    {
        FakeExecutor executor = new(arguments => arguments[0] switch
        {
            "--version" => new CommandExecutionResult(0, "curl 8.0\r\nFeatures: SSL", ""),
            _ => new CommandExecutionResult(2, "", "curl: option --tls-max: is unknown"),
        });
        CurlBlockCheckProbeRunner runner = new(executor: executor);

        var issues = await runner.CheckAsync(
            [Target(BlockCheckProtocol.Tls12), Target(BlockCheckProtocol.Tls13), Target(BlockCheckProtocol.Quic)],
            CancellationToken.None);
        CurlCapabilities capabilities = await runner.DetectCapabilitiesAsync(CancellationToken.None);

        Assert.Equal(["CURL_TLS_MAX_UNSUPPORTED", "CURL_TLS13_UNSUPPORTED", "CURL_HTTP3_UNSUPPORTED"], issues.Select(i => i.Code));
        Assert.Equal("curl 8.0", capabilities.Version);
        Assert.Equal(3, executor.Calls.Count);
    }

    [Fact]
    public async Task PreflightPassesWithFullCurl()
    {
        FakeExecutor executor = new(arguments => arguments[0] == "--version"
            ? new CommandExecutionResult(0, "curl 8.9\nFeatures: HTTP3 SSL", "")
            : new CommandExecutionResult(7, "", "curl: (7) Failed to connect"));
        CurlBlockCheckProbeRunner runner = new(executor: executor);

        var issues = await runner.CheckAsync(
            [Target(BlockCheckProtocol.Tls13), Target(BlockCheckProtocol.Quic)], CancellationToken.None);

        Assert.Empty(issues);
        await Assert.ThrowsAsync<ArgumentNullException>(() => runner.CheckAsync(null!, CancellationToken.None));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MissingCurlIsReported(bool throws)
    {
        FakeExecutor executor = new(_ => throws
            ? throw new System.ComponentModel.Win32Exception("not found")
            : new CommandExecutionResult(1, "", "\n bad curl \n"));
        CurlBlockCheckProbeRunner runner = new(executor: executor);

        var issue = Assert.Single(await runner.CheckAsync([Target(BlockCheckProtocol.Tls12)], CancellationToken.None));

        Assert.Equal("CURL_NOT_AVAILABLE", issue.Code);
        Assert.EndsWith(throws ? "not found" : "bad curl", issue.Message);
    }
}
