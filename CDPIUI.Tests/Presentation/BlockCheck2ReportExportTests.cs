using CDPIUI.AddOns.BlockCheck2.Reporting;
using CDPIUI.ViewModels.BlockCheck2;

namespace CDPIUI.Tests.Presentation;

public class BlockCheck2ReportExportTests
{
    [Fact]
    public void ConstructionDoesNotReadReportOrUseView()
    {
        var f = new Fixture();
        Assert.Empty(f.Trace);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NullReportStopsBeforeLocalizationPickerOrSave(bool json)
    {
        var f = new Fixture { Report = null };
        await f.Run(json);
        Assert.Equal(["report"], f.Trace);
    }

    [Theory]
    [InlineData(true, "blockcheck2-report.json", "BlockCheck2JsonFileFilter", "json")]
    [InlineData(false, "blockcheck2-report.txt", "BlockCheck2TextFileFilter", "text")]
    public async Task SaveUsesExactPickerArgumentsAndPublishesAfterAwait(bool json, string defaultName, string filterKey, string endpoint)
    {
        var f = new Fixture();
        await f.Run(json);
        Assert.Equal(("t:BlockCheck2SaveReportDialogTitle", defaultName, "t:" + filterKey), Assert.Single(f.Picks));
        Assert.Equal(["report", "text:BlockCheck2SaveReportDialogTitle", "text:" + filterKey, "pick", "report", endpoint,
            "text:BlockCheck2ActionCompletedTitle", "text:BlockCheck2FileSavedMessage", "show"], f.Trace);
        var saved = Assert.Single(f.Saved);
        Assert.Equal(json, saved.Json);
        Assert.Equal("chosen.path", saved.Path);
        Assert.Same(f.Report, saved.Report);
        Assert.Equal((BlockCheck2NoticeSeverity.Success, "t:BlockCheck2ActionCompletedTitle", "t:BlockCheck2FileSavedMessage"), Assert.Single(f.Notices));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PickerCancelDoesNotRereadReportOrSave(bool json)
    {
        var f = new Fixture { Path = null };
        await f.Run(json);
        Assert.Single(f.Picks);
        Assert.Single(f.Trace, name => name == "report");
        Assert.Empty(f.Saved);
        Assert.Empty(f.Notices);
        Assert.Empty(f.Errors);
    }

    [Theory]
    [InlineData(true, "")]
    [InlineData(false, " ")]
    public async Task NonNullFilenameIsPassedWithoutTrimmingOrValidation(bool json, string path)
    {
        var f = new Fixture { Path = path };
        await f.Run(json);
        Assert.Equal(path, Assert.Single(f.Saved).Path);
        Assert.Single(f.Notices);
    }

    // Detects snapshotting report at the null guard or reading it again before picker completion.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PickerUsesCurrentReportAfterLocalizationRemovedTheOriginal(bool json)
    {
        var f = new Fixture();
        var current = new BlockCheckReport { CatalogVersion = "current" };
        f.OnCall = name =>
        {
            if (name == "text:BlockCheck2SaveReportDialogTitle") f.Report = null;
            if (name == "pick") f.Report = current;
        };
        await f.Run(json);
        Assert.Same(current, Assert.Single(f.Saved).Report);
        Assert.Equal(2, f.ReportReads);
    }

    [Fact]
    public async Task ReportRemovedByPickerReachesSaveInsideCatch()
    {
        var f = new Fixture(); var missing = new ArgumentNullException("report", "missing current report");
        f.OnCall = name => { if (name == "pick") f.Report = null; };
        f.Save = (_, report) => report == null ? throw missing : Task.CompletedTask;
        await f.Run(true);
        Assert.Null(Assert.Single(f.Saved).Report);
        Assert.Equal([missing.Message], f.Errors);
        Assert.Empty(f.Notices);
    }

    [Theory]
    [InlineData(true, "report")]
    [InlineData(false, "report")]
    [InlineData(true, "text:BlockCheck2SaveReportDialogTitle")]
    [InlineData(false, "text:BlockCheck2SaveReportDialogTitle")]
    [InlineData(true, "text:BlockCheck2JsonFileFilter")]
    [InlineData(false, "text:BlockCheck2TextFileFilter")]
    [InlineData(true, "pick")]
    [InlineData(false, "pick")]
    public async Task SetupFaultEscapesWithoutErrorNotice(bool json, string endpoint)
    {
        var f = new Fixture(); var fault = new InvalidOperationException("setup fault");
        f.OnCall = name => { if (name == endpoint) throw fault; };
        Task exported = f.Run(json);
        Assert.Same(fault, await Assert.ThrowsAsync<InvalidOperationException>(() => exported));
        Assert.Empty(f.Errors);
        Assert.Empty(f.Notices);
        Assert.Empty(f.Saved);
    }

    [Fact]
    public async Task SecondReportReadFaultIsCaughtInsideSave()
    {
        var f = new Fixture();
        f.OnCall = name => { if (name == "report" && f.ReportReads == 2) throw new InvalidOperationException("current report fault"); };
        await f.Run(false);
        Assert.Equal(["current report fault"], f.Errors);
        Assert.Empty(f.Saved);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SynchronousOrPendingSaveFaultShowsErrorWithoutSuccess(bool pending)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fault = new IOException("save fault");
        var f = new Fixture { Save = (_, _) => pending ? completion.Task : throw fault };
        Task task = f.Run(true);
        if (pending)
        {
            Assert.False(task.IsCompleted);
            Assert.Empty(f.Errors);
            completion.SetException(fault);
        }
        await task;
        Assert.Equal(["save fault"], f.Errors);
        Assert.Empty(f.Notices);
        Assert.DoesNotContain("text:BlockCheck2ActionCompletedTitle", f.Trace);
    }

    [Fact]
    public async Task ConcurrentExportsKeepIndependentPathsReportsAndCompletionOrder()
    {
        var jsonDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var textDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var f = new Fixture { Path = "first.json", Save = (path, _) => path == "first.json" ? jsonDone.Task : textDone.Task };
        var firstReport = f.Report;
        Task first = f.Run(true);
        f.Path = "second.txt"; f.Report = new() { CatalogVersion = "second" };
        Task second = f.Run(false);
        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);
        Assert.Equal(2, f.Picks.Count);
        Assert.Empty(f.Notices);
        Assert.DoesNotContain("text:BlockCheck2ActionCompletedTitle", f.Trace);
        Assert.DoesNotContain("text:BlockCheck2FileSavedMessage", f.Trace);
        textDone.SetResult(); await second;
        Assert.False(first.IsCompleted);
        Assert.Single(f.Notices);
        jsonDone.SetResult(); await first;
        Assert.Equal(2, f.Notices.Count);
        Assert.Equal((true, "first.json", firstReport), f.Saved[0]);
        Assert.Equal((false, "second.txt", f.Report), f.Saved[1]);
        Assert.Equal(4, f.ReportReads);
    }

    [Fact]
    public async Task PickerCanReenterTextExportBeforeOuterJsonSave()
    {
        var f = new Fixture(); Task? nested = null;
        f.Pick = (_, name, _) =>
        {
            if (name == "blockcheck2-report.json") { nested = f.Run(false); return "outer.json"; }
            return "inner.txt";
        };
        await f.Run(true); await nested!;
        Assert.Equal(["inner.txt", "outer.json"], f.Saved.Select(entry => entry.Path));
        Assert.Equal([false, true], f.Saved.Select(entry => entry.Json));
        Assert.Equal(2, f.Notices.Count);
    }

    [Fact]
    public async Task CanceledSaveIsReportedAndOuterTaskCompletesSuccessfully()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Task canceled = Task.FromCanceled(cancellation.Token);
        var fault = await Assert.ThrowsAsync<TaskCanceledException>(() => canceled);
        var f = new Fixture { Save = (_, _) => canceled };
        Task outer = f.Run(false); await outer;
        Assert.True(outer.IsCompletedSuccessfully);
        Assert.Equal([fault.Message], f.Errors);
        Assert.Empty(f.Notices);
    }

    [Theory]
    [InlineData("text:BlockCheck2ActionCompletedTitle")]
    [InlineData("text:BlockCheck2FileSavedMessage")]
    [InlineData("show")]
    public async Task SuccessLocalizationOrNoticeFaultIsCaughtAndReported(string endpoint)
    {
        var f = new Fixture();
        f.OnCall = name => { if (name == endpoint) throw new InvalidOperationException("success fault"); };
        await f.Run(true);
        Assert.Single(f.Saved);
        Assert.Equal(["success fault"], f.Errors);
        Assert.Equal("error:success fault", f.Trace.Last());
    }

    [Fact]
    public async Task ErrorCallbackFaultEscapesWithIdentityAndWithoutRetry()
    {
        var f = new Fixture { Save = (_, _) => throw new IOException("write failed") };
        var errorFault = new InvalidOperationException("error callback fault");
        f.OnCall = name => { if (name.StartsWith("error:")) throw errorFault; };
        Task exported = f.Run(true);
        Assert.Same(errorFault, await Assert.ThrowsAsync<InvalidOperationException>(() => exported));
        Assert.Equal(["write failed"], f.Errors);
        Assert.Single(f.Saved);
    }

    [Fact]
    public async Task PrivateHelperConsumerCanSaveWithoutReportOrPicker()
    {
        var f = new Fixture { Report = null }; bool saved = false;
        await f.Owner.SaveAsync(() => { saved = true; return Task.CompletedTask; });
        Assert.True(saved);
        Assert.Equal(["text:BlockCheck2ActionCompletedTitle", "text:BlockCheck2FileSavedMessage", "show"], f.Trace);
        Assert.Empty(f.Picks);
    }

    private sealed class Fixture
    {
        internal readonly List<string> Trace = [];
        internal readonly List<(string Title, string Name, string Filter)> Picks = [];
        internal readonly List<(bool Json, string Path, BlockCheckReport? Report)> Saved = [];
        internal readonly List<(BlockCheck2NoticeSeverity Severity, string Title, string Message)> Notices = [];
        internal readonly List<string> Errors = [];
        internal BlockCheckReport? Report = new() { CatalogVersion = "initial" };
        internal string? Path = "chosen.path";
        internal int ReportReads;
        internal Action<string>? OnCall;
        internal Func<string, string, string, string?>? Pick;
        internal Func<string, BlockCheckReport, Task> Save = (_, _) => Task.CompletedTask;
        internal readonly BlockCheck2ReportExport Owner;
        internal Fixture()
        {
            Owner = new(() => { ReportReads++; Call("report"); return Report; },
                (title, name, filter) => { Picks.Add((title, name, filter)); Call("pick"); return Pick == null ? Path : Pick(title, name, filter); },
                (path, report) => { Saved.Add((true, path, report)); Call("json"); return Save(path, report); },
                (path, report) => { Saved.Add((false, path, report)); Call("text"); return Save(path, report); },
                key => { Call("text:" + key); return "t:" + key; },
                (severity, title, message) => { Notices.Add((severity, title, message)); Call("show"); },
                message => { Errors.Add(message); Call("error:" + message); });
        }
        internal Task Run(bool json) => json ? Owner.SaveJsonAsync() : Owner.SaveTextAsync();
        private void Call(string endpoint) { Trace.Add(endpoint); OnCall?.Invoke(endpoint); }
    }
}
