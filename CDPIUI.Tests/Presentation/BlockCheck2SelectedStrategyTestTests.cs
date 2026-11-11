using System.Globalization;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.ViewModels;
using CDPIUI.ViewModels.BlockCheck2;

namespace CDPIUI.Tests.Presentation;

public class BlockCheck2SelectedStrategyTestTests
{
    // Detects eager constructor work that could publish state before the host is composed.
    [Fact]
    public void ConstructionDoesNotReadStateOrPublish()
    {
        var f = new Fixture();
        Assert.Empty(f.Trace);
    }

    [Fact]
    public async Task MissingSelectionDoesNotReadRunningOrPublish()
    {
        var f = new Fixture { Selected = null, Running = true };
        await f.Owner.TestAsync();
        Assert.Equal(["selected"], f.Trace);
    }

    [Fact]
    public async Task RunningTestReadsSelectionThenStopsWithoutChangingFlags()
    {
        var f = new Fixture { Running = true };
        await f.Owner.TestAsync();
        Assert.Equal(["selected", "running?"], f.Trace);
        Assert.True(f.Running);
        Assert.False(f.Progress);
    }

    // Detects moving setup into try, moving cleanup earlier or skipping post-test selection publication.
    [Fact]
    public async Task CompletedTestPublishesAndCleansUpInOriginalOrder()
    {
        var f = new Fixture();
        await f.Owner.TestAsync();
        Assert.Equal([
            "selected", "running?", "running:True", "progress:True", "selection", "test", "selection",
            "text:BlockCheck2StrategyTestResultFormat", "text:BlockCheck2StrategyTestCompletedTitle", "show",
            "running:False", "progress:False", "selection", "assignments"], f.Trace);
        Assert.Equal((BlockCheck2NoticeSeverity.Success, "BlockCheck2StrategyTestCompletedTitle", "0/0/0"), Assert.Single(f.Notices));
        Assert.False(f.Running);
        Assert.False(f.Progress);
    }

    [Fact]
    public async Task PendingTestKeepsFlagsAndRejectsNestedCallUntilCompletion()
    {
        using var culture = new CultureScope("en-US");
        var pending = new TaskCompletionSource<BlockCheckManualStrategyTestResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var f = new Fixture { Test = _ => pending.Task };
        Task task = f.Owner.TestAsync();
        Assert.False(task.IsCompleted);
        Assert.True(f.Running);
        Assert.True(f.Progress);
        await f.Owner.TestAsync();
        Assert.Single(f.Tested);
        Assert.DoesNotContain("assignments", f.Trace);
        Assert.Empty(f.Notices);
        pending.SetResult(Result(true, false));
        await task;
        Assert.False(f.Running);
        Assert.False(f.Progress);
        Assert.Equal((BlockCheck2NoticeSeverity.Warning, "BlockCheck2StrategyTestCompletedTitle", "1/2/0.5"), Assert.Single(f.Notices));
    }

    [Fact]
    public async Task SelectionNotificationKeepsCapturedItemButUsesCurrentTestPort()
    {
        var f = new Fixture();
        var original = f.Selected;
        f.OnCall = name =>
        {
            if (name != "selection") return;
            f.Selected = new BlockCheck2StrategyEvidenceItem(new());
            f.Test = item => Task.FromResult(Result(false));
        };
        await f.Owner.TestAsync();
        Assert.Same(original, Assert.Single(f.Tested));
        Assert.Equal((BlockCheck2NoticeSeverity.Warning, "BlockCheck2StrategyTestCompletedTitle", "0/1/0"), Assert.Single(f.Notices));
    }

    [Fact]
    public async Task ReentryDuringProgressSeesRunningBeforeFirstTestStarts()
    {
        var f = new Fixture();
        Task? nested = null;
        f.OnCall = name => { if (name == "progress:True") nested = f.Owner.TestAsync(); };
        await f.Owner.TestAsync();
        Assert.True(nested!.IsCompletedSuccessfully);
        Assert.Single(f.Tested);
        Assert.Equal(["selected", "running?", "running:True", "progress:True", "selected", "running?", "selection", "test"], f.Trace.Take(8));
    }

    [Theory]
    [InlineData("progress:True", true)]
    [InlineData("selection", true)]
    [InlineData("selected", false)]
    [InlineData("running?", false)]
    public async Task SetupFaultEscapesWithoutCatchOrCleanup(string endpoint, bool runningAfter)
    {
        var f = new Fixture();
        var fault = new InvalidOperationException("setup fault");
        f.OnCall = name => { if (name == endpoint) throw fault; };
        Assert.Same(fault, await Assert.ThrowsAsync<InvalidOperationException>(() => f.Owner.TestAsync()));
        Assert.Equal(runningAfter, f.Running);
        Assert.Empty(f.Errors);
        Assert.DoesNotContain("running:False", f.Trace);
        Assert.DoesNotContain("assignments", f.Trace);
        Assert.Empty(f.Tested);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SynchronousAndAwaitedTestFaultAreReportedThenCleanedUp(bool pending)
    {
        var fault = new InvalidOperationException("probe failed");
        var completion = new TaskCompletionSource<BlockCheckManualStrategyTestResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var f = new Fixture { Test = _ => pending ? completion.Task : throw fault };
        Task task = f.Owner.TestAsync();
        if (pending) completion.SetException(fault);
        await task;
        Assert.Equal(["probe failed"], f.Errors);
        Assert.Equal(["error:probe failed", "running:False", "progress:False", "selection", "assignments"], f.Trace.TakeLast(5));
    }

    [Fact]
    public async Task CancellationIsReportedAsAnErrorAndDoesNotLeaveRunning()
    {
        var f = new Fixture { Test = _ => Task.FromException<BlockCheckManualStrategyTestResult>(new OperationCanceledException("cancelled test")) };
        await f.Owner.TestAsync();
        Assert.Equal(["cancelled test"], f.Errors);
        Assert.False(f.Running);
        Assert.Contains("assignments", f.Trace);
    }

    [Fact]
    public async Task ActuallyCanceledTaskIsReportedAndOuterTaskCompletesAfterCleanup()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Task<BlockCheckManualStrategyTestResult> canceled = Task.FromCanceled<BlockCheckManualStrategyTestResult>(cancellation.Token);
        var cancellationError = await Assert.ThrowsAsync<TaskCanceledException>(() => canceled);
        var f = new Fixture { Test = _ => canceled };
        Task outer = f.Owner.TestAsync();
        await outer;
        Assert.True(outer.IsCompletedSuccessfully);
        Assert.Equal([cancellationError.Message], f.Errors);
        Assert.False(f.Running);
        Assert.False(f.Progress);
        Assert.Equal(["running:False", "progress:False", "selection", "assignments"], f.Trace.TakeLast(4));
    }

    [Fact]
    public async Task PostTestSelectionFaultIsCaughtAndFinalSelectionStillRuns()
    {
        var f = new Fixture(); int selections = 0;
        f.OnCall = name => { if (name == "selection" && ++selections == 2) throw new InvalidOperationException("selection fault"); };
        await f.Owner.TestAsync();
        Assert.Equal(["selection fault"], f.Errors);
        Assert.Equal(3, selections);
        Assert.Empty(f.Notices);
        Assert.Equal("assignments", f.Trace.Last());
    }

    [Fact]
    public async Task PresentationFaultIsCaughtBeforeCleanup()
    {
        var f = new Fixture();
        f.OnCall = name => { if (name == "show") throw new InvalidOperationException("notice fault"); };
        await f.Owner.TestAsync();
        Assert.Equal(["notice fault"], f.Errors);
        Assert.Equal(["error:notice fault", "running:False", "progress:False", "selection", "assignments"], f.Trace.TakeLast(5));
    }

    [Fact]
    public async Task ErrorCallbackFaultEscapesWithIdentityAfterCleanup()
    {
        var f = new Fixture { Test = _ => throw new InvalidOperationException("test fault") };
        var errorFault = new InvalidOperationException("error callback fault");
        f.OnCall = name => { if (name.StartsWith("error:")) throw errorFault; };
        Assert.Same(errorFault, await Assert.ThrowsAsync<InvalidOperationException>(() => f.Owner.TestAsync()));
        Assert.Equal("assignments", f.Trace.Last());
        Assert.False(f.Running);
    }

    [Theory]
    [InlineData("progress:False", "progress:False")]
    [InlineData("selection", "selection")]
    [InlineData("assignments", "assignments")]
    public async Task CleanupFaultEscapesAndSkipsRemainingCleanup(string endpoint, string last)
    {
        var f = new Fixture();
        var fault = new InvalidOperationException("cleanup fault");
        f.OnCall = name => { if (!f.Running && name == endpoint) throw fault; };
        Assert.Same(fault, await Assert.ThrowsAsync<InvalidOperationException>(() => f.Owner.TestAsync()));
        Assert.False(f.Running);
        Assert.Empty(f.Errors);
        Assert.Equal(last, f.Trace.Last());
    }

    [Fact]
    public async Task CleanupFaultReplacesCatchFaultWithoutAnotherErrorNotice()
    {
        var f = new Fixture { Test = _ => throw new InvalidOperationException("test fault") };
        var catchFault = new InvalidOperationException("catch fault");
        var finallyFault = new InvalidOperationException("finally fault");
        f.OnCall = name =>
        {
            if (name.StartsWith("error:")) throw catchFault;
            if (name == "progress:False") throw finallyFault;
        };
        Assert.Same(finallyFault, await Assert.ThrowsAsync<InvalidOperationException>(() => f.Owner.TestAsync()));
        Assert.Equal(["test fault"], f.Errors);
        Assert.Equal("progress:False", f.Trace.Last());
    }

    [Fact]
    public void MissingProbeUsesFirstIssueEvenWhenItIsOnlyAWarning()
    {
        var f = new Fixture();
        f.Owner.ShowResult(new() { Issues = [Issue(BlockCheckIssueSeverity.Warning, "first"), Issue(BlockCheckIssueSeverity.Error, "second")] });
        Assert.Equal(["issue:first", "error:first/message-first"], f.Trace);
        Assert.Equal(["first/message-first"], f.Errors);
        Assert.Empty(f.Notices);
    }

    [Fact]
    public void MissingProbeWithoutIssuesUsesFallbackText()
    {
        var f = new Fixture();
        f.Owner.ShowResult(new());
        Assert.Equal(["text:BlockCheck2StrategyTestFailedMessage", "error:BlockCheck2StrategyTestFailedMessage"], f.Trace);
    }

    [Theory]
    [InlineData(0, 0, BlockCheck2NoticeSeverity.Success, "0/0/0")]
    [InlineData(0, 1, BlockCheck2NoticeSeverity.Warning, "0/1/0")]
    [InlineData(1, 2, BlockCheck2NoticeSeverity.Warning, "1/2/0.5")]
    [InlineData(2, 2, BlockCheck2NoticeSeverity.Success, "2/2/1")]
    public void ProbeSummaryUsesCountsIncludingZeroAttemptSuccess(int successes, int attempts, BlockCheck2NoticeSeverity severity, string expected)
    {
        using var culture = new CultureScope("en-US");
        var f = new Fixture();
        f.Owner.ShowResult(new() { ProbeResult = new() { Attempts = Enumerable.Range(0, attempts).Select(i => new ProbeAttempt { Success = i < successes }).ToList() },
            Issues = [Issue(BlockCheckIssueSeverity.Warning, "warning")] });
        Assert.Equal((severity, "BlockCheck2StrategyTestCompletedTitle", expected), Assert.Single(f.Notices));
        Assert.Empty(f.Errors);
        Assert.Equal(["text:BlockCheck2StrategyTestResultFormat", "text:BlockCheck2StrategyTestCompletedTitle", "show"], f.Trace);
    }

    [Fact]
    public void FirstErrorIsShownAfterFormattingEvenWhenEveryProbeSucceeded()
    {
        var f = new Fixture();
        f.Owner.ShowResult(new() { ProbeResult = new() { Attempts = [new() { Success = true }] },
            Issues = [Issue(BlockCheckIssueSeverity.Warning, "warning"), Issue(BlockCheckIssueSeverity.Error, "first"), Issue(BlockCheckIssueSeverity.Error, "second")] });
        Assert.Equal(["text:BlockCheck2StrategyTestResultFormat", "issue:first", "error:first/message-first"], f.Trace);
        Assert.Empty(f.Notices);
    }

    // Detects caching the first Error before the localizer has returned the format string.
    [Fact]
    public void FormatLocalizationCanReplaceTheFirstErrorBeforeItIsChosen()
    {
        var issues = new List<BlockCheckIssue> { Issue(BlockCheckIssueSeverity.Error, "old") };
        var f = new Fixture();
        f.OnCall = name =>
        {
            if (name != "text:BlockCheck2StrategyTestResultFormat") return;
            issues[0] = Issue(BlockCheckIssueSeverity.Error, "current");
        };
        f.Owner.ShowResult(new() { ProbeResult = new(), Issues = issues });
        Assert.Equal(["text:BlockCheck2StrategyTestResultFormat", "issue:current", "error:current/message-current"], f.Trace);
        Assert.Equal(["current/message-current"], f.Errors);
        Assert.Empty(f.Notices);
    }

    [Fact]
    public void MalformedFormatFailsBeforeExistingErrorIssueIsLocalized()
    {
        var f = new Fixture { Format = "{3}" };
        Assert.Throws<FormatException>(() => f.Owner.ShowResult(new() { ProbeResult = new(), Issues = [Issue(BlockCheckIssueSeverity.Error, "first")] }));
        Assert.Equal(["text:BlockCheck2StrategyTestResultFormat"], f.Trace);
        Assert.Empty(f.Errors);
    }

    [Fact]
    public void FormatUsesCurrentCultureAtTheTimeOfFormatting()
    {
        using var culture = new CultureScope("en-US");
        var f = new Fixture();
        f.OnCall = name => { if (name == "text:BlockCheck2StrategyTestResultFormat") CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU"); };
        f.Owner.ShowResult(Result(true, false));
        Assert.Equal("1/2/0,5", Assert.Single(f.Notices).Message);
    }

    [Theory]
    [InlineData("text:BlockCheck2StrategyTestResultFormat")]
    [InlineData("text:BlockCheck2StrategyTestCompletedTitle")]
    [InlineData("show")]
    [InlineData("issue:first")]
    [InlineData("error:first/message-first")]
    public void DirectPresentationFaultEscapesUnchangedWithoutRetry(string endpoint)
    {
        var f = new Fixture(); var fault = new InvalidOperationException("presentation fault");
        f.OnCall = name => { if (name == endpoint) throw fault; };
        var result = endpoint.StartsWith("issue:") || endpoint.StartsWith("error:")
            ? new BlockCheckManualStrategyTestResult { Issues = [Issue(BlockCheckIssueSeverity.Error, "first")] }
            : Result(true);
        Assert.Same(fault, Assert.Throws<InvalidOperationException>(() => f.Owner.ShowResult(result)));
        Assert.Equal(endpoint, f.Trace.Last());
        Assert.Single(f.Trace, name => name == endpoint);
    }

    [Fact]
    public void NoticeCanReenterPresentationBeforeOuterShowReturns()
    {
        var f = new Fixture(); bool nested = false;
        f.OnCall = name =>
        {
            if (name != "show" || nested) return;
            nested = true; f.Owner.ShowResult(new());
        };
        f.Owner.ShowResult(Result(true));
        Assert.Single(f.Notices);
        Assert.Equal(["BlockCheck2StrategyTestFailedMessage"], f.Errors);
        Assert.Equal(["show", "text:BlockCheck2StrategyTestFailedMessage", "error:BlockCheck2StrategyTestFailedMessage"], f.Trace.TakeLast(3));
    }

    private static BlockCheckIssue Issue(BlockCheckIssueSeverity severity, string code) => new(severity, code, "message-" + code);
    private static BlockCheckManualStrategyTestResult Result(params bool[] attempts) => new()
    { ProbeResult = new() { Attempts = attempts.Select(success => new ProbeAttempt { Success = success }).ToList() } };

    private sealed class Fixture
    {
        internal readonly List<string> Trace = [];
        internal readonly List<string> Errors = [];
        internal readonly List<(BlockCheck2NoticeSeverity Severity, string Title, string Message)> Notices = [];
        internal readonly List<BlockCheck2StrategyEvidenceItem> Tested = [];
        internal BlockCheck2StrategyEvidenceItem? Selected = new(new());
        internal bool Running, Progress;
        internal string Format = "{0}/{1}/{2}";
        internal Action<string>? OnCall;
        internal Func<BlockCheck2StrategyEvidenceItem, Task<BlockCheckManualStrategyTestResult>> Test = _ => Task.FromResult(Result());
        internal readonly BlockCheck2SelectedStrategyTest Owner;
        internal Fixture()
        {
            Owner = new(() => { Call("selected"); return Selected; }, () => { Call("running?"); return Running; },
                value => { Running = value; Call("running:" + value); }, value => { Progress = value; Call("progress:" + value); },
                () => Call("selection"), item => { Tested.Add(item); Call("test"); return Test(item); },
                () => Call("assignments"), key => { Call("text:" + key); return key == "BlockCheck2StrategyTestResultFormat" ? Format : key; },
                issue => { Call("issue:" + issue.Code); return issue.Code + "/" + issue.Message; },
                message => { Errors.Add(message); Call("error:" + message); },
                (severity, title, message) => { Notices.Add((severity, title, message)); Call("show"); });
        }
        private void Call(string name) { Trace.Add(name); OnCall?.Invoke(name); }
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _before = CultureInfo.CurrentCulture;
        internal CultureScope(string name) => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
        public void Dispose() => CultureInfo.CurrentCulture = _before;
    }
}
