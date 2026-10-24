using CDPIUI.AddOns.BlockCheck2.Catalog;
using CDPIUI.AddOns.BlockCheck2.Execution;
using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.Tests.AddOns;

public class BlockCheckManualStrategyTestServiceTests
{
    private static readonly BlockCheckTarget Target = new()
    {
        Id = "t1",
        Host = "example.com",
        Protocol = BlockCheckProtocol.Tls12,
        IpVersion = BlockCheckIpVersion.IPv4,
    };

    private static readonly StrategyDefinition Strategy =
        StrategyCatalogLoader.LoadBuiltIn().Strategies.First(strategy => strategy.AppliesTo(Target));

    private static readonly BlockCheckManualStrategyTestService Service = new();

    [Fact]
    public async Task StrategyIsStartedProbedStoppedAndRestored()
    {
        var log = new List<string>();
        var runner = new RecordingStrategyRunner(log);

        var result = await Service.TestAsync(
            Strategy, Target, runner, new RecordingProbeRunner(log), new BlockCheckManualStrategyTestOptions { Attempts = 2 });

        Assert.True(result.Success);
        Assert.Equal(["prepare", "start", "probe", "probe", "stop", "complete"], log);
        Assert.Equal(result.CommandLine, runner.Arguments);
        Assert.Equal(2, result.ProbeResult!.Attempts.Count);
        Assert.Equal(Strategy.Id, result.ProbeResult.StrategyId);
    }

    [Fact]
    public async Task FailedPrepareIsReportedAndOnlyTheRestoreRuns()
    {
        var log = new List<string>();
        var runner = new RecordingStrategyRunner(log) { FailAt = "prepare" };

        var result = await Service.TestAsync(Strategy, Target, runner, new RecordingProbeRunner(log));

        Assert.Equal(["prepare", "complete"], log);
        Assert.Contains(result.Issues, issue => issue.Code == "MANUAL_TEST_PREPARE_FAILED");
        Assert.Empty(result.ProbeResult!.Attempts);
    }

    [Fact]
    public async Task FailedStopIsReportedAndTheRestoreStillRuns()
    {
        var log = new List<string>();
        var runner = new RecordingStrategyRunner(log) { FailAt = "stop" };

        var result = await Service.TestAsync(
            Strategy, Target, runner, new RecordingProbeRunner(log), new BlockCheckManualStrategyTestOptions { Attempts = 1 });

        Assert.Equal(["prepare", "start", "probe", "stop", "complete"], log);
        Assert.Equal(["MANUAL_TEST_STOP_FAILED"], result.Issues.Select(issue => issue.Code));
        Assert.False(result.Success);
    }

    [Fact]
    public async Task CancellationIsRethrownAfterTheCleanup()
    {
        var log = new List<string>();
        using var cancellation = new CancellationTokenSource();
        var probeRunner = new RecordingProbeRunner(log) { OnProbe = cancellation.Cancel };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service.TestAsync(
            Strategy, Target, new RecordingStrategyRunner(log), probeRunner,
            new BlockCheckManualStrategyTestOptions { Attempts = 3 }, cancellation.Token));

        Assert.Equal(["prepare", "start", "probe", "stop", "complete"], log);
    }

    [Fact]
    public async Task InvalidInputStartsNothing()
    {
        var log = new List<string>();
        StrategyDefinition inapplicable = StrategyCatalogLoader.LoadBuiltIn().Strategies
            .First(strategy => !strategy.AppliesTo(Target));

        var result = await Service.TestAsync(
            inapplicable, Target, new RecordingStrategyRunner(log), new RecordingProbeRunner(log),
            new BlockCheckManualStrategyTestOptions { Attempts = 0 });

        Assert.Empty(log);
        Assert.Equal(
            ["MANUAL_TEST_ATTEMPTS_INVALID", "MANUAL_TEST_STRATEGY_INAPPLICABLE"],
            result.Issues.Select(issue => issue.Code));
        Assert.Null(result.ProbeResult);
    }

    [Fact]
    public void FullStrategyArgumentsMatchTheTestedCommandLine()
    {
        var arguments = Service.GetFullStrategyArguments(Strategy, Target);

        Assert.True(arguments.Success);
        Assert.Contains("example.com", arguments.Result);
    }

    private sealed class RecordingStrategyRunner(List<string> log) : IBlockCheckStrategyRunner
    {
        public string? FailAt { get; init; }
        public string? Arguments { get; private set; }

        public Task PrepareAsync(CancellationToken cancellationToken) => Record("prepare");

        public Task StartAsync(string arguments, CancellationToken cancellationToken)
        {
            Arguments = arguments;
            return Record("start");
        }

        public Task StopAsync(CancellationToken cancellationToken) => Record("stop");

        public Task CompleteAsync(CancellationToken cancellationToken) => Record("complete");

        private Task Record(string step)
        {
            log.Add(step);
            return step == FailAt ? Task.FromException(new InvalidOperationException(step)) : Task.CompletedTask;
        }
    }

    private sealed class RecordingProbeRunner(List<string> log) : IBlockCheckProbeRunner
    {
        public Action? OnProbe { get; init; }

        public Task<ProbeAttempt> ProbeAsync(BlockCheckTarget target, CancellationToken cancellationToken)
        {
            log.Add("probe");
            OnProbe?.Invoke();
            return Task.FromResult(new ProbeAttempt { Success = true, HttpStatusCode = 200 });
        }
    }
}
