using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.AddOns.BlockCheck2.Reporting;
using CDPIUI.ViewModels;

namespace CDPIUI.Tests.Presentation;

public class BlockCheck2ResultTests
{
    private static readonly string[] ResultNotifications =
    [
        "Issues", "LastResult", "LastReport", "LastSession", "NoBypassRequired", "LastReportIsHttpOnly",
        "ReportJson", "ReportText", "PresetArguments", "CanExportReport", "CanExportPreset",
    ];

    [Fact]
    public void EmptyStateHasNoReportOrExportsAndDoesNotSerialize()
    {
        var reports = new Reports();
        var owner = Create(reports);

        Assert.Null(owner.OperationError);
        Assert.Null(owner.HistorySaveError);
        Assert.Empty(owner.Issues);
        Assert.Null(owner.LastResult);
        Assert.Null(owner.LastReport);
        Assert.Null(owner.LastSession);
        Assert.False(owner.NoBypassRequired);
        Assert.False(owner.LastReportIsHttpOnly);
        Assert.False(owner.CanExportReport);
        Assert.False(owner.CanExportPreset);
        Assert.Equal("", owner.ReportJson);
        Assert.Equal("", owner.ReportText);
        Assert.Equal("", owner.PresetArguments);
        Assert.Empty(reports.Calls);
    }

    [Fact]
    public void PreparedIssuesAreAssignedQuietlyWithoutChangingTheLastReport()
    {
        var reports = new Reports { Report = new() { Issues = [Issue("NO_BYPASS_REQUIRED")] } };
        var owner = Create(reports);
        owner.ApplyRunResult(new(), new());
        List<string?> notifications = [];
        owner.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        BlockCheckIssue[] prepared = [Issue("PREPARED")];

        owner.SetPreparedIssues(prepared);

        Assert.Same(prepared, owner.Issues);
        Assert.Same(reports.Report, owner.LastReport);
        Assert.True(owner.NoBypassRequired);
        Assert.Empty(notifications);
    }

    [Fact]
    public void ApplyKeepsReferenceIdentityLiveOptionReadsAndTheOriginalNotificationOrder()
    {
        var options = Options();
        options.SelectedRunPreset = BlockCheckRunPreset.Quick;
        var prepared = new BlockCheckSessionPreparationResult
        {
            Catalog = new() { CatalogVersion = "fixture-catalog" },
            Targets = [new() { Id = "original", Host = "example.test" }],
            RunOptions = new(),
            Issues = [Issue("PREPARED")],
        };
        var result = new BlockCheckRunResult();
        var report = new BlockCheckReport { Issues = [Issue("REPORTED")], PresetArguments = "--fixture" };
        var reports = new Reports { Report = report };
        var owner = new BlockCheck2RunResultViewModel(reports, options);
        reports.OnBuild = (catalog, preset, targets, run) =>
        {
            Assert.Same(result, owner.LastResult);
            Assert.Null(owner.LastReport);
            Assert.Same(prepared.Catalog, catalog);
            Assert.Equal(BlockCheckRunPreset.Quick, preset);
            Assert.Same(prepared.Targets, targets);
            Assert.Same(result, run);
            options.SelectedRunPreset = BlockCheckRunPreset.Exhaustive;
            options.ConnectTimeoutSeconds = 12;
            options.RequestTimeoutSeconds = 14;
            options.StartupGraceSeconds = 1.25;
            return report;
        };
        List<string?> notifications = [];
        owner.PropertyChanged += (sender, args) =>
        {
            Assert.Same(owner, sender);
            Assert.Same(result, owner.LastResult);
            Assert.Same(report, owner.LastReport);
            Assert.Same(report.Issues, owner.Issues);
            Assert.NotNull(owner.LastSession);
            notifications.Add(args.PropertyName);
        };

        owner.ApplyRunResult(prepared, result);

        var session = Assert.IsType<BlockCheckResultSession>(owner.LastSession);
        Assert.Same(prepared.Catalog, session.Catalog);
        Assert.Same(prepared.Targets, session.Targets);
        Assert.Same(prepared.RunOptions, session.RunOptions);
        Assert.Same(result, session.RunResult);
        Assert.Same(report, session.Report);
        Assert.Equal(TimeSpan.FromSeconds(12), session.ConnectTimeout);
        Assert.Equal(TimeSpan.FromSeconds(14), session.RequestTimeout);
        Assert.Equal(TimeSpan.FromSeconds(1.25), session.StartupGracePeriod);
        Assert.Equal(ResultNotifications, notifications);
    }

    [Fact]
    public void BuildFailureRetainsTheOriginalPartiallyAssignedStateAndRaisesNoResultNotifications()
    {
        var reports = new Reports { Report = new() { Issues = [Issue("FIRST")] } };
        var owner = Create(reports);
        owner.ApplyRunResult(new(), new());
        var oldSession = owner.LastSession;
        var nextResult = new BlockCheckRunResult();
        var failure = new InvalidOperationException("fixture build failure");
        reports.OnBuild = (_, _, _, _) => throw failure;
        List<string?> notifications = [];
        owner.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => owner.ApplyRunResult(new(), nextResult)));

        Assert.Same(nextResult, owner.LastResult);
        Assert.Same(reports.Report, owner.LastReport);
        Assert.Same(oldSession, owner.LastSession);
        Assert.Same(reports.Report.Issues, owner.Issues);
        Assert.Empty(notifications);
    }

    [Fact]
    public void NotificationFailurePropagatesAfterAllResultFieldsHaveBeenAssigned()
    {
        var reports = new Reports();
        var owner = Create(reports);
        var failure = new InvalidOperationException("fixture subscriber failure");
        List<string?> notifications = [];
        owner.PropertyChanged += (_, args) => { notifications.Add(args.PropertyName); throw failure; };

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => owner.ApplyRunResult(new(), new())));

        Assert.NotNull(owner.LastResult);
        Assert.Same(reports.Report, owner.LastReport);
        Assert.NotNull(owner.LastSession);
        Assert.Same(reports.Report.Issues, owner.Issues);
        Assert.Equal(["Issues"], notifications);
    }

    [Fact]
    public void AReentrantApplyPublishesTheNewResultBeforeTheOuterNotificationSequenceContinues()
    {
        var firstResult = new BlockCheckRunResult();
        var secondResult = new BlockCheckRunResult();
        var firstReport = new BlockCheckReport();
        var secondReport = new BlockCheckReport { PresetArguments = "--second" };
        var reports = new Reports
        {
            OnBuild = (_, _, _, result) => ReferenceEquals(result, firstResult) ? firstReport : secondReport,
        };
        var owner = Create(reports);
        List<(string? Name, BlockCheckReport? Report)> observations = [];
        bool reapplied = false;
        owner.PropertyChanged += (_, args) =>
        {
            observations.Add((args.PropertyName, owner.LastReport));
            if (!reapplied)
            {
                reapplied = true;
                owner.ApplyRunResult(new(), secondResult);
            }
        };

        owner.ApplyRunResult(new(), firstResult);

        Assert.Same(secondResult, owner.LastResult);
        Assert.Same(secondReport, owner.LastReport);
        Assert.Same(secondResult, owner.LastSession!.RunResult);
        Assert.Equal(22, observations.Count);
        Assert.Equal(("Issues", firstReport), observations[0]);
        Assert.All(observations.Skip(1), observation => Assert.Same(secondReport, observation.Report));
        Assert.Equal(ResultNotifications.Concat(ResultNotifications.Skip(1)), observations.Skip(1).Select(item => item.Name));
    }

    [Fact]
    public void AReentrantApplyDuringResetRemainsVisibleToTheRestOfTheResetNotifications()
    {
        var reports = new Reports();
        var owner = Create(reports);
        owner.ApplyRunResult(new(), new());
        var nextResult = new BlockCheckRunResult();
        List<(string? Name, BlockCheckRunResult? Result)> observations = [];
        owner.PropertyChanged += (_, args) =>
        {
            observations.Add((args.PropertyName, owner.LastResult));
            if (args.PropertyName == "OperationError")
                owner.ApplyRunResult(new(), nextResult);
        };

        owner.Reset(notify => notify());

        Assert.Null(observations[0].Result);
        Assert.Equal("OperationError", observations[0].Name);
        Assert.Same(nextResult, owner.LastResult);
        Assert.Same(nextResult, owner.LastSession!.RunResult);
        Assert.All(observations.Skip(1), observation => Assert.Same(nextResult, observation.Result));
        Assert.Equal(ResultNotifications.Concat(["HistorySaveError"]).Concat(ResultNotifications),
            observations.Skip(1).Select(item => item.Name));
    }

    [Theory]
    [InlineData("NO_BYPASS_REQUIRED", true)]
    [InlineData("no_bypass_required", false)]
    [InlineData("NO_BYPASS_REQUIRED_EXTRA", false)]
    public void NoBypassRequiredUsesTheExactOrdinalReportIssueCode(string code, bool expected)
    {
        var owner = Create(new Reports { Report = new() { Issues = [Issue(code)] } });
        owner.ApplyRunResult(new(), new());
        Assert.Equal(expected, owner.NoBypassRequired);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("Http", true)]
    [InlineData("Http,Http", true)]
    [InlineData("Http,Tls12", false)]
    [InlineData("TlsAuto", false)]
    public void HttpOnlyRequiresANonemptyReportContainingOnlyHttp(string protocols, bool expected)
    {
        var targets = protocols.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select((protocol, i) => new BlockCheckReportTarget(i.ToString(), "example.test", "/",
                Enum.Parse<BlockCheckProtocol>(protocol), BlockCheckIpVersion.IPv4,
                BlockCheckTransport.Tcp, 80, "fixture")).ToArray();
        var owner = Create(new Reports { Report = new() { Targets = targets } });
        owner.ApplyRunResult(new(), new());
        Assert.Equal(expected, owner.LastReportIsHttpOnly);
    }

    [Theory]
    [InlineData(false, false, "--fixture", false)]
    [InlineData(true, false, "--fixture", true)]
    [InlineData(false, true, "--fixture", true)]
    [InlineData(true, true, "--fixture", true)]
    [InlineData(true, false, "", false)]
    [InlineData(false, true, " \t ", false)]
    public void PresetExportRequiresSuccessOrBestEffortAndNonblankArguments(bool success, bool bestEffort, string arguments, bool expected)
    {
        var owner = Create(new Reports { Report = new() { Success = success, IsBestEffort = bestEffort, PresetArguments = arguments } });
        owner.ApplyRunResult(new(), new());
        Assert.True(owner.CanExportReport);
        Assert.Equal(expected, owner.CanExportPreset);
        Assert.Equal(arguments, owner.PresetArguments);
    }

    [Fact]
    public void ReportTextAndJsonAreSerializedLazilyOnEveryRead()
    {
        var reports = new Reports();
        var owner = Create(reports);
        owner.ApplyRunResult(new(), new());
        Assert.Equal(["build"], reports.Calls);

        Assert.Equal("json:1", owner.ReportJson);
        Assert.Equal("json:2", owner.ReportJson);
        Assert.Equal("text:1", owner.ReportText);
        Assert.Equal("text:2", owner.ReportText);

        Assert.Equal(["build", "json", "json", "text", "text"], reports.Calls);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("text")]
    [InlineData("preset")]
    public void MissingReportExportThrowsSynchronouslyBeforeCallingTheBackend(string format)
    {
        var reports = new Reports();
        var owner = Create(reports);

        var error = Assert.Throws<InvalidOperationException>(() => { _ = Save(owner, format, "unused", default); });

        Assert.Equal("No BlockCheck2 report is available.", error.Message);
        Assert.Empty(reports.Calls);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("text")]
    [InlineData("preset")]
    public async Task ExportsForwardTheExactPathReportTokenAndTask(string format)
    {
        var reports = new Reports();
        var owner = Create(reports);
        owner.ApplyRunResult(new(), new());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        reports.SaveTask = completion.Task;

        Task saved = Save(owner, format, "  C:\\fixture\\report  ", cancellation.Token);

        Assert.Same(completion.Task, saved);
        Assert.Equal(format, reports.SavedFormat);
        Assert.Equal("  C:\\fixture\\report  ", reports.SavedPath);
        Assert.Same(reports.Report, reports.SavedReport);
        Assert.Equal(cancellation.Token, reports.SavedToken);
        completion.SetResult();
        await saved;
    }

    [Theory]
    [InlineData("json")]
    [InlineData("text")]
    [InlineData("preset")]
    public void ExportBackendExceptionsRetainTheirSynchronousBoundary(string format)
    {
        var failure = new InvalidOperationException("fixture export failure");
        var reports = new Reports { SaveFailure = failure };
        var owner = Create(reports);
        owner.ApplyRunResult(new(), new());

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => { _ = Save(owner, format, "unused", default); }));
    }

    [Fact]
    public void RepeatedErrorAssignmentsStillNotifyEvenWhenTheTextMatches()
    {
        var owner = Create(new Reports());
        List<string?> notifications = [];
        owner.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        owner.SetOperationError("operation");
        owner.SetOperationError("operation");
        owner.SetHistorySaveError("history");
        owner.SetHistorySaveError("history");

        Assert.Equal("operation", owner.OperationError);
        Assert.Equal("history", owner.HistorySaveError);
        Assert.Equal(["OperationError", "OperationError", "HistorySaveError", "HistorySaveError"], notifications);
    }

    [Fact]
    public void ResetClearsAllResultAndProgressFieldsBeforeResultThenProgressNotifications()
    {
        var owner = Create(new Reports());
        owner.ApplyRunResult(new(), new());
        owner.SetOperationError("operation");
        owner.SetHistorySaveError("history");
        var progress = new BlockCheck2ProgressViewModel(() => [], () => 3, () => 0.75);
        progress.Update(new(BlockCheckScanPhase.Probing, 2, 8, "fixture", "strategy", SuccessfulChecks: 2));
        List<string> notifications = [];
        owner.PropertyChanged += (_, args) =>
        {
            Assert.Null(owner.OperationError);
            Assert.Null(owner.HistorySaveError);
            Assert.Null(owner.LastResult);
            Assert.Null(owner.LastReport);
            Assert.Null(owner.LastSession);
            Assert.Empty(owner.Issues);
            Assert.Equal(0, progress.ProgressCurrent);
            Assert.Equal(0, progress.SuccessfulChecks);
            notifications.Add(args.PropertyName!);
        };
        progress.PropertyChanged += (_, args) => notifications.Add("progress:" + args.PropertyName);

        owner.Reset(progress.Reset);

        Assert.Equal(new[] { "OperationError", "HistorySaveError" }.Concat(ResultNotifications), notifications.Take(13));
        Assert.Equal("progress:CurrentPhase", notifications[13]);
        Assert.Equal("progress:CurrentStrategyId", notifications[^1]);
        Assert.Equal(29, notifications.Count);
        notifications.Clear();
        owner.Reset(progress.Reset);
        Assert.Equal(29, notifications.Count);
    }

    [Fact]
    public void ProgressResetFailureLeavesClearedResultFieldsWithoutResultNotifications()
    {
        var owner = Create(new Reports());
        owner.ApplyRunResult(new(), new());
        owner.SetOperationError("operation");
        List<string?> notifications = [];
        owner.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        var failure = new InvalidOperationException("fixture progress failure");

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => owner.Reset(_ => throw failure)));

        Assert.Null(owner.LastReport);
        Assert.Null(owner.LastResult);
        Assert.Null(owner.LastSession);
        Assert.Null(owner.OperationError);
        Assert.Empty(owner.Issues);
        Assert.Empty(notifications);
    }

    [Fact]
    public void ProductionAdapterUsesTheExistingBuilderAndSerializers()
    {
        var serializer = new BlockCheckReportSerializer();
        var reports = new BlockCheck2ReportOperations(new BlockCheckReportBuilder(), serializer);
        var owner = new BlockCheck2RunResultViewModel(reports, Options());
        var prepared = new BlockCheckSessionPreparationResult
        {
            Catalog = new() { CatalogVersion = "fixture-version" },
            Targets = [new() { Id = "fixture", Host = "example.test", Protocol = BlockCheckProtocol.Http }],
        };

        owner.ApplyRunResult(prepared, new());

        var report = Assert.IsType<BlockCheckReport>(owner.LastReport);
        Assert.Equal("fixture-version", report.CatalogVersion);
        Assert.Equal("example.test", Assert.Single(report.Targets).Host);
        Assert.Equal(BlockCheckRunPreset.Balanced, report.RunPreset);
        Assert.Equal(serializer.SerializeJson(report), owner.ReportJson);
        Assert.Equal(serializer.SerializeText(report), owner.ReportText);
        Assert.Throws<InvalidOperationException>(() => { _ = owner.SavePresetArgumentsAsync("unused"); });
    }

    private static BlockCheckIssue Issue(string code) => new(BlockCheckIssueSeverity.Warning, code, "fixture");
    private static BlockCheck2OptionsViewModel Options() => new(new Settings(), () => false, () => 0);
    private static BlockCheck2RunResultViewModel Create(Reports reports) => new(reports, Options());
    private static Task Save(BlockCheck2RunResultViewModel owner, string format, string path, CancellationToken token) => format switch
    {
        "json" => owner.SaveReportJsonAsync(path, token),
        "text" => owner.SaveReportTextAsync(path, token),
        _ => owner.SavePresetArgumentsAsync(path, token),
    };

    private sealed class Settings : IBlockCheck2OptionsSettings
    {
        public int ReadInt(string key, int defaultValue) => defaultValue;
        public bool ReadBool(string key, bool defaultValue) => defaultValue;
        public double ReadDouble(string key, double defaultValue) => defaultValue;
        public void Save(string? key, int value) { }
        public void Save(string? key, bool value) { }
        public void Save(string? key, double value) { }
    }

    private sealed class Reports : IBlockCheck2ReportOperations
    {
        public BlockCheckReport Report { get; init; } = new();
        public List<string> Calls { get; } = [];
        public Func<StrategyCatalog, BlockCheckRunPreset, IReadOnlyList<BlockCheckTarget>, BlockCheckRunResult, BlockCheckReport>? OnBuild;
        public Task SaveTask = Task.CompletedTask;
        public Exception? SaveFailure;
        public string? SavedFormat;
        public string? SavedPath;
        public BlockCheckReport? SavedReport;
        public CancellationToken SavedToken;
        private int _jsonReads;
        private int _textReads;

        public BlockCheckReport Build(StrategyCatalog catalog, BlockCheckRunPreset preset, IReadOnlyList<BlockCheckTarget> targets, BlockCheckRunResult result)
        {
            Calls.Add("build");
            return OnBuild?.Invoke(catalog, preset, targets, result) ?? Report;
        }
        public string SerializeJson(BlockCheckReport report) { Assert.Same(Report, report); Calls.Add("json"); return "json:" + ++_jsonReads; }
        public string SerializeText(BlockCheckReport report) { Assert.Same(Report, report); Calls.Add("text"); return "text:" + ++_textReads; }
        public Task SaveJsonAsync(string path, BlockCheckReport report, CancellationToken token) => Save("json", path, report, token);
        public Task SaveTextAsync(string path, BlockCheckReport report, CancellationToken token) => Save("text", path, report, token);
        public Task SavePresetArgumentsAsync(string path, BlockCheckReport report, CancellationToken token) => Save("preset", path, report, token);
        private Task Save(string format, string path, BlockCheckReport report, CancellationToken token)
        {
            SavedFormat = format; SavedPath = path; SavedReport = report; SavedToken = token;
            if (SaveFailure != null) throw SaveFailure;
            return SaveTask;
        }
    }
}
