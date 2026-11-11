using CDPIUI.ViewModels.CreateConfig;

namespace CDPIUI.Tests.Presentation;

public sealed class CreateConfigComponentSelectionTests
{
    private static ConfigComponentOption Option(string? id = "supported") => new() { Id = id!, Name = "Option" };

    [Fact]
    public void ConstructorOnlyStoresCallbacksAndPreservesInitialFields()
    {
        var f = new Fixture();
        Assert.Null(f.State.SelectedComponent); Assert.Empty(f.State.DisplayName);
        Assert.False(f.State.IsAdditionalSettingsVisible);
        Assert.True(f.State.IsNotReadyErrorVisible); Assert.True(f.State.IsTabBarVisible);
        Assert.Empty(f.Trace); Assert.Empty(f.Events);
    }

    [Fact]
    public void SupportedSelectionNotifiesBeforeViewEffectsDirtyAndAudit()
    {
        var f = new Fixture();
        var selected = Option();
        f.State.SelectedComponent = selected;
        Assert.Same(selected, f.State.SelectedComponent);
        Assert.Equal(SupportedTrace, f.Trace);
        Assert.True(f.State.IsAdditionalSettingsVisible);
        Assert.False(f.State.IsNotReadyErrorVisible); Assert.True(f.State.IsTabBarVisible);
    }

    [Fact]
    public void UnsupportedSelectionHandlesDefaultViewBeforeSelectingTextAndHidingTabbar()
    {
        var f = new Fixture();
        f.State.SelectedComponent = Option("unsupported");
        Assert.Equal(["event:SelectedComponent", "event:IsAdditionalSettingsVisible", "event:IsNotReadyErrorVisible",
            "supported:unsupported", "selection:True", "text-tab", "event:IsTabBarVisible", "dirty", "audit"], f.Trace);
        Assert.False(f.State.IsTabBarVisible);
    }

    [Fact]
    public void SupportedTabbarUpdateOccursBetweenTabSelectionAndSelectionHandling()
    {
        var f = new Fixture();
        f.State.SelectedComponent = Option("unsupported"); f.Trace.Clear();
        f.State.SelectedComponent = Option();
        Assert.Equal(["event:SelectedComponent", "supported:supported", "designer-tab",
            "event:IsTabBarVisible", "selection:False", "dirty", "audit"], f.Trace);
    }

    [Fact]
    public void EqualReferenceSuppressesEverythingButEqualDataInAnotherObjectDoesNot()
    {
        var f = new Fixture();
        var option = Option();
        f.State.SelectedComponent = option; f.Trace.Clear();
        f.State.SelectedComponent = option;
        Assert.Empty(f.Trace);
        f.State.SelectedComponent = Option();
        Assert.Equal(["event:SelectedComponent", "supported:supported", "designer-tab", "selection:False", "dirty", "audit"], f.Trace);
    }

    [Fact]
    public void NullSelectionResetsOnlyAdditionalAndErrorFlagsThenStillMarksDirtyAndAudits()
    {
        var f = new Fixture();
        f.State.SelectedComponent = Option("unsupported"); f.Trace.Clear();
        f.State.SelectedComponent = null;
        Assert.Equal(["event:SelectedComponent", "event:IsAdditionalSettingsVisible", "event:IsNotReadyErrorVisible", "dirty", "audit"], f.Trace);
        Assert.False(f.State.IsAdditionalSettingsVisible); Assert.True(f.State.IsNotReadyErrorVisible);
        Assert.False(f.State.IsTabBarVisible);
    }

    [Fact]
    public void EqualInitialNullSelectionDoesNotProduceEffects()
    {
        var f = new Fixture();
        f.State.SelectedComponent = null;
        Assert.Empty(f.Trace);
    }

    [Fact]
    public void DisplayNameNormalizesNullAndSuppressesEqualAssignments()
    {
        var f = new Fixture();
        f.State.DisplayName = null!; Assert.Empty(f.Trace);
        f.State.DisplayName = "name"; f.State.DisplayName = "name"; f.State.DisplayName = null!;
        Assert.Empty(f.State.DisplayName);
        Assert.Equal(["event:DisplayName", "dirty", "audit", "event:DisplayName", "dirty", "audit"], f.Trace);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("SUPPORTED")]
    public void DesignerPredicateReceivesCurrentNormalizedIdWithoutCaseFolding(string? id)
    {
        var f = new Fixture();
        f.State.SelectedComponent = Option(id); f.Trace.Clear();
        Assert.False(f.State.IsDesignerSupported);
        Assert.Equal(["supported:" + (id ?? "")], f.Trace);
    }

    [Fact]
    public void SupportQueryOnAbsentComponentStillCallsTheOriginalEmptyIdPredicate()
    {
        var f = new Fixture();
        Assert.False(f.State.IsDesignerSupported);
        Assert.Equal(["supported:"], f.Trace);
    }

    [Fact]
    public void SupportQueryUsesLivePredicateRatherThanAConstructorSnapshot()
    {
        var f = new Fixture();
        f.State.SelectedComponent = Option();
        f.Supported.Clear();
        Assert.False(f.State.IsDesignerSupported);
    }

    [Fact]
    public void SaveCapabilityShortCircuitsBeforeEditorRead()
    {
        var f = new Fixture();
        f.State.DisplayName = "name"; f.Trace.Clear();
        Assert.False(f.State.IsSaveAvailable); Assert.Empty(f.Trace);
        f.State.DisplayName = "";
        f.State.SelectedComponent = Option(); f.Trace.Clear();
        Assert.False(f.State.IsSaveAvailable); Assert.Empty(f.Trace);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(" ", true)]
    [InlineData("--args", true)]
    public void SaveCapabilityReadsCurrentEditorAndDoesNotTrim(string? editor, bool expected)
    {
        var f = new Fixture { Editor = editor };
        f.State.SelectedComponent = Option(); f.State.DisplayName = " "; f.Trace.Clear();
        Assert.Equal(expected, f.State.IsSaveAvailable);
        Assert.Equal(["editor"], f.Trace);
    }

    [Fact]
    public void NullComponentIdStillAllowsSaveWhenTheObjectNameAndTextExist()
    {
        var f = new Fixture();
        f.State.SelectedComponent = Option(null); f.State.DisplayName = "name";
        Assert.True(f.State.IsSaveAvailable);
    }

    [Fact]
    public void SaveQueryDoesNotRevalidateEarlierFieldsAfterEditorCallbackReentry()
    {
        var f = new Fixture();
        f.State.SelectedComponent = Option(); f.State.DisplayName = "name";
        f.ReadEditor = () => { f.State.SelectedComponent = null; f.State.DisplayName = ""; return "text"; };
        Assert.True(f.State.IsSaveAvailable);
        Assert.Null(f.State.SelectedComponent); Assert.Empty(f.State.DisplayName);
    }

    [Fact]
    public void EditorQueryFailurePropagatesWithoutNewMutationOrDirtyEffects()
    {
        var f = new Fixture();
        var selected = Option();
        f.State.SelectedComponent = selected; f.State.DisplayName = "name"; f.Trace.Clear();
        f.ReadEditor = () => throw new IOException("editor");
        Assert.Throws<IOException>(() => _ = f.State.IsSaveAvailable);
        Assert.Equal(["editor"], f.Trace); Assert.Same(selected, f.State.SelectedComponent);
    }

    [Fact]
    public void ExplicitCheckViewTypeDoesNotMarkDirtyOrAudit()
    {
        var f = new Fixture();
        f.State.CheckViewType(); Assert.Empty(f.Trace);
        f.State.SelectedComponent = Option(); f.Trace.Clear();
        f.State.CheckViewType();
        Assert.Equal(["supported:supported", "designer-tab", "selection:False"], f.Trace);
    }

    [Fact]
    public void FlagSettersOnlyNotifyAndSuppressEqualValues()
    {
        var f = new Fixture();
        f.State.IsTabBarVisible = false; f.State.IsTabBarVisible = false;
        f.State.IsAdditionalSettingsVisible = true; f.State.IsNotReadyErrorVisible = false;
        Assert.Equal(["event:IsTabBarVisible", "event:IsAdditionalSettingsVisible", "event:IsNotReadyErrorVisible"], f.Trace);
    }

    [Fact]
    public void SelectedObserverFailureLeavesAssignmentBeforeAnyViewTypeEffects()
    {
        var f = new Fixture();
        var selected = Option();
        f.Observer = name => { if (name == "SelectedComponent") throw new IOException("selected"); };
        Assert.Throws<IOException>(() => f.State.SelectedComponent = selected);
        Assert.Same(selected, f.State.SelectedComponent);
        Assert.False(f.State.IsAdditionalSettingsVisible); Assert.True(f.State.IsNotReadyErrorVisible);
        Assert.Equal(["event:SelectedComponent"], f.Trace);
    }

    [Fact]
    public void FlagObserverFailureLeavesTheOriginalPartialCheckViewTypeState()
    {
        var f = new Fixture();
        f.Observer = name => { if (name == "IsAdditionalSettingsVisible") throw new IOException("flag"); };
        Assert.Throws<IOException>(() => f.State.SelectedComponent = Option());
        Assert.True(f.State.IsAdditionalSettingsVisible); Assert.True(f.State.IsNotReadyErrorVisible);
        Assert.Equal(["event:SelectedComponent", "event:IsAdditionalSettingsVisible"], f.Trace);
    }

    [Theory]
    [InlineData("supported:supported")]
    [InlineData("designer-tab")]
    [InlineData("selection:False")]
    [InlineData("dirty")]
    [InlineData("audit")]
    public void CallbackFailuresPreserveTheExpectedPrefixAndPartialState(string failingStep)
    {
        var f = new Fixture();
        f.OnStep = step => { if (step == failingStep) throw new IOException(step); };
        Assert.Throws<IOException>(() => f.State.SelectedComponent = Option());
        Assert.Equal(SupportedTrace.Take(Array.IndexOf(SupportedTrace, failingStep) + 1), f.Trace);
        Assert.True(f.State.IsAdditionalSettingsVisible); Assert.False(f.State.IsNotReadyErrorVisible);
        Assert.NotNull(f.State.SelectedComponent);
    }

    [Fact]
    public void DisplayObserverFailureDoesNotReachDirtyOrAudit()
    {
        var f = new Fixture();
        f.Observer = _ => throw new IOException("display");
        Assert.Throws<IOException>(() => f.State.DisplayName = "assigned");
        Assert.Equal("assigned", f.State.DisplayName);
        Assert.Equal(["event:DisplayName"], f.Trace);
    }

    [Fact]
    public void DisplayDirtyFailureKeepsAssignedNameAndSkipsAudit()
    {
        var f = new Fixture();
        f.OnStep = step => { if (step == "dirty") throw new IOException("dirty"); };
        Assert.Throws<IOException>(() => f.State.DisplayName = "assigned");
        Assert.Equal("assigned", f.State.DisplayName);
        Assert.Equal(["event:DisplayName", "dirty"], f.Trace);
    }

    [Fact]
    public void ReentrantNullSelectionDuringFlagNotifyDoesNotStopOuterContinuation()
    {
        var f = new Fixture();
        bool entered = false;
        f.Observer = name =>
        {
            if (name == "IsAdditionalSettingsVisible" && !entered)
            {
                entered = true; f.State.SelectedComponent = null;
            }
        };
        f.State.SelectedComponent = Option();
        Assert.Null(f.State.SelectedComponent);
        Assert.False(f.State.IsAdditionalSettingsVisible);
        Assert.False(f.State.IsNotReadyErrorVisible);
        Assert.False(f.State.IsTabBarVisible);
        Assert.Contains("supported:", f.Trace);
        Assert.Equal(["dirty", "audit"], f.Trace.TakeLast(2));
    }

    [Fact]
    public void ReentrantUnsupportedSelectionDuringDesignerTabKeepsOuterSupportedContinuation()
    {
        var f = new Fixture();
        bool entered = false;
        f.OnStep = step =>
        {
            if (step == "designer-tab" && !entered)
            {
                entered = true; f.State.SelectedComponent = Option("unsupported");
            }
        };
        f.State.SelectedComponent = Option();
        Assert.Equal("unsupported", f.State.SelectedComponent!.Id);
        Assert.True(f.State.IsTabBarVisible);
        Assert.Equal(["event:IsTabBarVisible", "selection:False", "dirty", "audit"], f.Trace.TakeLast(4));
    }

    private static readonly string[] SupportedTrace = ["event:SelectedComponent", "event:IsAdditionalSettingsVisible",
        "event:IsNotReadyErrorVisible", "supported:supported", "designer-tab", "selection:False", "dirty", "audit"];

    private sealed class Fixture
    {
        public Fixture()
        {
            State = new(id => { Step("supported:" + id); return Supported.Contains(id); },
                () => Step("designer-tab"), () => Step("text-tab"), value => Step("selection:" + value),
                () => Step("dirty"), () => Step("audit"),
                () => { Step("editor"); return ReadEditor is null ? Editor : ReadEditor(); });
            State.PropertyChanged += (sender, e) =>
            {
                Assert.Same(State, sender);
                Events.Add(e.PropertyName!); Trace.Add("event:" + e.PropertyName);
                Observer?.Invoke(e.PropertyName!);
            };
        }
        public CreateConfigComponentSelection State { get; }
        public string? Editor { get; init; } = "text";
        public HashSet<string> Supported { get; } = new(StringComparer.Ordinal) { "supported" };
        public List<string> Trace { get; } = [];
        public List<string> Events { get; } = [];
        public Action<string>? OnStep { get; set; }
        public Action<string>? Observer { get; set; }
        public Func<string?>? ReadEditor { get; set; }
        private void Step(string step) { Trace.Add(step); OnStep?.Invoke(step); }
    }
}
