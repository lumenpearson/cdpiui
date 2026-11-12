using System.Globalization;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.ViewModels;
using CDPIUI.ViewModels.BlockCheck2;

namespace CDPIUI.Tests.Presentation;

public class BlockCheck2ResultSelectionTests
{
    [Fact]
    public void ConstructorKeepsColdDefaultsWithoutReadingAnyPort()
    {
        var f = new Fixture();
        Assert.Empty(f.Trace);
        Assert.True(f.Owner.IsDetailsHitTestVisible && f.Owner.IsTestStrategyEnabled && f.Owner.IsManualTestEnabled);
        Assert.True(f.Owner.IsAddForSiteEnabled && f.Owner.IsAddForListEnabled);
        Assert.False(f.Owner.IsManualTestChecked);
        Assert.Equal("", f.Owner.ManualTestLabel); Assert.Equal("\uE768", f.Owner.ManualTestGlyph);
        Assert.Equal(["", "", "", "", "", "", ""], Details(f.Owner));
    }

    [Theory]
    [InlineData(false, false, true, false, "PresetTestConfirmButton", "\uE768")]
    [InlineData(false, true, true, true, "StopTest", "\uE769")]
    [InlineData(true, false, false, false, "PresetTestConfirmButton", "\uE768")]
    [InlineData(true, true, true, true, "StopTest", "\uE769")]
    public void SelectedSessionRetainsRunningAndManualActionPolicy(bool running, bool manual, bool manualEnabled,
        bool checkedValue, string label, string glyph)
    {
        var f = new Fixture { Item = Item("a"), Running = running, Manual = manual };
        f.Owner.Update();
        Assert.Equal(!running, f.Owner.IsDetailsHitTestVisible);
        Assert.Equal(!running, f.Owner.IsTestStrategyEnabled);
        Assert.Equal(manualEnabled, f.Owner.IsManualTestEnabled);
        Assert.Equal(checkedValue, f.Owner.IsManualTestChecked);
        Assert.Equal(label, f.Owner.ManualTestLabel); Assert.Equal(glyph, f.Owner.ManualTestGlyph);
        Assert.Equal(!running, f.Owner.IsAddForSiteEnabled); Assert.Equal(!running, f.Owner.IsAddForListEnabled);
        Assert.Same(f.Item, Assert.Single(f.ListItems));
        Assert.Equal("Strategy a", f.Owner.SelectedStrategyName); Assert.Equal("--a", f.Owner.SelectedStrategyArguments);
        Assert.Equal("https://a.test/", f.Owner.SelectedSite);
    }

    [Fact]
    public void ItemWithoutSessionStillShowsDetailsButSkipsRunningAndListPorts()
    {
        var f = new Fixture { Item = Item("a"), HasSession = false, Running = true, Manual = true };
        f.Owner.Update();
        Assert.False(f.Owner.IsDetailsHitTestVisible || f.Owner.IsTestStrategyEnabled || f.Owner.IsManualTestEnabled);
        Assert.False(f.Owner.IsAddForSiteEnabled || f.Owner.IsAddForListEnabled);
        Assert.Equal("a", f.Owner.SelectedStrategyId); Assert.Equal("--a", f.Owner.SelectedStrategyArguments);
        Assert.DoesNotContain("running", f.Trace); Assert.DoesNotContain("manual", f.Trace); Assert.Empty(f.ListItems);
    }

    [Fact]
    public void NullSelectionSkipsSessionAndResetsEveryDetailAfterMenu()
    {
        var f = new Fixture { Item = Item("a") }; f.Owner.Update(); f.ResetTrace(); f.Item = null;
        f.Owner.Update();
        Assert.Equal(["BlockCheck2SelectStrategyText", "", "", "", "", "", ""], Details(f.Owner));
        Assert.DoesNotContain("session", f.Trace);
        Assert.True(f.Trace.IndexOf("menu") < f.Trace.IndexOf("text:BlockCheck2SelectStrategyText"));
        Assert.False(f.Owner.IsManualTestChecked);
    }

    [Fact]
    public void UpdatePreservesLiteralPortAndNotificationOrder()
    {
        var f = new Fixture { Item = Item("a") }; f.Owner.Update();
        Assert.Equal(["selected", "session", "running", "running", "running", "manual",
            "text:PresetTestConfirmButton", "p:ManualTestLabel", "running", "list", "running", "menu",
            "p:SelectedStrategyName", "p:SelectedStrategyId", "p:SelectedStrategyArguments", "p:SelectedSite",
            "p:SelectedConnection", "p:SelectedEvidence", "text:BlockCheck2BaselineEvidenceFormat", "p:SelectedBaseline"], f.Trace);
    }

    [Fact]
    public void ListPredicateRunsEvenWhenStrategyTestIsRunningAndFalseSkipsFinalRunningRead()
    {
        var f = new Fixture { Item = Item("a"), Running = true, CanList = false };
        f.Owner.Update();
        Assert.Same(f.Item, Assert.Single(f.ListItems));
        Assert.Equal("p:IsAddForListEnabled", f.Trace[f.Trace.IndexOf("list") + 1]);
        Assert.Equal("menu", f.Trace[f.Trace.IndexOf("list") + 2]);
    }

    [Fact]
    public void NotificationCanChangeRunningBeforeLaterActionGuards()
    {
        var f = new Fixture { Item = Item("a") };
        f.OnStep = step => { if (step == "p:ManualTestLabel") f.Running = true; };
        f.Owner.Update();
        Assert.True(f.Owner.IsDetailsHitTestVisible && f.Owner.IsTestStrategyEnabled && f.Owner.IsManualTestEnabled);
        Assert.False(f.Owner.IsAddForSiteEnabled || f.Owner.IsAddForListEnabled);
    }

    [Fact]
    public void ManualStateIsRereadAfterEnabledNotification()
    {
        var f = new Fixture { Item = Item("a"), Running = true };
        f.OnStep = step => { if (step == "p:IsManualTestEnabled") f.Manual = true; };
        f.Owner.Update();
        Assert.False(f.Owner.IsManualTestEnabled); Assert.True(f.Owner.IsManualTestChecked);
        Assert.Equal("StopTest", f.Owner.ManualTestLabel); Assert.Equal("\uE769", f.Owner.ManualTestGlyph);
    }

    [Fact]
    public void MenuMutationDoesNotReplaceCapturedItemButNextUpdateReadsFreshItemAndSession()
    {
        var a = Item("a"); var b = Item("b"); var f = new Fixture { Item = a };
        f.OnStep = step => { if (step == "menu") { f.Item = b; f.HasSession = false; } };
        f.Owner.Update(); Assert.Equal("a", f.Owner.SelectedStrategyId); Assert.Same(a, Assert.Single(f.ListItems));
        f.OnStep = null; f.ResetTrace(); f.Owner.Update();
        Assert.Equal("b", f.Owner.SelectedStrategyId); Assert.False(f.Owner.IsTestStrategyEnabled); Assert.Empty(f.ListItems);
    }

    [Fact]
    public void MenuCanReenterAndOuterDetailsResumeWithOriginalItem()
    {
        var f = new Fixture { Item = Item("a") }; bool nested = false; List<string> ids = [];
        f.OnStep = step =>
        {
            if (step == "menu" && !nested) { nested = true; f.Item = Item("b"); f.Owner.Update(); }
            if (step == "p:SelectedStrategyId") ids.Add(f.Owner.SelectedStrategyId);
        };
        f.Owner.Update();
        Assert.Equal(["b", "a"], ids); Assert.Equal("a", f.Owner.SelectedStrategyId);
        Assert.Equal(2, f.Trace.Count(s => s == "menu"));
    }

    [Fact]
    public void EqualUpdateStillReadsTextAndNotifiesMenuButDoesNotPublishPropertyChanges()
    {
        var f = new Fixture { Item = Item("a") }; f.Owner.Update(); f.ResetTrace(); f.Owner.Update();
        Assert.DoesNotContain(f.Trace, s => s.StartsWith("p:", StringComparison.Ordinal));
        Assert.Equal(1, f.Trace.Count(s => s == "menu"));
        Assert.Contains("text:BlockCheck2BaselineEvidenceFormat", f.Trace);
    }

    [Fact]
    public void NullableTwoWayCheckUsesEqualityAndAcceptsNull()
    {
        var f = new Fixture(); f.Owner.IsManualTestChecked = null; f.Owner.IsManualTestChecked = null;
        Assert.Null(f.Owner.IsManualTestChecked); Assert.Equal(["p:IsManualTestChecked"], f.Trace);
        f.Owner.IsManualTestChecked = false;
        Assert.Equal(["p:IsManualTestChecked", "p:IsManualTestChecked"], f.Trace);
    }

    [Fact]
    public void LabelObserverCanResetNullableCheckBeforeGlyphIsChosen()
    {
        var f = new Fixture { Item = Item("a"), Manual = true };
        f.OnStep = step => { if (step == "p:ManualTestLabel") f.Owner.IsManualTestChecked = null; };
        f.Owner.Update();
        Assert.Equal("StopTest", f.Owner.ManualTestLabel);
        Assert.Null(f.Owner.IsManualTestChecked);
        Assert.Equal("\uE768", f.Owner.ManualTestGlyph);
        Assert.DoesNotContain("p:ManualTestGlyph", f.Trace);
    }

    [Theory]
    [InlineData("selected")]
    [InlineData("session")]
    [InlineData("running")]
    [InlineData("manual")]
    [InlineData("text:PresetTestConfirmButton")]
    [InlineData("list")]
    [InlineData("menu")]
    [InlineData("p:SelectedStrategyName")]
    [InlineData("p:SelectedStrategyId")]
    [InlineData("text:BlockCheck2BaselineEvidenceFormat")]
    public void PortOrObserverFailurePropagatesSameExceptionAndStopsAtExactStage(string failing)
    {
        var f = new Fixture { Item = Item("a") }; var error = new IOException(failing);
        f.OnStep = step => { if (step == failing) throw error; };
        Assert.Same(error, Assert.Throws<IOException>(() => f.Owner.Update()));
        Assert.Equal(failing, f.Trace.Last());
        Assert.Equal("", f.Owner.SelectedBaseline);
        if (failing == "p:SelectedStrategyId")
        { Assert.Equal("a", f.Owner.SelectedStrategyId); Assert.Equal("", f.Owner.SelectedStrategyArguments); }
    }

    [Fact]
    public void FormatFailureKeepsSixPublishedDetailsAndPreviousBaseline()
    {
        var f = new Fixture { Item = Item("a") }; f.Owner.Update(); string baseline = f.Owner.SelectedBaseline;
        f.Item = Item("b"); f.Format = "{3}";
        Assert.Throws<FormatException>(() => f.Owner.Update());
        Assert.Equal("b", f.Owner.SelectedStrategyId); Assert.Equal("--b", f.Owner.SelectedStrategyArguments);
        Assert.Equal(baseline, f.Owner.SelectedBaseline);
    }

    [Fact]
    public void NullBranchLocalizationFailureKeepsPreviousDetailsAfterActionFlagsChange()
    {
        var f = new Fixture { Item = Item("a") }; f.Owner.Update(); f.Item = null;
        var error = new IOException("choose"); f.OnStep = s => { if (s == "text:BlockCheck2SelectStrategyText") throw error; };
        Assert.Same(error, Assert.Throws<IOException>(() => f.Owner.Update()));
        Assert.False(f.Owner.IsTestStrategyEnabled); Assert.Equal("a", f.Owner.SelectedStrategyId);
    }

    [Fact]
    public void DetailFormattingUsesCurrentCultureAndRealEvidenceAtEachUpdate()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var f = new Fixture { Item = Item("a") }; f.Owner.Update(); string first = f.Owner.SelectedBaseline;
            Assert.Equal("base(" + f.Item.BaselineText + ")", first);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("sv-SE"); f.Owner.Update();
            Assert.Equal("base(" + f.Item.BaselineText + ")", f.Owner.SelectedBaseline);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    private static string[] Details(BlockCheck2ResultSelection s) => [s.SelectedStrategyName, s.SelectedStrategyId,
        s.SelectedStrategyArguments, s.SelectedSite, s.SelectedConnection, s.SelectedEvidence, s.SelectedBaseline];
    private static BlockCheck2StrategyEvidenceItem Item(string id) => new(new()
    {
        Strategy = new() { Id = id, DisplayName = "Strategy " + id }, StrategyArguments = "--" + id,
        Target = new() { Id = id, Host = id + ".test", Protocol = BlockCheckProtocol.Tls12, IpVersion = BlockCheckIpVersion.IPv4 },
    });

    private sealed class Fixture
    {
        public readonly List<string> Trace = [];
        public readonly List<BlockCheck2StrategyEvidenceItem> ListItems = [];
        public BlockCheck2StrategyEvidenceItem? Item;
        public bool HasSession = true, Running, Manual, CanList = true;
        public string Format = "base({0})";
        public Action<string>? OnStep;
        public readonly BlockCheck2ResultSelection Owner;
        public Fixture()
        {
            Owner = new(() => { Step("selected"); return Item; }, () => { Step("session"); return HasSession; },
                () => { Step("running"); return Running; }, () => { Step("manual"); return Manual; },
                item => { ListItems.Add(item); Step("list"); return CanList; },
                key => { Step("text:" + key); return key == "BlockCheck2BaselineEvidenceFormat" ? Format : key; },
                () => Step("menu"));
            Owner.PropertyChanged += (sender, e) => { Assert.Same(Owner, sender); Step("p:" + e.PropertyName); };
        }
        public void ResetTrace() { Trace.Clear(); ListItems.Clear(); }
        private void Step(string step) { Trace.Add(step); OnStep?.Invoke(step); }
    }
}
