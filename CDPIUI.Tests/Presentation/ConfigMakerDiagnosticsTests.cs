using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.ViewModels;

namespace CDPIUI.Tests.Presentation;

public sealed class ConfigMakerDiagnosticsTests
{
    [Fact]
    public void ConstructorStoresSameCollectionWithoutCallingPorts()
    {
        var f = new Fixture();
        Assert.Empty(f.Trace); Assert.Empty(f.Rows);
        Assert.Equal("", f.Owner.ErrorCountText); Assert.Equal("", f.Owner.WarningCountText);
    }

    [Fact]
    public void RebuildReadsEditorThenCurrentOptionsAndAppendsReferencesAfterValidationResults()
    {
        var f = new Fixture { Command = "preset://missing --unknown\n--need" };
        f.OnEditor = () => f.Options = [NeedOption()];
        f.Owner.Rebuild();
        Assert.Equal(["editor", "options", "resource:preset://missing", "collection:Reset"], f.Trace.Take(4));
        Assert.Equal(["CFG001", "CFG002", "CFG005"], f.Rows.Select(row => row.Source.Code));
        Assert.Equal([1, 2, 1], f.Rows.Select(row => row.Source.Line));
        Assert.Equal("ConfigMakerDiagnosticErrorCount=2", f.Owner.ErrorCountText);
        Assert.Equal("ConfigMakerDiagnosticWarningCount=1", f.Owner.WarningCountText);
        Assert.Equal("highlight", f.Trace.Last());
        Assert.All(f.Rows, row => Assert.Same(row.Source, f.Projected.Single(d => ReferenceEquals(d, row.Source))));
    }

    [Fact]
    public void CurrentInputsAreReadOnEveryRebuildAndExistingCollectionIsReused()
    {
        var f = new Fixture { Command = "--need" };
        f.Owner.Rebuild(); var first = Assert.Single(f.Rows);
        f.Command = "--need value"; f.Owner.Rebuild(); Assert.Empty(f.Rows);
        f.Command = "--unknown"; f.Options = []; f.Owner.Rebuild(); Assert.Empty(f.Rows);
        f.Options = [NeedOption()]; f.Owner.Rebuild();
        Assert.Equal("CFG001", Assert.Single(f.Rows).Source.Code);
        Assert.DoesNotContain(first, f.Rows);
        Assert.Equal(4, f.Trace.Count(x => x == "editor"));
        Assert.Equal(4, f.Trace.Count(x => x == "options"));
    }

    [Fact]
    public void ActualVariableCollectionAndCurrentResourcePredicateAreUsed()
    {
        var f = new Fixture { Command = "%KNOWN% %CURRENT% preset://attached" };
        f.Variables.Add(new() { Name = "known" });
        f.KnownResource = true; f.Owner.Rebuild(); Assert.Empty(f.Rows);
        f.KnownResource = false; f.Owner.Rebuild();
        Assert.Equal("CFG005", Assert.Single(f.Rows).Source.Code);
        Assert.Equal(2, f.Trace.Count(x => x == "resource:preset://attached"));
    }

    [Theory]
    [InlineData(ComponentCommandDiagnosticSeverity.Error, "ConfigMakerDiagnosticErrorSeverity")]
    [InlineData(ComponentCommandDiagnosticSeverity.Warning, "ConfigMakerDiagnosticWarningSeverity")]
    [InlineData((ComponentCommandDiagnosticSeverity)99, "ConfigMakerDiagnosticWarningSeverity")]
    public void SeverityLocalizationRetainsFallback(ComponentCommandDiagnosticSeverity severity, string key)
    {
        var f = new Fixture();
        Assert.Equal(key, f.Owner.GetSeverityText(new() { Severity = severity }));
        Assert.Equal(["text:" + key], f.Trace);
    }

    [Theory]
    [InlineData(ComponentCommandDiagnosticKind.UnknownFlag, "ConfigMakerUnknownFlagDiagnostic", true)]
    [InlineData(ComponentCommandDiagnosticKind.MissingRequiredArgument, "ConfigMakerMissingArgumentDiagnostic", true)]
    [InlineData(ComponentCommandDiagnosticKind.UnresolvedVariable, "ConfigMakerUnresolvedVariableDiagnostic", true)]
    [InlineData(ComponentCommandDiagnosticKind.UnresolvedResource, "ConfigMakerUnresolvedResourceDiagnostic", true)]
    [InlineData(ComponentCommandDiagnosticKind.UnterminatedQuote, "ConfigMakerUnterminatedQuoteDiagnostic", false)]
    [InlineData((ComponentCommandDiagnosticKind)99, "ConfigMakerUnterminatedQuoteDiagnostic", false)]
    public void DescriptionRetainsKindTokenAndFallback(ComponentCommandDiagnosticKind kind, string key, bool format)
    {
        var f = new Fixture();
        f.Localize = k => k + "<{0}>";
        Assert.Equal(key + (format ? "<--token={x}>" : "<{0}>"),
            f.Owner.GetDescription(new() { Kind = kind, Token = "--token={x}" }));
        Assert.Equal(["text:" + key], f.Trace);
    }

    [Fact]
    public void FormattingUsesCurrentCultureAtCallTime()
    {
        var f = new Fixture { Command = "--need" };
        f.Localize = key => key.EndsWith("Count", StringComparison.Ordinal) ? "{0:N1}" : key;
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR"); f.Owner.Rebuild();
            Assert.Equal("1,0", f.Owner.ErrorCountText); Assert.Equal("0,0", f.Owner.WarningCountText);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US"); f.Owner.Rebuild();
            Assert.Equal("1.0", f.Owner.ErrorCountText); Assert.Equal("0.0", f.Owner.WarningCountText);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void CounterEventsFollowAllRowsAndEqualitySuppressesOnlyUnchangedCounters()
    {
        var f = new Fixture { Command = "--need" };
        f.Owner.Rebuild();
        Assert.Equal(["collection:Reset", "project:CFG002", "text:ConfigMakerDiagnosticErrorSeverity",
            "text:ConfigMakerMissingArgumentDiagnostic", "collection:Add", "text:ConfigMakerDiagnosticErrorCount",
            "property:ErrorCountText", "text:ConfigMakerDiagnosticWarningCount", "property:WarningCountText", "highlight"], f.Trace.Skip(2));
        f.Trace.Clear(); f.Owner.Rebuild();
        Assert.DoesNotContain(f.Trace, x => x.StartsWith("property:", StringComparison.Ordinal));
        Assert.Contains("collection:Reset", f.Trace); Assert.Contains("collection:Add", f.Trace);
        Assert.Equal("highlight", f.Trace.Last());
    }

    [Theory]
    [InlineData("editor")]
    [InlineData("options")]
    [InlineData("resource")]
    public void InputFailureBeforeClearPreservesPreviousRowsAndCounters(string endpoint)
    {
        var f = new Fixture { Command = "--need" }; f.Owner.Rebuild();
        var old = Assert.Single(f.Rows); var error = f.Owner.ErrorCountText; var warning = f.Owner.WarningCountText;
        f.Command = "preset://missing"; f.Trace.Clear();
        f.OnEditor = () => { if (endpoint == "editor") throw new IOException("editor"); };
        f.OnOptions = () => { if (endpoint == "options") throw new IOException("options"); };
        f.OnResource = () => { if (endpoint == "resource") throw new IOException("resource"); };
        Assert.Throws<IOException>(f.Owner.Rebuild);
        Assert.Same(old, Assert.Single(f.Rows));
        Assert.Equal(error, f.Owner.ErrorCountText); Assert.Equal(warning, f.Owner.WarningCountText);
        Assert.DoesNotContain("collection:Reset", f.Trace); Assert.DoesNotContain("highlight", f.Trace);
    }

    [Fact]
    public void ClearObserverFailureLeavesClearedCollectionAndPreviousCounters()
    {
        var f = new Fixture { Command = "--need" }; f.Owner.Rebuild(); f.Trace.Clear();
        f.Rows.CollectionChanged += (_, e) => { if (e.Action == NotifyCollectionChangedAction.Reset) throw new IOException("clear"); };
        Assert.Throws<IOException>(f.Owner.Rebuild); Assert.Empty(f.Rows);
        Assert.Equal("ConfigMakerDiagnosticErrorCount=1", f.Owner.ErrorCountText);
        Assert.DoesNotContain("highlight", f.Trace); Assert.DoesNotContain(f.Trace, x => x.StartsWith("project:", StringComparison.Ordinal));
    }

    [Fact]
    public void FactoryFailureLeavesEarlierRowsButNotCountsOrHighlight()
    {
        var f = new Fixture { Command = "--unknown --need" };
        f.OnProject = d => { if (d.Code == "CFG002") throw new IOException("factory"); };
        Assert.Throws<IOException>(f.Owner.Rebuild);
        Assert.Equal("CFG001", Assert.Single(f.Rows).Source.Code);
        Assert.Equal("", f.Owner.ErrorCountText); Assert.Equal("", f.Owner.WarningCountText);
        Assert.DoesNotContain("highlight", f.Trace);
    }

    [Fact]
    public void AddObserverFailureRetainsAlreadyAddedRowAndSkipsCounts()
    {
        var f = new Fixture { Command = "--need" };
        f.Rows.CollectionChanged += (_, e) => { if (e.Action == NotifyCollectionChangedAction.Add) throw new IOException("add"); };
        Assert.Throws<IOException>(f.Owner.Rebuild); Assert.Single(f.Rows);
        Assert.Equal("", f.Owner.ErrorCountText); Assert.DoesNotContain("highlight", f.Trace);
    }

    [Theory]
    [InlineData("ConfigMakerDiagnosticErrorSeverity", false)]
    [InlineData("ConfigMakerMissingArgumentDiagnostic", false)]
    [InlineData("ConfigMakerDiagnosticErrorCount", false)]
    [InlineData("ConfigMakerDiagnosticWarningCount", true)]
    public void LocalizationFailurePreservesExactPartialCounterStage(string key, bool assignedError)
    {
        var f = new Fixture { Command = "--need" };
        f.Localize = k => k == key ? throw new IOException("text") : Fixture.DefaultText(k);
        Assert.Throws<IOException>(f.Owner.Rebuild);
        Assert.Equal(assignedError ? "ConfigMakerDiagnosticErrorCount=1" : "", f.Owner.ErrorCountText);
        Assert.Equal("", f.Owner.WarningCountText); Assert.DoesNotContain("highlight", f.Trace);
        Assert.Equal(key.EndsWith("Count", StringComparison.Ordinal) ? 1 : 0, f.Rows.Count);
    }

    [Theory]
    [InlineData("ErrorCountText", "")]
    [InlineData("WarningCountText", "ConfigMakerDiagnosticWarningCount=0")]
    public void CounterObserverFailureKeepsAssignmentAndStopsLaterCallbacks(string name, string warning)
    {
        var f = new Fixture { Command = "--need" };
        f.Observe = n => { if (n == name) throw new IOException("counter"); };
        Assert.Throws<IOException>(f.Owner.Rebuild);
        Assert.Equal("ConfigMakerDiagnosticErrorCount=1", f.Owner.ErrorCountText);
        Assert.Equal(warning, f.Owner.WarningCountText); Assert.DoesNotContain("highlight", f.Trace);
    }

    [Fact]
    public void HighlightFailureOccursAfterCollectionAndBothCounters()
    {
        var f = new Fixture { Command = "--need" }; f.OnHighlight = () => throw new IOException("highlight");
        Assert.Throws<IOException>(f.Owner.Rebuild); Assert.Single(f.Rows);
        Assert.Equal("ConfigMakerDiagnosticErrorCount=1", f.Owner.ErrorCountText);
        Assert.Equal("ConfigMakerDiagnosticWarningCount=0", f.Owner.WarningCountText);
    }

    [Fact]
    public void ReentryFromClearKeepsOuterResultsAndContinuesAfterNestedRebuild()
    {
        var f = new Fixture { Command = "--unknown --need" }; bool entered = false;
        f.OnCollection = e =>
        {
            if (e.Action == NotifyCollectionChangedAction.Reset && !entered) { entered = true; f.Command = ""; f.Owner.Rebuild(); }
        };
        f.Owner.Rebuild();
        Assert.Equal(["CFG001", "CFG002"], f.Rows.Select(x => x.Source.Code));
        Assert.Equal("ConfigMakerDiagnosticErrorCount=1", f.Owner.ErrorCountText);
        Assert.Equal("ConfigMakerDiagnosticWarningCount=1", f.Owner.WarningCountText);
        Assert.Equal(2, f.Trace.Count(x => x == "highlight"));
    }

    [Fact]
    public void ReentryFromErrorCounterKeepsOriginalOuterWarningContinuation()
    {
        var f = new Fixture { Command = "--unknown --need" }; bool entered = false;
        f.Observe = name =>
        {
            if (name == "ErrorCountText" && !entered) { entered = true; f.Command = ""; f.Owner.Rebuild(); }
        };
        f.Owner.Rebuild(); Assert.Empty(f.Rows);
        Assert.Equal("ConfigMakerDiagnosticErrorCount=0", f.Owner.ErrorCountText);
        Assert.Equal("ConfigMakerDiagnosticWarningCount=1", f.Owner.WarningCountText);
        Assert.Equal(2, f.Trace.Count(x => x == "highlight"));
    }

    [Fact]
    public void CollectionObserversMayAlterPublishedRowsWithoutChangingOuterResultCounts()
    {
        var f = new Fixture { Command = "--need" }; bool entered = false;
        f.OnCollection = e =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add && !entered) { entered = true; f.Rows.Clear(); }
        };
        f.Owner.Rebuild(); Assert.Empty(f.Rows);
        Assert.Equal("ConfigMakerDiagnosticErrorCount=1", f.Owner.ErrorCountText);
        Assert.Equal("ConfigMakerDiagnosticWarningCount=0", f.Owner.WarningCountText);
    }

    [Fact]
    public void ReentryFromFactoryRetainsOuterRowAndCounterSnapshot()
    {
        var f = new Fixture { Command = "--need" }; bool entered = false;
        f.OnProject = _ => { if (!entered) { entered = true; f.Command = ""; f.Owner.Rebuild(); } };
        f.Owner.Rebuild(); Assert.Single(f.Rows);
        Assert.Equal("ConfigMakerDiagnosticErrorCount=1", f.Owner.ErrorCountText);
        Assert.Equal(2, f.Trace.Count(x => x == "highlight"));
    }

    [Fact]
    public void ReentryFromSeverityLocalizationKeepsOuterProjectionAndCounts()
    {
        var f = new Fixture { Command = "--need" }; bool entered = false;
        f.Localize = key =>
        {
            if (key == "ConfigMakerDiagnosticErrorSeverity" && !entered) { entered = true; f.Command = ""; f.Owner.Rebuild(); }
            return Fixture.DefaultText(key);
        };
        f.Owner.Rebuild(); Assert.Single(f.Rows);
        Assert.Equal("ConfigMakerDiagnosticErrorCount=1", f.Owner.ErrorCountText);
        Assert.Equal(2, f.Trace.Count(x => x == "highlight"));
    }

    [Fact]
    public void ReentryFromHighlightPublishesNestedFinalState()
    {
        var f = new Fixture { Command = "--need" }; bool entered = false;
        f.OnHighlight = () => { if (!entered) { entered = true; f.Command = ""; f.Owner.Rebuild(); } };
        f.Owner.Rebuild(); Assert.Empty(f.Rows);
        Assert.Equal("ConfigMakerDiagnosticErrorCount=0", f.Owner.ErrorCountText);
        Assert.Equal(2, f.Trace.Count(x => x == "highlight"));
    }

    [Theory]
    [InlineData("ConfigMakerDiagnosticErrorCount", "")]
    [InlineData("ConfigMakerDiagnosticWarningCount", "ConfigMakerDiagnosticErrorCount=1")]
    public void InvalidCounterFormatPreservesEarlierAssignments(string key, string error)
    {
        var f = new Fixture { Command = "--need" };
        f.Localize = k => k == key ? "{not-a-number}" : Fixture.DefaultText(k);
        Assert.Throws<FormatException>(f.Owner.Rebuild); Assert.Single(f.Rows);
        Assert.Equal(error, f.Owner.ErrorCountText); Assert.Equal("", f.Owner.WarningCountText);
        Assert.DoesNotContain("highlight", f.Trace);
    }

    [Fact]
    public void MultipleCollectionListenersKeepObservableCollectionReentryGuard()
    {
        var f = new Fixture { Command = "--need" };
        f.Rows.CollectionChanged += (_, e) => { if (e.Action == NotifyCollectionChangedAction.Add) f.Rows.Clear(); };
        Assert.Throws<InvalidOperationException>(f.Owner.Rebuild); Assert.Single(f.Rows);
        Assert.Equal("", f.Owner.ErrorCountText); Assert.DoesNotContain("highlight", f.Trace);
    }

    private static ComponentCommandHelpOption NeedOption() => new() { Names = ["--need"], Syntax = "--need <value>", IsArgumentRequired = true };
    private sealed record Row(ComponentCommandDiagnostic Source, string Severity, string Description);
    private sealed class Fixture
    {
        public Fixture()
        {
            Rows.CollectionChanged += (_, e) => { Trace.Add("collection:" + e.Action); OnCollection?.Invoke(e); };
            Owner = new(Rows, () => { Trace.Add("editor"); OnEditor?.Invoke(); return Command; },
                () => { Trace.Add("options"); OnOptions?.Invoke(); return Options; }, Variables,
                reference => { Trace.Add("resource:" + reference); OnResource?.Invoke(); return KnownResource; },
                d => { Trace.Add("project:" + d.Code); Projected.Add(d); OnProject?.Invoke(d); return new(d, Owner!.GetSeverityText(d), Owner.GetDescription(d)); },
                key => { Trace.Add("text:" + key); return Localize is null ? DefaultText(key) : Localize(key); },
                () => { Trace.Add("highlight"); OnHighlight?.Invoke(); });
            Owner.PropertyChanged += (sender, e) => { Assert.Same(Owner, sender); Trace.Add("property:" + e.PropertyName); Observe?.Invoke(e.PropertyName!); };
        }
        public ConfigMakerDiagnostics<Row> Owner { get; }
        public ObservableCollection<Row> Rows { get; } = [];
        public ObservableCollection<ConfigMakerVariableDefinition> Variables { get; } = [];
        public IReadOnlyList<ComponentCommandHelpOption> Options { get; set; } = [NeedOption()];
        public List<string> Trace { get; } = [];
        public List<ComponentCommandDiagnostic> Projected { get; } = [];
        public string Command { get; set; } = "";
        public bool KnownResource { get; set; }
        public Action? OnEditor, OnOptions, OnResource, OnHighlight;
        public Action<ComponentCommandDiagnostic>? OnProject;
        public Action<NotifyCollectionChangedEventArgs>? OnCollection;
        public Action<string>? Observe;
        public Func<string, string>? Localize;
        public static string DefaultText(string key) => key.EndsWith("Count", StringComparison.Ordinal) ? key + "={0}" :
            key == "ConfigMakerUnterminatedQuoteDiagnostic" || key.EndsWith("Severity", StringComparison.Ordinal) ? key : key + "({0})";
    }
}
