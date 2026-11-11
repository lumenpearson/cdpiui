using CDPIUI.Helper.UserExperience;

namespace CDPIUI.Tests.Presentation;

public class DiagnosticHighlightingUpdaterTests
{
    [Fact]
    public void PlainValuesKeepErrorAndWarningOrder()
    {
        var updater = new DiagnosticHighlightingUpdater();
        List<Snapshot> applied = [];
        updater.Update("Black", false, () => new[] { 3, 2, 1, 4 },
            value => value % 2 == 1, value => value % 2 == 0,
            value => value.ToString(), (light, errors, warnings) => applied.Add(new(light, errors, warnings)));
        var result = Assert.Single(applied);
        Assert.False(result.Light);
        Assert.Equal(new[] { "3", "1" }, result.Errors);
        Assert.Equal(new[] { "2", "4" }, result.Warnings);
    }

    [Fact]
    public void ReadsCurrentDiagnosticsTwiceAndFinishesErrorsBeforeWarnings()
    {
        var updater = new DiagnosticHighlightingUpdater();
        List<string> trace = [];
        int reads = 0;
        updater.Update("Mica", true,
            () => { trace.Add("get:" + ++reads); return reads == 1 ? new[] { "e", "i" } : new[] { "w" }; },
            item => { trace.Add("error:" + item); return item == "e"; },
            item => { trace.Add("warning:" + item); return true; },
            item => { trace.Add("token:" + item); return item; },
            (light, errors, warnings) =>
            {
                trace.Add("apply"); Assert.True(light);
                Assert.Equal(new[] { "e" }, errors); Assert.Equal(new[] { "w" }, warnings);
            });
        Assert.Equal(new[] { "get:1", "error:e", "token:e", "error:i", "get:2", "warning:w", "token:w", "apply" }, trace);
    }

    [Fact]
    public void PredicateAndTokenCanReadDifferentCurrentSources()
    {
        var updater = new DiagnosticHighlightingUpdater();
        var node = new Node(new Source(true, false, "before"));
        List<Snapshot> applied = [];
        updater.Update("Black", false, () => new[] { node },
            item => { bool selected = item.Current.Error; item.Current = new(false, true, "after"); return selected; },
            item => item.Current.Warning, item => item.Current.Token,
            (light, errors, warnings) => applied.Add(new(light, errors, warnings)));
        Assert.Equal(new[] { "after" }, Assert.Single(applied).Errors);
        Assert.Equal(new[] { "after" }, applied[0].Warnings);
    }

    [Fact]
    public void ErrorTokensAreCapturedBeforeWarningCollectionMutatesSources()
    {
        var updater = new DiagnosticHighlightingUpdater();
        var node = new Node(new Source(true, false, "error"));
        int reads = 0;
        Snapshot? applied = null;
        updater.Update("Black", false,
            () => { if (++reads == 2) node.Current = new(false, true, "warning"); return new[] { node }; },
            item => item.Current.Error, item => item.Current.Warning, item => item.Current.Token,
            (light, errors, warnings) => applied = new(light, errors, warnings));
        Assert.Equal(new[] { "error" }, applied!.Errors);
        Assert.Equal(new[] { "warning" }, applied.Warnings);
    }

    [Fact]
    public void IdenticalKeyStillCollectsBothPassesBeforeSkippingApply()
    {
        var updater = new DiagnosticHighlightingUpdater();
        int reads = 0, predicates = 0, tokens = 0, applies = 0;
        void Run() => updater.Update("Black", false, () => { reads++; return new[] { 1 }; },
            _ => { predicates++; return true; }, _ => { predicates++; return true; },
            _ => { tokens++; return "same"; }, (_, _, _) => applies++);
        Run(); Run();
        Assert.Equal(4, reads); Assert.Equal(4, predicates); Assert.Equal(4, tokens); Assert.Equal(1, applies);
    }

    [Fact]
    public void UnselectedTokensAreNeverRead()
    {
        var updater = new DiagnosticHighlightingUpdater();
        updater.Update("Black", false, () => new[] { 1 }, _ => false, _ => false,
            _ => throw new InvalidOperationException(), (_, errors, warnings) =>
            { Assert.Empty(errors); Assert.Empty(warnings); });
    }

    [Fact]
    public void ArraysPreserveRawNullEmptyDuplicatesCaseAndOrder()
    {
        var updater = new DiagnosticHighlightingUpdater();
        string[] raw = [null!, "", "A", "a", "A", "x,y", "|"];
        Snapshot? result = null;
        updater.Update("Black", false, () => raw, _ => true, _ => true, item => item,
            (light, errors, warnings) => result = new(light, errors, warnings));
        Assert.Equal(raw, result!.Errors); Assert.Equal(raw, result.Warnings);
        Assert.NotSame(raw, result.Errors); Assert.NotSame(result.Errors, result.Warnings);
    }

    [Theory]
    [InlineData("Black", false, "black", false)]
    [InlineData("Black", false, "Black", true)]
    [InlineData("Mica", true, "MicaSmoke", true)]
    public void BackgroundAndLightArePartOfOrdinalKey(string first, bool firstLight, string next, bool nextLight)
    {
        var updater = new DiagnosticHighlightingUpdater(); List<Snapshot> applied = [];
        Run(updater, [], [], Capture(applied), first, firstLight);
        Run(updater, [], [], Capture(applied), next, nextLight);
        Assert.Equal(2, applied.Count); Assert.Equal(nextLight, applied[1].Light);
    }

    [Theory]
    [InlineData("A", "a")]
    [InlineData("x", "y")]
    public void TokenCaseAndValueChangesRebuild(string first, string next)
    {
        var updater = new DiagnosticHighlightingUpdater(); List<Snapshot> applied = [];
        Run(updater, [first], [], Capture(applied)); Run(updater, [next], [], Capture(applied));
        Assert.Equal(2, applied.Count);
    }

    [Fact]
    public void TokenOrderDuplicatesAndWarningChangesRebuild()
    {
        var updater = new DiagnosticHighlightingUpdater(); List<Snapshot> applied = [];
        Run(updater, ["a", "b"], [], Capture(applied));
        Run(updater, ["b", "a"], [], Capture(applied));
        Run(updater, ["b", "a", "a"], [], Capture(applied));
        Run(updater, ["b", "a", "a"], ["w"], Capture(applied));
        Assert.Equal(4, applied.Count);
    }

    [Fact]
    public void CommaDelimiterCollisionRetainsOriginalSkipBehavior()
    {
        var updater = new DiagnosticHighlightingUpdater(); List<Snapshot> applied = [];
        Run(updater, ["a,b"], [], Capture(applied));
        Run(updater, ["a", "b"], [], Capture(applied));
        Assert.Equal(new[] { "a,b" }, Assert.Single(applied).Errors);
    }

    [Fact]
    public void NullEmptyAndNoTokensShareOriginalKey()
    {
        var updater = new DiagnosticHighlightingUpdater(); List<Snapshot> applied = [];
        Run(updater, [], [], Capture(applied));
        Run(updater, [null!], [""], Capture(applied));
        Assert.Single(applied);
    }

    [Fact]
    public void PipeDelimiterCollisionAcrossSeveritiesRetainsOriginalSkip()
    {
        var updater = new DiagnosticHighlightingUpdater(); List<Snapshot> applied = [];
        Run(updater, ["a|b"], ["c"], Capture(applied));
        Run(updater, ["a"], ["b|c"], Capture(applied));
        Assert.Single(applied);
    }

    [Fact]
    public void ApplyFaultIsSameInstanceAndCommitsRequestedKey()
    {
        var updater = new DiagnosticHighlightingUpdater(); var fault = new InvalidOperationException("apply");
        Assert.Same(fault, Assert.Throws<InvalidOperationException>(() => Run(updater, ["x"], [], (_, _, _) => throw fault)));
        Run(updater, ["x"], [], (_, _, _) => Assert.Fail("A failed apply still commits its key."));
        int calls = 0; Run(updater, ["y"], [], (_, _, _) => calls++); Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("first-get")]
    [InlineData("second-get")]
    [InlineData("error-predicate")]
    [InlineData("warning-predicate")]
    [InlineData("error-token")]
    [InlineData("warning-token")]
    [InlineData("error-enumeration")]
    [InlineData("warning-enumeration")]
    public void CollectionFaultDoesNotCommitNewKey(string stage)
    {
        var updater = new DiagnosticHighlightingUpdater(); var fault = new InvalidOperationException(stage);
        Run(updater, ["old"], [], (_, _, _) => { });
        int reads = 0;
        IEnumerable<int> Values(bool fail)
        {
            yield return 1;
            if (fail) throw fault;
        }
        var thrown = Assert.Throws<InvalidOperationException>(() => updater.Update("Black", false,
            () =>
            {
                reads++;
                if ((reads == 1 && stage == "first-get") || (reads == 2 && stage == "second-get")) throw fault;
                return Values(stage == (reads == 1 ? "error-enumeration" : "warning-enumeration"));
            },
            _ => stage == "error-predicate" ? throw fault : true,
            _ => stage == "warning-predicate" ? throw fault : true,
            _ => stage == (reads == 1 ? "error-token" : "warning-token") ? throw fault : "new",
            (_, _, _) => Assert.Fail("The collection fault must prevent apply.")));
        Assert.Same(fault, thrown);
        Run(updater, ["old"], [], (_, _, _) => Assert.Fail("The previous key must survive."));
        int calls = 0; Run(updater, ["new"], ["new"], (_, _, _) => calls++); Assert.Equal(1, calls);
    }

    [Fact]
    public void CommitPrecedesBoundedSameKeyCallbackReentry()
    {
        var updater = new DiagnosticHighlightingUpdater(); int calls = 0;
        void Apply(bool light, string[] errors, string[] warnings)
        {
            calls++;
            if (calls == 1) Run(updater, ["x"], [], Apply);
        }
        Run(updater, ["x"], [], Apply);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void DifferentCallbackReentryKeySurvivesOuterReturn()
    {
        var updater = new DiagnosticHighlightingUpdater(); List<string> applied = [];
        Run(updater, ["outer"], [], (_, _, _) =>
        {
            applied.Add("outer"); Run(updater, ["inner"], [], (_, _, _) => applied.Add("inner"));
        });
        Run(updater, ["inner"], [], (_, _, _) => Assert.Fail("The inner key must remain current."));
        Run(updater, ["outer"], [], (_, _, _) => applied.Add("outer-again"));
        Assert.Equal(new[] { "outer", "inner", "outer-again" }, applied);
    }

    [Fact]
    public void ResetInCallbackSurvivesOuterReturn()
    {
        var updater = new DiagnosticHighlightingUpdater(); int calls = 0;
        Run(updater, ["same"], [], (_, _, _) => { calls++; updater.Reset(); });
        Run(updater, ["same"], [], (_, _, _) => calls++);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void ResetForcesUnchangedKeyAndInstancesStayIndependent()
    {
        var first = new DiagnosticHighlightingUpdater(); var second = new DiagnosticHighlightingUpdater(); int calls = 0;
        Action<bool, string[], string[]> apply = (_, _, _) => calls++;
        Run(first, [], [], apply); Run(second, [], [], apply);
        first.Reset(); Run(first, [], [], apply); Run(second, [], [], apply);
        Assert.Equal(3, calls);
    }

    [Fact]
    public void SameKeyCollectionReentryIsComparedAfterCollection()
    {
        var updater = new DiagnosticHighlightingUpdater(); int reads = 0, calls = 0;
        updater.Update("Black", false,
            () => { if (++reads == 1) Run(updater, ["x"], [], (_, _, _) => calls++); return new[] { "x" }; },
            _ => true, _ => false, item => item, (_, _, _) => calls++);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void DifferentCollectionReentryReplacesKeyBeforeOuterComparison()
    {
        var updater = new DiagnosticHighlightingUpdater(); int reads = 0; List<string> applied = [];
        Run(updater, ["outer"], [], (_, _, _) => { });
        updater.Update("Black", false,
            () => { if (++reads == 1) Run(updater, ["inner"], [], (_, _, _) => applied.Add("inner")); return new[] { "outer" }; },
            _ => true, _ => false, item => item, (_, _, _) => applied.Add("outer"));
        Assert.Equal(new[] { "inner", "outer" }, applied);
        Run(updater, ["outer"], [], (_, _, _) => Assert.Fail("Outer apply is the last request."));
    }

    [Fact]
    public void ResetDuringCollectionInvalidatesPreviouslyMatchingKey()
    {
        var updater = new DiagnosticHighlightingUpdater(); int reads = 0, calls = 0;
        Run(updater, ["same"], [], (_, _, _) => { });
        updater.Update("Black", false,
            () => { if (++reads == 2) updater.Reset(); return new[] { "same" }; },
            _ => true, _ => false, item => item, (_, _, _) => calls++);
        Assert.Equal(1, calls);
    }

    private static void Run(DiagnosticHighlightingUpdater updater, string[] errors, string[] warnings,
        Action<bool, string[], string[]> apply, string background = "Black", bool light = false)
    {
        int reads = 0;
        updater.Update(background, light, () => ++reads == 1 ? errors : warnings, _ => true, _ => true, item => item, apply);
    }

    private static Action<bool, string[], string[]> Capture(List<Snapshot> applied) =>
        (light, errors, warnings) => applied.Add(new(light, errors, warnings));

    private sealed record Snapshot(bool Light, string[] Errors, string[] Warnings);
    private sealed record Source(bool Error, bool Warning, string Token);
    private sealed class Node(Source current) { public Source Current { get; set; } = current; }
}
