using CDPIUI.ViewModels.BlockCheck2;

namespace CDPIUI.Tests.Presentation;

public class BlockCheck2WizardNavigationTests
{
    [Fact]
    public void ConstructorKeepsFourColdValuesWithoutInvokingDependencies()
    {
        var owner = new BlockCheck2WizardNavigation(null!, null!,
            _ => throw new InvalidOperationException("Early text read"),
            () => throw new InvalidOperationException("Early command notification"));

        Assert.Equal(0, (int)owner.CurrentStep);
        Assert.True(owner.IsNextEnabled);
        Assert.Null(owner.NextButtonText);
        Assert.True(owner.IsCancelEnabled);
    }

    [Theory]
    [InlineData(0, false, false, false, true, false, false, true, true, "next")]
    [InlineData(0, false, true, false, true, false, false, true, true, "next")]
    [InlineData(0, true, false, false, false, true, false, false, true, "next")]
    [InlineData(0, true, true, false, false, true, false, false, true, "next")]
    [InlineData(1, false, false, false, true, false, false, true, true, "next")]
    [InlineData(1, false, true, false, true, false, false, true, true, "next")]
    [InlineData(1, true, false, false, false, true, false, false, true, "next")]
    [InlineData(1, true, true, false, false, true, false, false, true, "next")]
    [InlineData(2, false, false, false, true, false, false, true, true, "next")]
    [InlineData(2, false, true, false, true, false, false, true, true, "next")]
    [InlineData(2, true, false, false, false, true, false, false, true, "next")]
    [InlineData(2, true, true, false, false, true, false, false, true, "next")]
    [InlineData(3, false, false, false, true, false, false, true, true, "start")]
    [InlineData(3, false, true, false, true, false, false, true, true, "start")]
    [InlineData(3, true, false, false, false, true, false, false, true, "start")]
    [InlineData(3, true, true, false, false, true, false, false, true, "start")]
    [InlineData(4, false, false, false, true, false, false, false, false, "next")]
    [InlineData(4, false, true, false, true, false, false, false, false, "next")]
    [InlineData(4, true, false, false, false, true, false, false, false, "next")]
    [InlineData(4, true, true, false, false, true, false, false, false, "next")]
    [InlineData(5, false, false, true, true, false, false, false, false, "next")]
    [InlineData(5, false, true, true, true, false, true, false, false, "next")]
    [InlineData(5, true, false, true, false, true, false, false, false, "next")]
    [InlineData(5, true, true, true, false, true, false, false, false, "next")]
    [InlineData(999, false, false, false, true, false, false, true, false, "next")]
    [InlineData(999, false, true, false, true, false, false, true, false, "next")]
    [InlineData(999, true, false, false, false, true, false, false, false, "next")]
    [InlineData(999, true, true, false, false, true, false, false, false, "next")]
    public void VisibilityAndEnabledValuesRetainTheOriginalTable(int step, bool running, bool report,
        bool exit, bool back, bool cancel, bool result, bool next, bool enabled, string text)
    {
        var f = new Fixture(); f.Owner.GoToStep((BlockCheck2WizardStep)step); f.ClearTrace();
        f.Source.Running = running; f.Source.Report = report; f.Owner.UpdateNavigationButtons();
        Assert.Equal(new BlockCheck2NavigationButtons(exit, back, cancel, result, next), Assert.Single(f.Buttons));
        Assert.Equal(enabled, f.Owner.IsNextEnabled); Assert.Equal(text, f.Owner.NextButtonText);
        Assert.False(f.Owner.IsCancelEnabled); Assert.Equal(1, f.Notifications);
    }

    [Theory]
    [InlineData(0)] [InlineData(3)] [InlineData(5)] [InlineData(999)]
    public void EveryGoAssignsBeforeTheViewEvenForRepeatedOrUnknownSteps(int value)
    {
        var f = new Fixture(); var step = (BlockCheck2WizardStep)value;
        f.OnStep = point => { if (point.StartsWith("view-step:", StringComparison.Ordinal)) Assert.Equal(step, f.Owner.CurrentStep); };
        f.Owner.GoToStep(step); f.Owner.GoToStep(step);
        Assert.Equal([step, step], f.Steps);
        Assert.DoesNotContain(f.Trace, p => p.StartsWith("changed:", StringComparison.Ordinal));
    }

    [Fact]
    public void NestedGoKeepsTheInnerStepAfterTheOuterViewReturns()
    {
        var f = new Fixture(); bool nested = false;
        f.OnStep = point => { if (point == "view-step:Review" && !nested) { nested = true; f.Owner.GoToStep(BlockCheck2WizardStep.Advanced); } };
        f.Owner.GoToStep(BlockCheck2WizardStep.Review);
        Assert.Equal(BlockCheck2WizardStep.Advanced, f.Owner.CurrentStep);
        Assert.Equal([BlockCheck2WizardStep.Review, BlockCheck2WizardStep.Advanced], f.Steps);
    }

    [Fact]
    public void GoFailureRetainsAssignedStepAndExceptionIdentity()
    {
        var f = new Fixture(); var failure = new Sentinel();
        f.OnStep = p => { if (p.StartsWith("view-step:", StringComparison.Ordinal)) throw failure; };
        Assert.Same(failure, Assert.Throws<Sentinel>(() => f.Owner.GoToStep((BlockCheck2WizardStep)999)));
        Assert.Equal((BlockCheck2WizardStep)999, f.Owner.CurrentStep);
        f.OnStep = null; f.Owner.GoToStep(BlockCheck2WizardStep.Mode);
        Assert.Equal(BlockCheck2WizardStep.Mode, f.Owner.CurrentStep);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CancelAlwaysAssignsAndNotifiesIncludingEqualValuesWithoutPropertyEvents(bool value)
    {
        var f = new Fixture();
        f.OnStep = p => { if (p.StartsWith("command:", StringComparison.Ordinal)) Assert.Equal(value, f.Owner.IsCancelEnabled); };
        f.Owner.SetCancelEnabled(value); f.Owner.SetCancelEnabled(value);
        Assert.Equal([value, value], f.CommandValues);
        Assert.Equal(2, f.Notifications);
        Assert.DoesNotContain(f.Trace, p => p.StartsWith("changed:", StringComparison.Ordinal));
    }

    [Fact]
    public void UpdatePublishesViewThenCurrentCancelThenNextAndLocalizedText()
    {
        var f = new Fixture(); f.Owner.GoToStep(BlockCheck2WizardStep.Review); f.ClearTrace(); f.Source.Start = false;
        f.Owner.UpdateNavigationButtons();
        Assert.Equal(["running", "view-buttons", "can-cancel", "command:False", "can-start",
            "changed:IsNextEnabled", "text:BlockCheck2StartButton", "changed:NextButtonText"], f.Trace);
    }

    [Fact]
    public void ViewCallbackKeepsEarlierVisibilityAndStepFlagsButLaterCancelIsCurrent()
    {
        var f = new Fixture(); f.Owner.GoToStep(BlockCheck2WizardStep.Review); f.ClearTrace();
        f.OnStep = p =>
        {
            if (p == "view-buttons")
            {
                f.Owner.GoToStep(BlockCheck2WizardStep.WorkComplete); f.Source.Running = true;
                f.Source.Cancel = true; f.Source.Start = false;
            }
        };
        f.Owner.UpdateNavigationButtons();
        Assert.Equal(new BlockCheck2NavigationButtons(false, true, false, false, true), Assert.Single(f.Buttons));
        Assert.Equal(BlockCheck2WizardStep.WorkComplete, f.Owner.CurrentStep);
        Assert.True(f.Owner.IsCancelEnabled); Assert.False(f.Owner.IsNextEnabled); Assert.Equal("start", f.Owner.NextButtonText);
        Assert.Contains("can-start", f.Trace);
        Assert.True(f.Trace.IndexOf("view-buttons") < f.Trace.IndexOf("can-cancel"));
    }

    [Fact]
    public void StepFlagsAreObservedAfterTheRunningGetter()
    {
        var f = new Fixture(); f.Source.Report = true;
        f.OnStep = p => { if (p == "running") f.Owner.GoToStep(BlockCheck2WizardStep.WorkComplete); };
        f.Owner.UpdateNavigationButtons();
        Assert.Equal(new BlockCheck2NavigationButtons(true, true, false, true, false), Assert.Single(f.Buttons));
        Assert.Equal(["running", "view-step:WorkComplete", "report"], f.Trace.Take(3));
    }

    [Fact]
    public void CommandCallbackChangesLaterInputsButDoesNotRecomputeAdvancedStepFlags()
    {
        var f = new Fixture(); f.Owner.GoToStep(BlockCheck2WizardStep.Advanced); f.ClearTrace(); f.Source.Valid = false;
        f.OnStep = p =>
        {
            if (p == "command:False")
            { f.Source.Valid = true; f.Source.Continue = true; f.Owner.GoToStep(BlockCheck2WizardStep.Review); f.Source.Start = false; }
        };
        f.Owner.UpdateNavigationButtons();
        Assert.True(f.Owner.IsNextEnabled); Assert.Equal("next", f.Owner.NextButtonText);
        Assert.Equal(BlockCheck2WizardStep.Review, f.Owner.CurrentStep);
        Assert.Contains("valid", f.Trace); Assert.Contains("continue", f.Trace); Assert.DoesNotContain("can-start", f.Trace);
    }

    [Fact]
    public void NextPropertyCallbackDoesNotChangeTheEarlierReviewTextDecision()
    {
        var f = new Fixture(); f.Owner.GoToStep(BlockCheck2WizardStep.Review); f.ClearTrace(); f.Source.Start = false;
        f.OnStep = p => { if (p == "changed:IsNextEnabled") f.Owner.GoToStep(BlockCheck2WizardStep.Mode); };
        f.Owner.UpdateNavigationButtons();
        Assert.Equal(BlockCheck2WizardStep.Mode, f.Owner.CurrentStep);
        Assert.False(f.Owner.IsNextEnabled); Assert.Equal("start", f.Owner.NextButtonText);
    }

    [Fact]
    public void NestedLocalizationRetainsInnerEnabledValueAndOuterText()
    {
        var f = new Fixture(); f.Owner.GoToStep(BlockCheck2WizardStep.Review); f.ClearTrace(); bool nested = false;
        f.OnStep = p =>
        {
            if (p == "text:BlockCheck2StartButton" && !nested)
            {
                nested = true; f.Owner.GoToStep(BlockCheck2WizardStep.SiteLists); f.Source.Targets = false;
                f.Owner.UpdateNavigationButtons();
            }
        };
        f.Owner.UpdateNavigationButtons();
        Assert.Equal(BlockCheck2WizardStep.SiteLists, f.Owner.CurrentStep);
        Assert.False(f.Owner.IsNextEnabled); Assert.Equal("start", f.Owner.NextButtonText);
        Assert.Equal(2, f.Notifications); Assert.Equal(2, f.Buttons.Count);
        Assert.Equal(2, f.Trace.Count(p => p == "changed:NextButtonText"));
    }

    [Fact]
    public void NestedViewRetainsTheOuterSitesDecisionAfterInnerReview()
    {
        var f = new Fixture(); f.Source.Targets = false; bool nested = false;
        f.OnStep = p =>
        {
            if (p == "view-buttons" && !nested)
            { nested = true; f.Owner.GoToStep(BlockCheck2WizardStep.Review); f.Owner.UpdateNavigationButtons(); }
        };
        f.Owner.UpdateNavigationButtons();
        Assert.Equal(BlockCheck2WizardStep.Review, f.Owner.CurrentStep);
        Assert.False(f.Owner.IsNextEnabled); Assert.Equal("next", f.Owner.NextButtonText);
        Assert.Equal(2, f.Notifications); Assert.Equal(2, f.Buttons.Count);
    }

    [Fact]
    public void EqualUpdatesStillCallViewCommandSourceAndTextButSuppressBoundPropertyEvents()
    {
        var f = new Fixture(); f.Source.Targets = false; f.Owner.UpdateNavigationButtons(); f.ClearTrace();
        f.Owner.UpdateNavigationButtons();
        Assert.Equal(["running", "view-buttons", "can-cancel", "command:False", "targets", "text:BlockCheck2NextButton"], f.Trace);
        Assert.Equal(1, f.Notifications);
    }

    [Theory]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)] [InlineData(3, false)]
    [InlineData(4, false)] [InlineData(5, true)] [InlineData(999, false)]
    public void ReportGetterStaysLazyWhenVisibilityDoesNotNeedIt(int step, bool running)
    {
        var f = new Fixture(); f.Owner.GoToStep((BlockCheck2WizardStep)step); f.Source.Running = running;
        f.OnStep = p => { if (p == "report") throw new Sentinel(); };
        f.Owner.UpdateNavigationButtons(); Assert.DoesNotContain("report", f.Trace);
    }

    [Theory]
    [InlineData(0, false, "targets")]
    [InlineData(1, false, "")]
    [InlineData(2, false, "valid")]
    [InlineData(2, true, "valid,continue")]
    [InlineData(3, false, "can-start")]
    [InlineData(4, false, "")]
    [InlineData(5, false, "")]
    [InlineData(999, false, "")]
    public void NextBranchesReadOnlyTheirOriginalInputs(int step, bool valid, string expected)
    {
        var f = new Fixture(); f.Owner.GoToStep((BlockCheck2WizardStep)step); f.ClearTrace(); f.Source.Valid = valid;
        f.Owner.UpdateNavigationButtons();
        string[] ports = ["targets", "valid", "continue", "can-start"];
        Assert.Equal(expected.Split(',', StringSplitOptions.RemoveEmptyEntries), f.Trace.Where(ports.Contains));
    }

    [Fact]
    public void AdvancedDoesNotAddATargetSourceGuardAndRunningDoesNotDirectlyDisableNextValue()
    {
        var f = new Fixture(); f.Owner.GoToStep(BlockCheck2WizardStep.Advanced); f.Source.Targets = false; f.Source.Running = true;
        f.Owner.UpdateNavigationButtons();
        Assert.True(f.Owner.IsNextEnabled); Assert.False(Assert.Single(f.Buttons).Next);
        Assert.DoesNotContain("targets", f.Trace);
    }

    [Fact]
    public void BoundSettersKeepCurrentFieldAndEqualityIncludingNullText()
    {
        var f = new Fixture(); f.Owner.NextButtonText = null; Assert.Empty(f.Trace);
        f.OnStep = p =>
        {
            if (p == "changed:IsNextEnabled") Assert.False(f.Owner.IsNextEnabled);
            if (p == "changed:NextButtonText") Assert.Equal("label", f.Owner.NextButtonText);
        };
        f.Owner.IsNextEnabled = false; f.Owner.IsNextEnabled = false;
        f.Owner.NextButtonText = "label"; f.Owner.NextButtonText = "label";
        Assert.Equal(["changed:IsNextEnabled", "changed:NextButtonText"], f.Trace);
    }

    [Theory]
    [InlineData("running", 0, 0, 0, true, true, null)]
    [InlineData("report", 5, 0, 0, true, true, null)]
    [InlineData("view-buttons", 0, 1, 0, true, true, null)]
    [InlineData("can-cancel", 0, 1, 0, true, true, null)]
    [InlineData("command:False", 0, 1, 1, false, true, null)]
    [InlineData("targets", 0, 1, 1, false, true, null)]
    [InlineData("valid", 2, 1, 1, false, true, null)]
    [InlineData("continue", 2, 1, 1, false, true, null)]
    [InlineData("can-start", 3, 1, 1, false, true, null)]
    [InlineData("changed:IsNextEnabled", 0, 1, 1, false, false, null)]
    [InlineData("text:BlockCheck2NextButton", 0, 1, 1, false, false, null)]
    [InlineData("changed:NextButtonText", 0, 1, 1, false, false, "next")]
    public void FailuresPreserveExceptionIdentityAndOriginalPartialEffects(string point, int step,
        int views, int notifications, bool cancel, bool next, string? text)
    {
        var f = new Fixture(); f.Owner.GoToStep((BlockCheck2WizardStep)step); f.ClearTrace();
        f.Source.Targets = false; f.Source.Start = false; var failure = new Sentinel();
        f.OnStep = p => { if (p == point) throw failure; };
        Assert.Same(failure, Assert.Throws<Sentinel>(() => f.Owner.UpdateNavigationButtons()));
        Assert.Equal(views, f.Buttons.Count); Assert.Equal(notifications, f.Notifications);
        Assert.Equal(cancel, f.Owner.IsCancelEnabled); Assert.Equal(next, f.Owner.IsNextEnabled);
        Assert.Equal(text, f.Owner.NextButtonText);
        f.OnStep = null; f.Owner.UpdateNavigationButtons(); Assert.NotNull(f.Owner.NextButtonText);
    }

    [Fact]
    public void OwnersHaveIndependentStateAndCallbacks()
    {
        var first = new Fixture(); var second = new Fixture(); first.Owner.GoToStep(BlockCheck2WizardStep.Review);
        first.Owner.SetCancelEnabled(false); first.Owner.IsNextEnabled = false; first.Owner.NextButtonText = "first";
        Assert.Equal(BlockCheck2WizardStep.SiteLists, second.Owner.CurrentStep);
        Assert.True(second.Owner.IsCancelEnabled); Assert.True(second.Owner.IsNextEnabled); Assert.Null(second.Owner.NextButtonText);
        Assert.Empty(second.Trace);
    }

    private sealed class Sentinel : Exception;
    private sealed class Fixture
    {
        internal readonly List<string> Trace = [];
        internal readonly List<BlockCheck2WizardStep> Steps = [];
        internal readonly List<BlockCheck2NavigationButtons> Buttons = [];
        internal readonly List<bool> CommandValues = [];
        internal Action<string>? OnStep;
        internal int Notifications;
        internal readonly Source Source;
        internal readonly BlockCheck2WizardNavigation Owner;
        internal Fixture()
        {
            Source = new Source(this); Owner = new BlockCheck2WizardNavigation(Source, new View(this), Text, Notify);
            Owner.PropertyChanged += (sender, args) => { Assert.Same(Owner, sender); Step("changed:" + args.PropertyName); };
        }
        internal void Step(string point) { Trace.Add(point); OnStep?.Invoke(point); }
        internal bool Read(string name, Func<bool> value) { Step(name); return value(); }
        internal void ClearTrace() { Trace.Clear(); Steps.Clear(); Buttons.Clear(); CommandValues.Clear(); Notifications = 0; }
        private string Text(string key)
        {
            Step("text:" + key);
            return key switch { "BlockCheck2StartButton" => "start", "BlockCheck2NextButton" => "next", _ => throw new InvalidOperationException("Unexpected navigation key: " + key) };
        }
        private void Notify() { Notifications++; CommandValues.Add(Owner.IsCancelEnabled); Step("command:" + Owner.IsCancelEnabled); }
    }
    private sealed class Source(Fixture f) : IBlockCheck2NavigationSource
    {
        internal bool Running, Cancel, Report;
        internal bool Targets = true, Valid = true, Continue = true, Start = true;
        public bool IsRunning => f.Read("running", () => Running);
        public bool CanCancel => f.Read("can-cancel", () => Cancel);
        public bool HasReport => f.Read("report", () => Report);
        public bool HasTargetSources => f.Read("targets", () => Targets);
        public bool AdvancedOptionsValid => f.Read("valid", () => Valid);
        public bool CanContinueFromTargets => f.Read("continue", () => Continue);
        public bool CanStart => f.Read("can-start", () => Start);
    }
    private sealed class View(Fixture f) : IBlockCheck2MainPageView
    {
        public void GoToStep(BlockCheck2WizardStep step) { f.Steps.Add(step); f.Step("view-step:" + step); }
        public void ShowNavigationButtons(BlockCheck2NavigationButtons buttons) { f.Buttons.Add(buttons); f.Step("view-buttons"); }
    }
}
