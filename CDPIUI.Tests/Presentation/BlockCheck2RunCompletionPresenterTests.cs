using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.ViewModels.BlockCheck2;

namespace CDPIUI.Tests.Presentation;

public class BlockCheck2RunCompletionPresenterTests
{
    [Fact]
    public void ConstructorDoesNotReadLocalizeFormatOrPublish()
    {
        var f = new Fixture();
        Assert.Empty(f.Trace);
        Assert.Empty(f.Notices);
    }

    [Theory]
    [InlineData(BlockCheckSessionState.Idle, BlockCheck2NoticeSeverity.Error, "BlockCheck2FailedTitle", "issues", 4)]
    [InlineData(BlockCheckSessionState.InputInvalid, BlockCheck2NoticeSeverity.Error, "BlockCheck2FailedTitle", "issues", 4)]
    [InlineData(BlockCheckSessionState.Running, BlockCheck2NoticeSeverity.Error, "BlockCheck2FailedTitle", "issues", 4)]
    [InlineData(BlockCheckSessionState.Canceling, BlockCheck2NoticeSeverity.Error, "BlockCheck2FailedTitle", "issues", 4)]
    [InlineData(BlockCheckSessionState.Completed, BlockCheck2NoticeSeverity.Success, "BlockCheck2CompletedTitle", "t:BlockCheck2CompletedMessage", 2)]
    [InlineData(BlockCheckSessionState.CompletedWithWarnings, BlockCheck2NoticeSeverity.Warning, "BlockCheck2BestEffortCompletedTitle", "t:BlockCheck2BestEffortCompletedMessage", 3)]
    [InlineData(BlockCheckSessionState.Failed, BlockCheck2NoticeSeverity.Error, "BlockCheck2FailedTitle", "issues", 4)]
    [InlineData(BlockCheckSessionState.Canceled, BlockCheck2NoticeSeverity.Informational, "BlockCheck2CanceledTitle", "t:BlockCheck2CanceledMessage", 4)]
    public void EveryStateRetainsSeverityKeysAndRepeatedStateReads(BlockCheckSessionState state,
        BlockCheck2NoticeSeverity severity, string title, string message, int stateReads)
    {
        var f = new Fixture { State = state };
        f.Owner.Show();
        Assert.Equal((severity, "t:" + title, message), Assert.Single(f.Notices));
        Assert.Equal(stateReads, f.Trace.Count(s => s == "state"));
        Assert.Equal(state == BlockCheckSessionState.Completed ? 1 : 0, f.Trace.Count(s => s == "no-bypass"));
        Assert.DoesNotContain("http", f.Trace);
    }

    [Theory]
    [InlineData(false, "BlockCheck2NoBypassRequiredMessage")]
    [InlineData(true, "BlockCheck2HttpOnlyNoBypassRequiredMessage")]
    public void CompletedNoBypassHasPriorityAndReadsHttpAfterTitle(bool httpOnly, string key)
    {
        var f = new Fixture { State = BlockCheckSessionState.Completed, NoBypass = true, HttpOnly = httpOnly };
        f.Owner.Show();
        Assert.Equal((BlockCheck2NoticeSeverity.Informational, "t:BlockCheck2NoBypassRequiredTitle", "t:" + key), Assert.Single(f.Notices));
        Assert.Equal(["state", "no-bypass", "text:BlockCheck2NoBypassRequiredTitle", "http", "text:" + key,
            "history-error", "publish"], f.Trace);
    }

    [Fact]
    public void CompletedReadOrderDoesNotTouchFailureOrHttpPorts()
    {
        var f = new Fixture { State = BlockCheckSessionState.Completed };
        f.Owner.Show();
        Assert.Equal(["state", "no-bypass", "state", "text:BlockCheck2CompletedTitle",
            "text:BlockCheck2CompletedMessage", "history-error", "publish"], f.Trace);
    }

    [Fact]
    public void AStateChangeBetweenChecksUsesTheLaterStateWithoutBacktracking()
    {
        var f = new Fixture { NoBypass = true };
        var states = new Queue<BlockCheckSessionState>([BlockCheckSessionState.Running, BlockCheckSessionState.Completed]);
        f.ReadState = states.Dequeue;
        f.Owner.Show();
        Assert.Equal(BlockCheck2NoticeSeverity.Success, Assert.Single(f.Notices).Severity);
        Assert.DoesNotContain("no-bypass", f.Trace);
        Assert.Empty(states);
    }

    [Fact]
    public void TheThirdStateReadCanSelectWarningsAfterCompletedNoBypassWasFalse()
    {
        var f = new Fixture();
        var states = new Queue<BlockCheckSessionState>([BlockCheckSessionState.Completed,
            BlockCheckSessionState.Running, BlockCheckSessionState.CompletedWithWarnings]);
        f.ReadState = states.Dequeue;
        f.Owner.Show();
        Assert.Equal(BlockCheck2NoticeSeverity.Warning, Assert.Single(f.Notices).Severity);
        Assert.Equal(3, f.Trace.Count(s => s == "state"));
        Assert.Equal(1, f.Trace.Count(s => s == "no-bypass"));
    }

    [Fact]
    public void NoBypassTitleLocalizationCanChangeTheHttpMessage()
    {
        var f = new Fixture { State = BlockCheckSessionState.Completed, NoBypass = true };
        f.OnStep = step => { if (step == "text:BlockCheck2NoBypassRequiredTitle") f.HttpOnly = true; };
        f.Owner.Show();
        Assert.Equal("t:BlockCheck2HttpOnlyNoBypassRequiredMessage", Assert.Single(f.Notices).Message);
    }

    [Fact]
    public void TitleMutationDoesNotRestartAnAlreadySelectedCompletedBranch()
    {
        var f = new Fixture { State = BlockCheckSessionState.Completed };
        f.OnStep = step => { if (step == "text:BlockCheck2CompletedTitle") f.State = BlockCheckSessionState.Failed; };
        f.Owner.Show();
        Assert.Equal((BlockCheck2NoticeSeverity.Success, "t:BlockCheck2CompletedTitle", "t:BlockCheck2CompletedMessage"), Assert.Single(f.Notices));
        Assert.DoesNotContain("operation-error", f.Trace);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public void BlankOperationErrorUsesLazyIssuesExactlyOnce(string? operationError)
    {
        var f = new Fixture { OperationError = operationError };
        f.Owner.Show();
        Assert.Equal("issues", Assert.Single(f.Notices).Message);
        Assert.Equal(1, f.Trace.Count(s => s == "operation-error"));
        Assert.Equal(1, f.Trace.Count(s => s == "issues"));
    }

    [Fact]
    public void FailureTitleLocalizationPrecedesTheOperationErrorGuard()
    {
        var f = new Fixture();
        f.OnStep = step => { if (step == "text:BlockCheck2FailedTitle") f.OperationError = " changed error "; };
        f.Owner.Show();
        Assert.Equal(" changed error ", Assert.Single(f.Notices).Message);
        Assert.Equal(2, f.Trace.Count(s => s == "operation-error"));
        Assert.DoesNotContain("issues", f.Trace);
    }

    [Fact]
    public void NonblankOperationErrorIsReadAgainAndCanBecomeNull()
    {
        var f = new Fixture { OperationError = "first" };
        f.OnStep = step =>
        {
            if (step == "operation-error" && f.Trace.Count(s => s == step) == 2) f.OperationError = null;
        };
        f.Owner.Show();
        Assert.Null(Assert.Single(f.Notices).Message);
        Assert.DoesNotContain("issues", f.Trace);
    }

    [Fact]
    public void IssueFormattingDoesNotRetryTheOperationErrorAfterMutation()
    {
        var f = new Fixture();
        f.OnStep = step => { if (step == "issues") f.OperationError = "late"; };
        f.Owner.Show();
        Assert.Equal("issues", Assert.Single(f.Notices).Message);
        Assert.Equal(1, f.Trace.Count(s => s == "operation-error"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public void BlankHistoryErrorDoesNotLocalizeOrReadItsValueAgain(string? historyError)
    {
        var f = new Fixture { State = BlockCheckSessionState.Completed, HistoryError = historyError };
        f.Owner.Show();
        Assert.Equal("t:BlockCheck2CompletedMessage", Assert.Single(f.Notices).Message);
        Assert.Equal(1, f.Trace.Count(s => s == "history-error"));
        Assert.DoesNotContain("text:BlockCheck2HistorySaveFailedMessage", f.Trace);
    }

    [Fact]
    public void HistoryWarningReadsItsRawCurrentValueAfterLocalization()
    {
        var f = new Fixture { State = BlockCheckSessionState.Completed, HistoryError = "first" };
        f.OnStep = step => { if (step == "text:BlockCheck2HistorySaveFailedMessage") f.HistoryError = " second "; };
        f.Owner.Show();
        Assert.Equal("t:BlockCheck2CompletedMessage" + Environment.NewLine +
            "t:BlockCheck2HistorySaveFailedMessage  second ", Assert.Single(f.Notices).Message);
        Assert.Equal(2, f.Trace.Count(s => s == "history-error"));
    }

    [Fact]
    public void HistoryValueCanBecomeNullAfterItsNonblankGuard()
    {
        var f = new Fixture { HistoryError = "first" };
        f.OnStep = step => { if (step == "text:BlockCheck2HistorySaveFailedMessage") f.HistoryError = null; };
        f.Owner.Show();
        Assert.Equal("issues" + Environment.NewLine + "t:BlockCheck2HistorySaveFailedMessage ", Assert.Single(f.Notices).Message);
        Assert.Equal(2, f.Trace.Count(s => s == "history-error"));
    }

    [Fact]
    public void PublicationCanReenterAndOuterLocalsContinueAfterInnerPublication()
    {
        var f = new Fixture { State = BlockCheckSessionState.Completed };
        bool nested = false;
        f.OnStep = step =>
        {
            if (step != "publish" || nested) return;
            nested = true;
            f.State = BlockCheckSessionState.Canceled;
            f.Owner.Show();
        };
        f.Owner.Show();
        Assert.Equal([BlockCheck2NoticeSeverity.Informational, BlockCheck2NoticeSeverity.Success], f.Notices.Select(n => n.Severity));
        Assert.Equal("t:BlockCheck2CompletedMessage", f.Notices[1].Message);
        Assert.Equal(2, f.Trace.Count(s => s == "publish"));
    }

    [Theory]
    [InlineData("state")]
    [InlineData("no-bypass")]
    [InlineData("http")]
    [InlineData("text:BlockCheck2NoBypassRequiredTitle")]
    [InlineData("text:BlockCheck2FailedTitle")]
    [InlineData("operation-error")]
    [InlineData("issues")]
    [InlineData("history-error")]
    [InlineData("text:BlockCheck2HistorySaveFailedMessage")]
    [InlineData("publish")]
    public void DelegateFailurePropagatesTheSameExceptionWithoutStickyState(string point)
    {
        var f = new Fixture { State = point is "state" or "no-bypass" or "http" or "text:BlockCheck2NoBypassRequiredTitle"
            ? BlockCheckSessionState.Completed : BlockCheckSessionState.Failed, NoBypass = true, HistoryError = "history" };
        var error = new SentinelException();
        f.OnStep = step => { if (step == point) throw error; };
        Assert.Same(error, Assert.Throws<SentinelException>(f.Owner.Show));
        Assert.Empty(f.Notices);
        f.OnStep = null;
        f.Owner.Show();
        Assert.Single(f.Notices);
    }

    private sealed class SentinelException : Exception;

    private sealed class Fixture
    {
        public BlockCheckSessionState State = BlockCheckSessionState.Failed;
        public bool NoBypass, HttpOnly;
        public string? OperationError, HistoryError;
        public Func<BlockCheckSessionState>? ReadState;
        public Action<string>? OnStep;
        public List<string> Trace { get; } = [];
        public List<(BlockCheck2NoticeSeverity Severity, string Title, string? Message)> Notices { get; } = [];
        public BlockCheck2RunCompletionPresenter Owner { get; }

        public Fixture()
        {
            Owner = new(
                () => Read("state", () => ReadState?.Invoke() ?? State),
                () => Read("no-bypass", () => NoBypass),
                () => Read("http", () => HttpOnly),
                () => Read("operation-error", () => OperationError),
                () => Read("history-error", () => HistoryError),
                key => Read("text:" + key, () => "t:" + key),
                () => Read("issues", () => "issues"),
                (severity, title, message) =>
                {
                    _ = Read("publish", () => true);
                    Notices.Add((severity, title, message));
                });
        }

        private T Read<T>(string point, Func<T> value)
        {
            Trace.Add(point);
            OnStep?.Invoke(point);
            return value();
        }
    }
}
