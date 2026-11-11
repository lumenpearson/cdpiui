using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.ViewModels;

namespace CDPIUI.Tests.Presentation;

public class BlockCheck2ProgressTests
{
    private static readonly string[] UpdateNames =
    [
        "CurrentPhase", "ProgressCurrent", "ProgressTotal", "ProgressValue", "IsProgressIndeterminate",
        "CurrentTargetId", "CurrentTargetUrl", "CurrentTargetConnectionDetails", "CurrentTargetDisplay",
        "CurrentStrategyId", "EstimatedRemainingTime", "EstimatedOverallRemainingTime",
        "SuccessfulChecks", "FailedChecks", "CompletedChecks", "SuccessfulStrategies",
    ];

    [Fact]
    public void ConstructionDoesNotReadLiveProvidersOrStartTheEstimator()
    {
        var fixture = new ProgressFixture();

        Assert.Empty(fixture.Trace);
        Assert.Null(fixture.Model.CurrentPhase);
        Assert.True(fixture.Model.IsProgressIndeterminate);
        Assert.Equal(0, fixture.Model.ProgressValue);
        Assert.Equal("", fixture.Model.CurrentTargetId);
    }

    [Fact]
    public void UpdateAssignsFieldsBeforeReadingEstimatorInputsAndRaisesTheOriginalNotificationOrder()
    {
        var fixture = new ProgressFixture();
        fixture.Targets = [Target("target")];
        fixture.Attempts = 4;
        fixture.Grace = 1.5;
        fixture.Estimator.BeforeUpdate = () =>
        {
            Assert.Equal("target", fixture.Model.CurrentTargetId);
            Assert.Equal(3, fixture.Model.CompletedChecks);
            Assert.Empty(fixture.Changes);
        };

        fixture.Model.Update(Progress());

        Assert.Equal(["targets", "attempts", "grace", "estimate:1:4:1.5"], fixture.Trace);
        Assert.Equal(UpdateNames, fixture.Changes);
        Assert.Equal(BlockCheckScanPhase.Probing, fixture.Model.CurrentPhase);
        Assert.Equal(2, fixture.Model.ProgressCurrent);
        Assert.Equal(8, fixture.Model.ProgressTotal);
        Assert.Equal(.25, fixture.Model.ProgressValue);
        Assert.False(fixture.Model.IsProgressIndeterminate);
        Assert.Equal("strategy", fixture.Model.CurrentStrategyId);
        Assert.Equal(2, fixture.Model.SuccessfulChecks);
        Assert.Equal(1, fixture.Model.FailedChecks);
        Assert.Equal(1, fixture.Model.SuccessfulStrategies);
        Assert.Equal(TimeSpan.FromSeconds(10), fixture.Model.EstimatedRemainingTime);
        Assert.Equal(TimeSpan.FromSeconds(20), fixture.Model.EstimatedOverallRemainingTime);
    }

    [Fact]
    public void EveryNotificationObservesTheCompleteUpdatedSnapshot()
    {
        var fixture = new ProgressFixture();
        fixture.Model.PropertyChanged += (_, _) =>
        {
            Assert.Equal(2, fixture.Model.ProgressCurrent);
            Assert.Equal(1, fixture.Model.SuccessfulStrategies);
            Assert.Equal(TimeSpan.FromSeconds(20), fixture.Model.EstimatedOverallRemainingTime);
        };

        fixture.Model.Update(Progress());
    }

    [Theory]
    [InlineData(-2, 8, 0d, false)]
    [InlineData(15, 8, 1d, false)]
    [InlineData(2, 0, 0d, true)]
    [InlineData(2, -1, 0d, true)]
    public void DisplayedRatioClampsWithoutChangingRawCounts(int current, int total, double ratio, bool indeterminate)
    {
        var fixture = new ProgressFixture();

        fixture.Model.Update(Progress() with { CompletedStrategyTargets = current, TotalStrategyTargets = total });

        Assert.Equal(current, fixture.Model.ProgressCurrent);
        Assert.Equal(total, fixture.Model.ProgressTotal);
        Assert.Equal(ratio, fixture.Model.ProgressValue);
        Assert.Equal(indeterminate, fixture.Model.IsProgressIndeterminate);
    }

    [Fact]
    public void TargetDisplayUsesTheLiveTargetListAndCaseSensitiveIdentifiers()
    {
        var fixture = new ProgressFixture();
        fixture.Targets = [Target("TARGET", "wrong.test"), Target("target")];
        fixture.Model.Update(Progress());

        Assert.Equal("https://example.test/check", fixture.Model.CurrentTargetUrl);
        Assert.Equal("TLS 1.3 · IPv6 · TCP:443", fixture.Model.CurrentTargetConnectionDetails);
        Assert.Equal("https://example.test/check [TLS 1.3 · IPv6 · TCP:443]", fixture.Model.CurrentTargetDisplay);
        fixture.Targets = [];
        Assert.Equal("target", fixture.Model.CurrentTargetUrl);
        Assert.Equal("", fixture.Model.CurrentTargetConnectionDetails);
        Assert.Equal("target", fixture.Model.CurrentTargetDisplay);
    }

    [Fact]
    public void RepeatingTheSameProgressStillRaisesEveryNotificationAndReadsCurrentOptions()
    {
        var fixture = new ProgressFixture();
        fixture.Model.Update(Progress());
        fixture.Trace.Clear();
        fixture.Changes.Clear();
        fixture.Attempts = 9;
        fixture.Grace = 3;
        fixture.Targets = [Target("target"), Target("other")];

        fixture.Model.Update(Progress());

        Assert.Equal(UpdateNames, fixture.Changes);
        Assert.Equal(["targets", "attempts", "grace", "estimate:2:9:3"], fixture.Trace);
    }

    [Fact]
    public void AReentrantProgressUpdateBecomesTheCurrentDisplayedState()
    {
        var fixture = new ProgressFixture();
        bool updated = false;
        fixture.Model.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != "CurrentPhase" || updated) return;
            updated = true;
            fixture.Model.Update(Progress() with { CompletedStrategyTargets = 5, TargetId = "inner" });
        };

        fixture.Model.Update(Progress());

        Assert.Equal(5, fixture.Model.ProgressCurrent);
        Assert.Equal("inner", fixture.Model.CurrentTargetId);
        Assert.Equal(32, fixture.Changes.Count);
        Assert.Equal(2, fixture.Trace.Count(item => item.StartsWith("estimate:", StringComparison.Ordinal)));
    }

    [Fact]
    public void ResetClearsFieldsBeforeTheHostResultNotificationsAndThenNotifiesProgressInOriginalOrder()
    {
        var fixture = new ProgressFixture();
        fixture.Model.Update(Progress());
        fixture.Trace.Clear();
        fixture.Changes.Clear();

        fixture.Model.Reset(() =>
        {
            Assert.Empty(fixture.Changes);
            Assert.Null(fixture.Model.CurrentPhase);
            Assert.Equal(0, fixture.Model.CompletedChecks);
            Assert.Equal("", fixture.Model.CurrentTargetId);
            Assert.Null(fixture.Model.EstimatedRemainingTime);
            fixture.Changes.Add("host-results");
        });

        Assert.Equal(["reset-estimator"], fixture.Trace);
        Assert.Equal(new[] { "host-results", "CurrentPhase", "ProgressCurrent", "ProgressTotal", "ProgressValue", "IsProgressIndeterminate",
            "EstimatedRemainingTime", "EstimatedOverallRemainingTime", "SuccessfulChecks", "FailedChecks", "CompletedChecks",
            "SuccessfulStrategies", "CurrentTargetId", "CurrentTargetUrl", "CurrentTargetConnectionDetails", "CurrentTargetDisplay", "CurrentStrategyId" }, fixture.Changes);
    }

    [Fact]
    public void StartingOnlyStartsTheEstimatorAndFinishingOnlyClearsTimeEstimates()
    {
        var fixture = new ProgressFixture();
        fixture.Model.Update(Progress());
        fixture.Trace.Clear();
        fixture.Changes.Clear();

        fixture.Model.StartRun();
        Assert.Empty(fixture.Changes);
        fixture.Model.FinishRun();

        Assert.Equal(["start-estimator", "stop-estimator"], fixture.Trace);
        Assert.Equal(["EstimatedRemainingTime", "EstimatedOverallRemainingTime"], fixture.Changes);
        Assert.Null(fixture.Model.EstimatedRemainingTime);
        Assert.Null(fixture.Model.EstimatedOverallRemainingTime);
        Assert.Equal(2, fixture.Model.ProgressCurrent);
        Assert.Equal("target", fixture.Model.CurrentTargetId);
    }

    [Fact]
    public void CompletingOnlyChangesDisplayedProgressAndStillNotifiesWhenAlreadyComplete()
    {
        var fixture = new ProgressFixture();
        fixture.Model.Update(Progress());
        fixture.Changes.Clear();

        fixture.Model.CompleteDisplayedProgress();
        fixture.Model.CompleteDisplayedProgress();

        Assert.Equal(8, fixture.Model.ProgressCurrent);
        Assert.Equal(1, fixture.Model.ProgressValue);
        Assert.Equal(3, fixture.Model.CompletedChecks);
        Assert.Equal(["ProgressCurrent", "ProgressValue", "ProgressCurrent", "ProgressValue"], fixture.Changes);
    }

    [Fact]
    public void CompletingIndeterminateProgressDoesNotNotify()
    {
        var fixture = new ProgressFixture();

        fixture.Model.CompleteDisplayedProgress();

        Assert.Empty(fixture.Trace);
        Assert.Empty(fixture.Changes);
    }

    [Fact]
    public void AnEstimatorFailurePropagatesWithAssignedFieldsAndNoPartialNotifications()
    {
        var fixture = new ProgressFixture();
        fixture.Estimator.Error = new IOException("estimate failed");

        Assert.Throws<IOException>(() => fixture.Model.Update(Progress()));

        Assert.Equal("target", fixture.Model.CurrentTargetId);
        Assert.Equal(2, fixture.Model.ProgressCurrent);
        Assert.Null(fixture.Model.EstimatedRemainingTime);
        Assert.Empty(fixture.Changes);
    }

    [Fact]
    public void DefaultEstimatorRemainsConnectedToTheRealProductionImplementation()
    {
        var model = new BlockCheck2ProgressViewModel(() => [], () => 3, () => 1d);
        model.StartRun();

        model.Update(Progress() with { Phase = BlockCheckScanPhase.BaselineProbing, CompletedStrategyTargets = 8 });

        Assert.Equal(TimeSpan.Zero, model.EstimatedRemainingTime);
        Assert.Null(model.EstimatedOverallRemainingTime);
        model.FinishRun();
        Assert.Null(model.EstimatedRemainingTime);
    }

    private static BlockCheckScanProgress Progress() => new(BlockCheckScanPhase.Probing, 2, 8, "target", "strategy",
        SuccessfulChecks: 2, FailedChecks: 1, SuccessfulStrategies: 1);
    private static BlockCheckTarget Target(string id, string host = "example.test") => new()
    {
        Id = id, Host = host, Path = "check", Protocol = BlockCheckProtocol.Tls13, IpVersion = BlockCheckIpVersion.IPv6,
    };

    private sealed class ProgressFixture
    {
        public ProgressFixture()
        {
            Estimator = new(Trace);
            Model = new(() => { Trace.Add("targets"); return Targets; },
                () => { Trace.Add("attempts"); return Attempts; },
                () => { Trace.Add("grace"); return Grace; }, Estimator);
            Model.PropertyChanged += (_, args) => Changes.Add(args.PropertyName!);
        }
        public List<string> Trace { get; } = [];
        public List<string> Changes { get; } = [];
        public IReadOnlyList<BlockCheckTarget> Targets { get; set; } = [];
        public int Attempts { get; set; } = 3;
        public double Grace { get; set; } = 1;
        public EstimatorFake Estimator { get; }
        public BlockCheck2ProgressViewModel Model { get; }
    }

    private sealed class EstimatorFake(List<string> trace) : IBlockCheck2ProgressEstimator
    {
        public Action? BeforeUpdate { get; set; }
        public Exception? Error { get; set; }
        public void StartRun() => trace.Add("start-estimator");
        public void StopRun() => trace.Add("stop-estimator");
        public void ResetObservations() => trace.Add("reset-estimator");
        public (TimeSpan? Stage, TimeSpan? Overall) Update(BlockCheckScanProgress progress, int targetCount, int attempts, double grace)
        {
            trace.Add(FormattableString.Invariant($"estimate:{targetCount}:{attempts}:{grace}"));
            BeforeUpdate?.Invoke();
            if (Error != null) throw Error;
            return (TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20));
        }
    }
}
