using CDPIUI.AddOns.BlockCheck2;
using CDPIUI.AddOns.BlockCheck2.Catalog;
using CDPIUI.AddOns.BlockCheck2.Execution;
using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.Tests.AddOns;

public class BlockCheckAddOnServiceTests
{
    private static readonly StrategyCatalog Catalog = StrategyCatalogLoader.LoadBuiltIn();

    private static readonly BlockCheckTarget[] Targets =
    [
        new() { Id = "t1", Host = "example.com", Protocol = BlockCheckProtocol.Tls12, IpVersion = BlockCheckIpVersion.IPv4 },
    ];

    [Fact]
    public async Task InvalidOptionsStopTheRunBeforeAnyCheck()
    {
        var probeRunner = new PreflightProbeRunner();
        BlockCheckRunOptions options = new()
        {
            Synthesis = new BlockCheckSynthesisOptions { MinimumAttempts = 0, MinimumSuccessRate = 2d },
        };

        var result = await new BlockCheckAddOnService().RunAsync(
            Catalog, Targets, new IdleStrategyRunner(), probeRunner, options);

        Assert.False(result.Success);
        Assert.False(result.WasCanceled);
        Assert.Equal(
            ["RUN_SYNTHESIS_ATTEMPTS_INVALID", "RUN_SYNTHESIS_RATE_INVALID", "RUN_SUCCESS_RATE_MISMATCH", "RUN_VALIDATION_RATE_MISMATCH"],
            result.PreflightIssues.Select(issue => issue.Code));
        Assert.Same(result.PreflightIssues, result.Scan.Issues);
        Assert.Equal(0, probeRunner.PreflightCalls);
        Assert.Equal(0, probeRunner.ProbeCalls);
        Assert.Null(result.Synthesis);
    }

    [Fact]
    public async Task PreflightErrorsStopTheRunBeforeTheScan()
    {
        var probeRunner = new PreflightProbeRunner
        {
            Issues = [new BlockCheckIssue(BlockCheckIssueSeverity.Error, "CURL_MISSING", "curl is missing.")],
        };

        var result = await new BlockCheckAddOnService().RunAsync(
            Catalog, Targets, new IdleStrategyRunner(), probeRunner);

        Assert.Equal(["CURL_MISSING"], result.PreflightIssues.Select(issue => issue.Code));
        Assert.Equal(1, probeRunner.PreflightCalls);
        Assert.Equal(0, probeRunner.ProbeCalls);
        Assert.Null(result.Synthesis);
    }

    [Fact]
    public async Task CanceledPreflightGivesACanceledResult()
    {
        using var cancellation = new CancellationTokenSource();
        var probeRunner = new PreflightProbeRunner { OnPreflight = cancellation.Cancel };

        var result = await new BlockCheckAddOnService().RunAsync(
            Catalog, Targets, new IdleStrategyRunner(), probeRunner, cancellationToken: cancellation.Token);

        Assert.True(result.WasCanceled);
        Assert.True(result.Scan.WasCanceled);
        Assert.Equal(["RUN_CANCELED"], result.PreflightIssues.Select(issue => issue.Code));
        Assert.Equal(0, probeRunner.ProbeCalls);
        Assert.Null(result.Synthesis);
    }

    private sealed class IdleStrategyRunner : IBlockCheckStrategyRunner
    {
        public Task StartAsync(string arguments, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class PreflightProbeRunner : IBlockCheckProbeRunner, IBlockCheckProbePreflight
    {
        public IReadOnlyList<BlockCheckIssue> Issues { get; init; } = [];
        public Action? OnPreflight { get; init; }
        public int PreflightCalls { get; private set; }
        public int ProbeCalls { get; private set; }

        public Task<IReadOnlyList<BlockCheckIssue>> CheckAsync(
            IEnumerable<BlockCheckTarget> targets,
            CancellationToken cancellationToken)
        {
            PreflightCalls++;
            OnPreflight?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Issues);
        }

        public Task<ProbeAttempt> ProbeAsync(BlockCheckTarget target, CancellationToken cancellationToken)
        {
            ProbeCalls++;
            return Task.FromResult(new ProbeAttempt { Success = true });
        }
    }
}
