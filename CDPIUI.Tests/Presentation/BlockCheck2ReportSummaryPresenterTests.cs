using CDPIUI.ViewModels.BlockCheck2;

namespace CDPIUI.Tests.Presentation;

public class BlockCheck2ReportSummaryPresenterTests
{
    [Fact]
    public void ConstructorDoesNotReadStateLocalizeOrShow()
    {
        var f = new Fixture(); Assert.Empty(f.Trace); Assert.Empty(f.Notices);
    }

    [Fact]
    public void MissingReportLocalizesEmptyTitleThenMessageWithoutReadingOtherFlags()
    {
        var f = new Fixture { HasReport = false, NoBypass = true, Success = true, Best = true, Http = true };
        f.Owner.Show();
        Assert.Equal(["has", "text:BlockCheck2ResultEmptyTitle", "text:BlockCheck2ResultEmptyMessage", "show"], f.Trace);
        Assert.Equal((BlockCheck2NoticeSeverity.Error, "t:BlockCheck2ResultEmptyTitle", "t:BlockCheck2ResultEmptyMessage"), Assert.Single(f.Notices));
    }

    [Theory]
    [InlineData(true, false, false, false, BlockCheck2NoticeSeverity.Informational, "BlockCheck2NoBypassRequiredTitle", "BlockCheck2NoBypassRequiredMessage")]
    [InlineData(true, true, true, true, BlockCheck2NoticeSeverity.Informational, "BlockCheck2NoBypassRequiredTitle", "BlockCheck2HttpOnlyNoBypassRequiredMessage")]
    [InlineData(false, true, true, true, BlockCheck2NoticeSeverity.Success, "BlockCheck2ResultSuccessTitle", "BlockCheck2ResultSuccessMessage")]
    [InlineData(false, false, true, true, BlockCheck2NoticeSeverity.Warning, "BlockCheck2ResultBestEffortTitle", "BlockCheck2ResultBestEffortMessage")]
    [InlineData(false, false, false, true, BlockCheck2NoticeSeverity.Warning, "BlockCheck2ResultPartialTitle", "BlockCheck2ResultPartialMessage")]
    public void PopulatedBranchesRetainPrecedenceAndLiteralResourceKeys(bool noBypass, bool success, bool best, bool http,
        BlockCheck2NoticeSeverity severity, string title, string message)
    {
        var f = new Fixture { NoBypass = noBypass, Success = success, Best = best, Http = http };
        f.Owner.Show();
        Assert.Equal((severity, "t:" + title, "t:" + message), Assert.Single(f.Notices));
        Assert.Equal(noBypass, f.Trace.Contains("http"));
        if (noBypass) { Assert.DoesNotContain("success", f.Trace); Assert.DoesNotContain("best", f.Trace); }
    }

    [Fact]
    public void SuccessReadsFlagsAgainForTitleAndMessageInOriginalOrder()
    {
        var f = new Fixture { Success = true }; f.Owner.Show();
        Assert.Equal(["has", "no-bypass", "success", "no-bypass", "success", "text:BlockCheck2ResultSuccessTitle",
            "no-bypass", "success", "text:BlockCheck2ResultSuccessMessage", "show"], f.Trace);
    }

    [Fact]
    public void NoBypassReadsHttpOnlyAfterLocalizingTitle()
    {
        var f = new Fixture { NoBypass = true }; f.Owner.Show();
        Assert.Equal(["has", "no-bypass", "no-bypass", "text:BlockCheck2NoBypassRequiredTitle",
            "no-bypass", "http", "text:BlockCheck2NoBypassRequiredMessage", "show"], f.Trace);
    }

    [Fact]
    public void TitleLocalizationCanChangeMessagePolicyWithoutRecomputingSeverityOrTitle()
    {
        var f = new Fixture();
        f.OnStep = s => { if (s == "text:BlockCheck2ResultPartialTitle") { f.NoBypass = true; f.Http = true; } };
        f.Owner.Show();
        Assert.Equal((BlockCheck2NoticeSeverity.Warning, "t:BlockCheck2ResultPartialTitle", "t:BlockCheck2HttpOnlyNoBypassRequiredMessage"), Assert.Single(f.Notices));
    }

    [Fact]
    public void BestEffortIsReadAgainAfterTitleLocalizationChangesOnlyThatFlag()
    {
        var f = new Fixture();
        f.OnStep = s => { if (s == "text:BlockCheck2ResultPartialTitle") f.Best = true; };
        f.Owner.Show();
        Assert.Equal((BlockCheck2NoticeSeverity.Warning, "t:BlockCheck2ResultPartialTitle", "t:BlockCheck2ResultBestEffortMessage"), Assert.Single(f.Notices));
        Assert.Equal(2, f.Trace.Count(s => s == "best"));
        Assert.True(f.Trace.IndexOf("best") < f.Trace.IndexOf("text:BlockCheck2ResultPartialTitle"));
        Assert.True(f.Trace.LastIndexOf("best") > f.Trace.IndexOf("text:BlockCheck2ResultPartialTitle"));
    }

    [Fact]
    public void EmptyBranchDoesNotRecheckReportAfterTitleLocalization()
    {
        var f = new Fixture { HasReport = false };
        f.OnStep = s => { if (s == "text:BlockCheck2ResultEmptyTitle") { f.HasReport = true; f.Success = true; } };
        f.Owner.Show();
        Assert.Equal(BlockCheck2NoticeSeverity.Error, Assert.Single(f.Notices).Severity);
        Assert.DoesNotContain("success", f.Trace); Assert.Equal(1, f.Trace.Count(s => s == "has"));
    }

    [Fact]
    public void ShowCallbackCanReenterAndNextCallReadsCurrentFlagsWithoutGuard()
    {
        var f = new Fixture { Success = true }; bool nested = false;
        f.OnStep = s => { if (s == "show" && !nested) { nested = true; f.HasReport = false; f.Owner.Show(); } };
        f.Owner.Show();
        Assert.Equal([BlockCheck2NoticeSeverity.Success, BlockCheck2NoticeSeverity.Error], f.Notices.Select(n => n.Severity));
        Assert.Equal(2, f.Trace.Count(s => s == "has"));
    }

    [Theory]
    [InlineData("has")]
    [InlineData("no-bypass")]
    [InlineData("success")]
    [InlineData("best")]
    [InlineData("text:BlockCheck2ResultPartialTitle")]
    [InlineData("text:BlockCheck2ResultPartialMessage")]
    public void ReadOrLocalizationFaultPropagatesSameExceptionWithoutShowingPartialNotice(string failing)
    {
        var f = new Fixture(); var error = new IOException(failing);
        f.OnStep = s => { if (s == failing) throw error; };
        Assert.Same(error, Assert.Throws<IOException>(() => f.Owner.Show()));
        Assert.Equal(failing, f.Trace.Last()); Assert.Empty(f.Notices);
    }

    [Fact]
    public void HttpFaultPropagatesAfterTitleWithoutShowing()
    {
        var f = new Fixture { NoBypass = true }; var error = new IOException("http");
        f.OnStep = s => { if (s == "http") throw error; };
        Assert.Same(error, Assert.Throws<IOException>(() => f.Owner.Show()));
        Assert.Contains("text:BlockCheck2NoBypassRequiredTitle", f.Trace); Assert.Empty(f.Notices);
    }

    [Fact]
    public void ShowFailureEscapesWithoutRetry()
    {
        var f = new Fixture(); var error = new IOException("show"); f.OnStep = s => { if (s == "show") throw error; };
        Assert.Same(error, Assert.Throws<IOException>(() => f.Owner.Show()));
        Assert.Single(f.Notices); Assert.Equal(1, f.Trace.Count(s => s == "show"));
    }

    private sealed class Fixture
    {
        public bool HasReport = true, NoBypass, Success, Best, Http;
        public readonly List<string> Trace = [];
        public readonly List<(BlockCheck2NoticeSeverity Severity, string Title, string Message)> Notices = [];
        public Action<string>? OnStep;
        public readonly BlockCheck2ReportSummaryPresenter Owner;
        public Fixture()
        {
            Owner = new(() => { Step("has"); return HasReport; }, () => { Step("no-bypass"); return NoBypass; },
                () => { Step("success"); return Success; }, () => { Step("best"); return Best; },
                () => { Step("http"); return Http; }, key => { Step("text:" + key); return "t:" + key; },
                (severity, title, message) => { Notices.Add((severity, title, message)); Step("show"); });
        }
        private void Step(string s) { Trace.Add(s); OnStep?.Invoke(s); }
    }
}
