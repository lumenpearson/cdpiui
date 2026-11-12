using System.Globalization;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.ViewModels.BlockCheck2;

namespace CDPIUI.Tests.Presentation;

public class BlockCheck2ProgressPresentationTests
{
    [Fact]
    public void ConstructorKeepsSixColdValuesWithoutReadingAnyPort()
    {
        var f = new Fixture();
        Assert.Equal(string.Empty, f.Owner.ProgressPhaseText);
        Assert.Equal(string.Empty, f.Owner.ProgressCounterText);
        Assert.Equal(string.Empty, f.Owner.StageRemainingTimeText);
        Assert.Equal(string.Empty, f.Owner.OverallRemainingTimeText);
        Assert.False(f.Owner.IsAdditionalProgressInfoVisible);
        Assert.Null(f.Owner.ProgressViewMoreText);
        Assert.Empty(f.Trace);
    }

    [Theory]
    [InlineData(BlockCheckScanPhase.BaselineProbing, "BlockCheck2PhaseBaseline")]
    [InlineData(BlockCheckScanPhase.StartingStrategy, "BlockCheck2PhaseStartingStrategy")]
    [InlineData(BlockCheckScanPhase.Probing, "BlockCheck2PhaseProbing")]
    [InlineData(BlockCheckScanPhase.StrategyCompleted, "BlockCheck2PhaseStrategyCompleted")]
    [InlineData(BlockCheckScanPhase.ValidatingPreset, "BlockCheck2PhaseValidatingConfig")]
    [InlineData(BlockCheckScanPhase.RepairingPreset, "BlockCheck2PhaseRepairingConfig")]
    [InlineData(null, "BlockCheck2PhasePreparing")]
    [InlineData((BlockCheckScanPhase)999, "BlockCheck2PhasePreparing")]
    public void EveryPhaseAndFallbackUsesTheOriginalKey(BlockCheckScanPhase? phase, string key)
    {
        var f = new Fixture { Phase = phase };
        f.Owner.UpdateProgressText();
        Assert.Equal("t:" + key, f.Owner.ProgressPhaseText);
        Assert.Equal(2, f.Trace.Count(s => s == "phase"));
        Assert.Equal("phase", f.Trace[0]);
        Assert.Equal("text:" + key, f.Trace[1]);
    }

    [Fact]
    public void RunningUpdateRetainsPortAndPublicationOrder()
    {
        var f = new Fixture();
        f.Owner.UpdateProgressText();
        Assert.Equal(["phase", "text:BlockCheck2PhaseBaseline", "changed:ProgressPhaseText",
            "text:BlockCheck2ProgressCounterFormat", "current", "total", "changed:ProgressCounterText",
            "running", "stage-estimate", "format:BlockCheck2RemainingTimeCalculating", "changed:StageRemainingTimeText",
            "running", "overall-estimate", "phase", "format:BlockCheck2OverallTimeCalculating", "changed:OverallRemainingTimeText"], f.Trace);
    }

    [Fact]
    public void PhasePublicationCanReplaceCountsBeforeTheCounterReadsThem()
    {
        var f = new Fixture();
        f.OnStep = point =>
        {
            if (point == "changed:ProgressPhaseText") { f.Current = 7; f.Total = 9; }
        };
        f.Owner.UpdateProgressText();
        Assert.Equal("7/9", f.Owner.ProgressCounterText);
        Assert.True(f.Trace.IndexOf("changed:ProgressPhaseText") < f.Trace.IndexOf("current"));
    }

    [Fact]
    public void CounterLocalizationRunsBeforeBothNumberReads()
    {
        var f = new Fixture();
        f.OnStep = point =>
        {
            if (point == "text:BlockCheck2ProgressCounterFormat") { f.Current = 12; f.Total = 20; }
        };
        f.Owner.UpdateProgressText();
        Assert.Equal("12/20", f.Owner.ProgressCounterText);
    }

    [Fact]
    public void StringFormatUsesCultureAfterBothNumberGettersHaveRun()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var f = new Fixture { Current = 1234, Total = 5678, CounterFormat = "{0:N0}|{1:N0}" };
            f.OnStep = point => { if (point == "total") CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE"); };
            f.Owner.UpdateProgressText();
            Assert.Equal("1.234|5.678", f.Owner.ProgressCounterText);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{0")]
    public void InvalidCounterFormatStillReadsBothNumbersBeforeThrowing(string? format)
    {
        var f = new Fixture { CounterFormat = format };
        if (format is null) Assert.Throws<ArgumentNullException>(f.Owner.UpdateProgressText);
        else Assert.Throws<FormatException>(f.Owner.UpdateProgressText);
        Assert.Contains("current", f.Trace);
        Assert.Contains("total", f.Trace);
        Assert.NotEqual(string.Empty, f.Owner.ProgressPhaseText);
        Assert.Equal(string.Empty, f.Owner.ProgressCounterText);
        Assert.Equal(string.Empty, f.Owner.StageRemainingTimeText);
        Assert.DoesNotContain("running", f.Trace);
    }

    [Fact]
    public void StagePublicationCanChangeTerminalStateBeforeOverallFormatting()
    {
        var f = new Fixture();
        f.OnStep = point =>
        {
            if (point == "changed:StageRemainingTimeText")
            {
                f.Running = false; f.Phase = BlockCheckScanPhase.Probing; f.OverallEstimate = TimeSpan.FromHours(1);
            }
        };
        f.Owner.UpdateProgressText();
        Assert.Equal("BlockCheck2RemainingTimeCalculating:null", f.Owner.StageRemainingTimeText);
        Assert.Equal("—", f.Owner.OverallRemainingTimeText);
        Assert.DoesNotContain("overall-estimate", f.Trace);
    }

    [Fact]
    public void OverallEstimateIsReadBeforeThePhaseWhichChoosesItsKey()
    {
        var f = new Fixture();
        f.OnStep = point => { if (point == "overall-estimate") f.Phase = BlockCheckScanPhase.Probing; };
        Assert.Equal("BlockCheck2RemainingTimeCalculating:null", f.Owner.FormatOverallRemainingTime());
        Assert.Equal(["running", "overall-estimate", "phase", "format:BlockCheck2RemainingTimeCalculating"], f.Trace);
    }

    [Fact]
    public void StoppedWithNullPhaseFormatsOverallAfterTwoIndependentPhaseReads()
    {
        var f = new Fixture { Running = false, Phase = null };
        Assert.Equal("BlockCheck2RemainingTimeCalculating:null", f.Owner.FormatOverallRemainingTime());
        Assert.Equal(["running", "phase", "overall-estimate", "phase", "format:BlockCheck2RemainingTimeCalculating"], f.Trace);
    }

    [Fact]
    public void StoppedWithNullPhaseStillReadsAndFormatsStageEstimate()
    {
        var f = new Fixture { Running = false, Phase = null, StageEstimate = TimeSpan.FromSeconds(2) };
        Assert.Equal("BlockCheck2RemainingTimeCalculating:20000000", f.Owner.FormatRemainingTime());
        Assert.Equal(["running", "phase", "stage-estimate", "format:BlockCheck2RemainingTimeCalculating"], f.Trace);
    }

    [Theory]
    [InlineData(BlockCheckScanPhase.BaselineProbing)]
    [InlineData(BlockCheckScanPhase.Probing)]
    [InlineData((BlockCheckScanPhase)999)]
    public void StoppedWithAnyPhaseReturnsEmDashWithoutReadingEstimates(BlockCheckScanPhase phase)
    {
        var f = new Fixture { Running = false, Phase = phase };
        Assert.Equal("—", f.Owner.FormatRemainingTime());
        Assert.Equal("—", f.Owner.FormatOverallRemainingTime());
        Assert.Equal(["running", "phase", "running", "phase"], f.Trace);
    }

    [Fact]
    public void RepeatedEqualUpdateStillEvaluatesEveryExpressionButSuppressesChanges()
    {
        var f = new Fixture();
        f.Owner.UpdateProgressText(); f.Trace.Clear();
        f.Owner.UpdateProgressText();
        Assert.DoesNotContain(f.Trace, s => s.StartsWith("changed:", StringComparison.Ordinal));
        Assert.Equal(2, f.Trace.Count(s => s == "phase"));
        Assert.Equal(2, f.Trace.Count(s => s == "running"));
        Assert.Contains("current", f.Trace); Assert.Contains("total", f.Trace);
        Assert.Contains("stage-estimate", f.Trace); Assert.Contains("overall-estimate", f.Trace);
    }

    [Fact]
    public void SixWritableValuesRetainSenderAndPerPropertyEquality()
    {
        var f = new Fixture();
        void Assign()
        {
            f.Owner.ProgressPhaseText = "phase"; f.Owner.ProgressCounterText = "counter";
            f.Owner.StageRemainingTimeText = "stage"; f.Owner.OverallRemainingTimeText = "overall";
            f.Owner.IsAdditionalProgressInfoVisible = true; f.Owner.ProgressViewMoreText = "more";
        }
        Assign();
        Assert.Equal(["changed:ProgressPhaseText", "changed:ProgressCounterText", "changed:StageRemainingTimeText",
            "changed:OverallRemainingTimeText", "changed:IsAdditionalProgressInfoVisible", "changed:ProgressViewMoreText"], f.Trace);
        f.Trace.Clear(); Assign(); Assert.Empty(f.Trace);
    }

    [Fact]
    public void DisclosureTogglePublishesVisibilityBeforeCurrentViewLessText()
    {
        var f = new Fixture(); f.Owner.ToggleAdditionalProgressInfo();
        Assert.True(f.Owner.IsAdditionalProgressInfoVisible);
        Assert.Equal("t:ViewLess", f.Owner.ProgressViewMoreText);
        Assert.Equal(["changed:IsAdditionalProgressInfoVisible", "text:ViewLess", "changed:ProgressViewMoreText"], f.Trace);
    }

    [Fact]
    public void NestedDisclosureToggleMakesOuterLocalizationReadTheFinalVisibility()
    {
        var f = new Fixture(); bool nested = false;
        f.OnStep = point =>
        {
            if (point != "changed:IsAdditionalProgressInfoVisible" || nested) return;
            nested = true; f.Owner.ToggleAdditionalProgressInfo();
        };
        f.Owner.ToggleAdditionalProgressInfo();
        Assert.False(f.Owner.IsAdditionalProgressInfoVisible);
        Assert.Equal("t:ViewMore", f.Owner.ProgressViewMoreText);
        Assert.Equal(2, f.Trace.Count(s => s == "changed:IsAdditionalProgressInfoVisible"));
        Assert.Equal(2, f.Trace.Count(s => s == "text:ViewMore"));
        Assert.Equal(1, f.Trace.Count(s => s == "changed:ProgressViewMoreText"));
        Assert.DoesNotContain("text:ViewLess", f.Trace);
    }

    [Fact]
    public void DisclosureObserverFailureKeepsChangedVisibilityAndUnreachedText()
    {
        var f = new Fixture(); var error = new Sentinel();
        f.OnStep = point => { if (point == "changed:IsAdditionalProgressInfoVisible") throw error; };
        Assert.Same(error, Assert.Throws<Sentinel>(f.Owner.ToggleAdditionalProgressInfo));
        Assert.True(f.Owner.IsAdditionalProgressInfoVisible); Assert.Null(f.Owner.ProgressViewMoreText);
        Assert.DoesNotContain("text:ViewLess", f.Trace);
        f.OnStep = null; f.Owner.ToggleAdditionalProgressInfo();
        Assert.False(f.Owner.IsAdditionalProgressInfoVisible); Assert.Equal("t:ViewMore", f.Owner.ProgressViewMoreText);
    }

    [Fact]
    public void InstancesOwnDistinctDisplayState()
    {
        var first = new Fixture(); var second = new Fixture();
        first.Owner.UpdateProgressText(); first.Owner.ToggleAdditionalProgressInfo();
        Assert.Equal(string.Empty, second.Owner.ProgressPhaseText);
        Assert.Null(second.Owner.ProgressViewMoreText); Assert.Empty(second.Trace);
    }

    [Theory]
    [InlineData("phase")]
    [InlineData("text:BlockCheck2PhaseBaseline")]
    [InlineData("text:BlockCheck2ProgressCounterFormat")]
    [InlineData("current")]
    [InlineData("total")]
    [InlineData("running")]
    [InlineData("stage-estimate")]
    [InlineData("format:BlockCheck2RemainingTimeCalculating")]
    [InlineData("overall-estimate")]
    [InlineData("format:BlockCheck2OverallTimeCalculating")]
    public void PortFailuresRetainIdentityWithoutAStickyGuard(string point)
    {
        var f = new Fixture(); var error = new Sentinel();
        f.OnStep = step => { if (step == point) throw error; };
        Assert.Same(error, Assert.Throws<Sentinel>(f.Owner.UpdateProgressText));
        f.OnStep = null; f.Owner.UpdateProgressText();
        Assert.NotEqual(string.Empty, f.Owner.OverallRemainingTimeText);
    }

    [Theory]
    [InlineData("ProgressPhaseText", 1)]
    [InlineData("ProgressCounterText", 2)]
    [InlineData("StageRemainingTimeText", 3)]
    [InlineData("OverallRemainingTimeText", 4)]
    public void ObserverFailureRetainsPublishedFieldsAndStopsLaterExpressions(string property, int published)
    {
        var f = new Fixture(); var error = new Sentinel();
        f.OnStep = point => { if (point == "changed:" + property) throw error; };
        Assert.Same(error, Assert.Throws<Sentinel>(f.Owner.UpdateProgressText));
        string[] values = [f.Owner.ProgressPhaseText, f.Owner.ProgressCounterText, f.Owner.StageRemainingTimeText, f.Owner.OverallRemainingTimeText];
        Assert.All(values.Take(published), value => Assert.NotEqual(string.Empty, value));
        Assert.All(values.Skip(published), value => Assert.Equal(string.Empty, value));
        f.OnStep = null; f.Owner.UpdateProgressText();
        Assert.NotEqual(string.Empty, f.Owner.OverallRemainingTimeText);
    }

    private sealed class Sentinel : Exception;

    private sealed class Fixture
    {
        public BlockCheckScanPhase? Phase = BlockCheckScanPhase.BaselineProbing;
        public bool Running = true;
        public int Current, Total;
        public TimeSpan? StageEstimate, OverallEstimate;
        public string? CounterFormat = "{0}/{1}";
        public Action<string>? OnStep;
        public List<string> Trace { get; } = [];
        public BlockCheck2ProgressPresentation Owner { get; }

        public Fixture()
        {
            Owner = new(() => Read("phase", () => Phase), () => Read("running", () => Running),
                () => Read("current", () => Current), () => Read("total", () => Total),
                () => Read("stage-estimate", () => StageEstimate), () => Read("overall-estimate", () => OverallEstimate),
                key => Read("text:" + key, () => key == "BlockCheck2ProgressCounterFormat" ? CounterFormat! : "t:" + key),
                (value, key) => Read("format:" + key, () => key + ":" + (value?.Ticks.ToString(CultureInfo.InvariantCulture) ?? "null")));
            Owner.PropertyChanged += (sender, args) =>
            {
                Assert.Same(Owner, sender);
                Step("changed:" + args.PropertyName);
            };
        }

        private void Step(string point) { Trace.Add(point); OnStep?.Invoke(point); }
        private T Read<T>(string point, Func<T> value) { Step(point); return value(); }
    }
}
