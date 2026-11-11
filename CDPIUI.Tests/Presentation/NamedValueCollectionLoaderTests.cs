using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CDPIUI.Helper.UserExperience;

namespace CDPIUI.Tests.Presentation;

public class NamedValueCollectionLoaderTests
{
    [Fact]
    public void LoadsRawAssignmentsThenNamedRowsWithoutNormalizationOrDeduplication()
    {
        var f = new Fixture
        {
            Assignments = ["%A%=one=two", "skip:ignored=value", "no separator", "=", "A=ordinary"],
            Named = [new("A", "named")]
        };
        f.Load();
        Assert.Equal([new Row("%A%", "one=two"), new Row("", ""), new Row("A", "ordinary"), new Row("A", "named")], f.Rows);
        Assert.Equal(["clear", "assignments", "assignments", "skip:%A%=one=two", "assignment:%A%:one=two", "add:%A%",
            "skip:skip:ignored=value", "skip:no separator", "skip:=", "assignment::", "add:", "skip:A=ordinary",
            "assignment:A:ordinary", "add:A", "named", "named", "pair:A", "add:A"], f.Trace);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ClearsEvenAnEmptyDestinationAndPreservesNullVersusEmptyReadCounts(bool nullSources)
    {
        var f = new Fixture { Assignments = nullSources ? null : [], Named = nullSources ? null : [] };
        f.Load();
        Assert.Empty(f.Rows);
        Assert.Equal(nullSources ? ["clear", "assignments", "named"] : ["clear", "assignments", "assignments", "named", "named"], f.Trace);
    }

    [Fact]
    public void ClearObserverCanReplaceTheSourceBeforeItsFirstRead()
    {
        var f = new Fixture { Assignments = ["old=value"] };
        f.Rows.Add(new("stale", "row")); f.Trace.Clear();
        f.OnStep = step => { if (step == "clear") f.Assignments = ["new=value"]; };
        f.Load();
        Assert.Equal(new Row("new", "value"), Assert.Single(f.Rows));
        Assert.Equal("clear", f.Trace[0]);
    }

    [Fact]
    public void AssignmentAddCanReplaceTheNamedSourceBeforeTheSecondPhase()
    {
        var f = new Fixture { Assignments = ["ordinary=value"], Named = [new("old", "pair")] };
        f.OnStep = step => { if (step == "add:ordinary") f.Named = [new("new", "pair")]; };
        f.Load();
        Assert.Equal([new Row("ordinary", "value"), new Row("new", "pair")], f.Rows);
        Assert.True(f.Trace.IndexOf("add:ordinary") < f.Trace.IndexOf("named"));
    }

    [Fact]
    public void EachNamedFactoryReadsMetadataAfterThePreviousAddObserver()
    {
        object first = new(), second = new();
        var f = new Fixture { Named = [new("one", "1"), new("two", "2")] };
        object current = first;
        f.NamedFactory = pair => new(pair.Key, pair.Value, current);
        f.OnStep = step => { if (step == "add:one") current = second; };
        f.Load();
        Assert.Same(first, f.Rows[0].Metadata); Assert.Same(second, f.Rows[1].Metadata);
    }

    [Theory]
    [InlineData("skip:A=value")]
    [InlineData("assignment:A:value")]
    [InlineData("add:A")]
    public void OrdinaryErrorsEscapeWithTheirIdentityAndPreventTheNamedPhase(string failingStep)
    {
        var error = new InvalidOperationException(failingStep);
        var f = new Fixture { Assignments = ["A=value", "later=value"], Named = [new("named", "value")] };
        f.OnStep = step => { if (step == failingStep) throw error; };
        Assert.Same(error, Assert.Throws<InvalidOperationException>(f.Load));
        Assert.DoesNotContain("named", f.Trace); Assert.Empty(f.Errors);
        Assert.Equal(failingStep == "add:A" ? 1 : 0, f.Rows.Count);
    }

    [Theory]
    [InlineData("pair:bad")]
    [InlineData("add:bad")]
    public void NamedFactoryAndAddErrorsReportTheSameErrorAndContinueWithPartialEffects(string failingStep)
    {
        var error = new InvalidOperationException(failingStep);
        var f = new Fixture { Named = [new("bad", "first"), new("good", "second")] };
        f.OnStep = step => { if (step == failingStep) throw error; };
        f.Load();
        Assert.Same(error, Assert.Single(f.Errors));
        Assert.Equal("good", f.Rows[^1].Name);
        Assert.Equal(failingStep == "add:bad" ? 2 : 1, f.Rows.Count);
    }

    [Fact]
    public void AReportingErrorEscapesAndStopsFurtherNamedItems()
    {
        var original = new InvalidOperationException("factory");
        var reportError = new ApplicationException("report");
        var f = new Fixture { Named = [new("bad", "1"), new("later", "2")] };
        f.NamedFactory = _ => throw original;
        f.OnStep = step => { if (step == "report") throw reportError; };
        Assert.Same(reportError, Assert.Throws<ApplicationException>(f.Load));
        Assert.Same(original, Assert.Single(f.Errors)); Assert.DoesNotContain("pair:later", f.Trace);
    }

    [Fact]
    public void NamedIteratorErrorsEscapeOutsideThePerItemRecovery()
    {
        var error = new InvalidOperationException("iterator");
        var f = new Fixture { Named = FailAfterFirst(error) };
        Assert.Same(error, Assert.Throws<InvalidOperationException>(f.Load));
        Assert.Equal(new Row("first", "value"), Assert.Single(f.Rows)); Assert.Empty(f.Errors);
    }

    [Fact]
    public void MutatingTheActiveAssignmentListRetainsItsEnumerationFailure()
    {
        var source = new List<string> { "first=value", "second=value" };
        var f = new Fixture { Assignments = source, Named = [new("named", "value")] };
        f.OnStep = step => { if (step == "add:first") source.Add("third=value"); };
        Assert.Throws<InvalidOperationException>(f.Load);
        Assert.Equal("first", Assert.Single(f.Rows).Name); Assert.Empty(f.Errors); Assert.DoesNotContain("named", f.Trace);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NonNullSourceIsReadAgainForEnumeration(bool namedPhase)
    {
        var f = new Fixture(); int reads = 0;
        if (namedPhase) f.ReadNamed = () => ++reads == 1 ? [new("old", "value")] : [new("current", "value")];
        else f.ReadAssignments = () => ++reads == 1 ? ["old=value"] : ["current=value"];
        f.Load();
        Assert.Equal(2, reads); Assert.Equal("current", Assert.Single(f.Rows).Name);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public void SourceReadErrorsEscapeWithoutBeingReported(bool namedPhase, int failingRead)
    {
        var error = new InvalidOperationException("source read");
        var f = new Fixture(); int reads = 0;
        if (namedPhase) f.ReadNamed = () => ++reads == failingRead ? throw error : [];
        else f.ReadAssignments = () => ++reads == failingRead ? throw error : [];
        Assert.Same(error, Assert.Throws<InvalidOperationException>(f.Load));
        Assert.Equal("clear", f.Trace[0]); Assert.Empty(f.Errors); Assert.Empty(f.Rows);
    }

    [Fact]
    public void ClearObserverErrorEscapesBeforeAnySourceRead()
    {
        var error = new InvalidOperationException("clear"); var f = new Fixture();
        f.Rows.Add(new("old", "value")); f.Trace.Clear();
        f.OnStep = _ => throw error;
        Assert.Same(error, Assert.Throws<InvalidOperationException>(f.Load));
        Assert.Equal(["clear"], f.Trace); Assert.Empty(f.Rows); Assert.Empty(f.Errors);
    }

    private static IEnumerable<KeyValuePair<string, string>> FailAfterFirst(Exception error)
    {
        yield return new("first", "value");
        throw error;
    }

    private sealed record Row(string Name, string Value, object? Metadata = null);

    private sealed class Fixture
    {
        public ObservableCollection<Row> Rows { get; } = [];
        public List<string> Trace { get; } = [];
        public List<Exception> Errors { get; } = [];
        public IEnumerable<string>? Assignments = [];
        public IEnumerable<KeyValuePair<string, string>>? Named = [];
        public Func<IEnumerable<string>?>? ReadAssignments;
        public Func<IEnumerable<KeyValuePair<string, string>>?>? ReadNamed;
        public Func<KeyValuePair<string, string>, Row>? NamedFactory;
        public Action<string>? OnStep;

        public Fixture()
        {
            Rows.CollectionChanged += (_, e) => Step(e.Action == NotifyCollectionChangedAction.Reset
                ? "clear" : "add:" + ((Row)e.NewItems![0]!).Name);
        }

        private void Step(string step) { Trace.Add(step); OnStep?.Invoke(step); }

        public void Load() => NamedValueCollectionLoader.Load(Rows,
            () => { Step("assignments"); return (ReadAssignments is null ? Assignments : ReadAssignments())!; },
            raw => { Step("skip:" + raw); return raw.StartsWith("skip:", StringComparison.Ordinal); },
            (name, value) => { Step("assignment:" + name + ":" + value); return new Row(name, value); },
            () => { Step("named"); return (ReadNamed is null ? Named : ReadNamed())!; },
            pair => { Step("pair:" + pair.Key); return NamedFactory?.Invoke(pair) ?? new Row(pair.Key, pair.Value); },
            error => { Errors.Add(error); Step("report"); });
    }
}
