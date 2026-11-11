using CDPIUI.ViewModels.Welcome;

namespace CDPIUI.Tests.Presentation;

public sealed class WelcomeMigrationPresentationTests
{
    private enum Severity { Initial = 41, Error = 73, Other = 99 }

    [Fact]
    public void NineDefaultsAndExplicitSeveritiesAreCold()
    {
        var owner = new WelcomeMigrationPresentation<Severity>(Severity.Initial, Severity.Error, null!);
        Assert.False(owner.IsMigrationMode);
        Assert.True(owner.IsReadyBundlesContentVisible);
        Assert.Equal(string.Empty, owner.MigrationStatusTitle);
        Assert.Equal(string.Empty, owner.MigrationStatusMessage);
        Assert.Equal(Severity.Initial, owner.MigrationStatusSeverity);
        Assert.Equal(0, owner.MigrationProgress);
        Assert.Null(owner.MigrationComponentItems);
        Assert.True(owner.IsMigrationComponentsListVisible);
        Assert.Equal(string.Empty, owner.MigrationSummaryText);
        Assert.False(owner.IsRetryMigrationVisible);
    }

    private sealed class Sentinel : Exception;

    private sealed class Subject
    {
        internal readonly WelcomeMigrationPresentation<Severity> State;
        internal readonly List<string> Trace = [];
        internal readonly Sentinel Failure = new();
        internal Func<string, string?> Resolve = key => key;
        internal Action<string>? Observe;
        internal string? Fault;

        internal Subject()
        {
            State = new(Severity.Initial, Severity.Error, key =>
            {
                Record("text:" + key);
                return Resolve(key);
            });
            State.PropertyChanged += (sender, args) =>
            {
                Assert.Same(State, sender);
                string name = args.PropertyName!;
                Record("event:" + name);
                Observe?.Invoke(name);
            };
        }

        private void Record(string token)
        {
            Trace.Add(token);
            if (Fault == token) throw Failure;
        }
    }

    [Theory]
    [InlineData("IsMigrationMode")]
    [InlineData("MigrationStatusTitle")]
    [InlineData("MigrationStatusMessage")]
    [InlineData("MigrationStatusSeverity")]
    [InlineData("MigrationProgress")]
    [InlineData("MigrationComponentItems")]
    [InlineData("IsMigrationComponentsListVisible")]
    [InlineData("MigrationSummaryText")]
    [InlineData("IsRetryMigrationVisible")]
    public void EachSetterKeepsValueAndSuppressesEquality(string name)
    {
        var s = new Subject();
        IReadOnlyList<MigrationComponentDisplayItem> items = [new() { StoreId = "one" }];
        Action assign = name switch
        {
            "IsMigrationMode" => () => s.State.IsMigrationMode = true,
            "MigrationStatusTitle" => () => s.State.MigrationStatusTitle = "title",
            "MigrationStatusMessage" => () => s.State.MigrationStatusMessage = "message",
            "MigrationStatusSeverity" => () => s.State.MigrationStatusSeverity = (Severity)(-7),
            "MigrationProgress" => () => s.State.MigrationProgress = -12.5,
            "MigrationComponentItems" => () => s.State.MigrationComponentItems = items,
            "IsMigrationComponentsListVisible" => () => s.State.IsMigrationComponentsListVisible = false,
            "MigrationSummaryText" => () => s.State.MigrationSummaryText = "summary",
            "IsRetryMigrationVisible" => () => s.State.IsRetryMigrationVisible = true,
            _ => throw new InvalidOperationException(name)
        };
        assign();
        object? first = s.State.GetType().GetProperty(name)!.GetValue(s.State);
        Assert.NotNull(first);
        Assert.Equal(name == "IsMigrationMode"
            ? ["event:IsMigrationMode", "event:IsReadyBundlesContentVisible"]
            : new[] { "event:" + name }, s.Trace);
        s.Trace.Clear();
        assign();
        Assert.Empty(s.Trace);
        Assert.Equal(first, s.State.GetType().GetProperty(name)!.GetValue(s.State));
        if (name == "MigrationComponentItems") Assert.Same(items, first);
    }

    [Theory]
    [InlineData("MigrationStatusTitle")]
    [InlineData("MigrationStatusMessage")]
    [InlineData("MigrationSummaryText")]
    public void StringSettersKeepRawNullAndEquality(string name)
    {
        var s = new Subject();
        Action assign = name switch
        {
            "MigrationStatusTitle" => () => s.State.MigrationStatusTitle = null,
            "MigrationStatusMessage" => () => s.State.MigrationStatusMessage = null,
            _ => () => s.State.MigrationSummaryText = null
        };
        assign(); assign();
        Assert.Null(s.State.GetType().GetProperty(name)!.GetValue(s.State));
        Assert.Equal(new[] { "event:" + name }, s.Trace);
    }

    private sealed class EqualItems : List<MigrationComponentDisplayItem>
    {
        public override bool Equals(object? obj) => obj is EqualItems;
        public override int GetHashCode() => 0;
    }

    [Fact]
    public void ComponentListKeepsReferenceNullAndDefaultCustomEquality()
    {
        var s = new Subject();
        var first = new EqualItems();
        s.State.MigrationComponentItems = first;
        s.State.MigrationComponentItems = new EqualItems();
        Assert.Same(first, s.State.MigrationComponentItems);
        Assert.Single(s.Trace);
        IReadOnlyList<MigrationComponentDisplayItem> different = [];
        s.State.MigrationComponentItems = different;
        Assert.Same(different, s.State.MigrationComponentItems);
        s.State.MigrationComponentItems = null;
        s.State.MigrationComponentItems = null;
        Assert.Null(s.State.MigrationComponentItems);
        Assert.Equal(3, s.Trace.Count);
    }

    [Fact]
    public void ProgressRetainsNonfiniteValuesAndEqualValueBits()
    {
        var s = new Subject();
        s.State.MigrationProgress = -0d;
        Assert.Empty(s.Trace);
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(s.State.MigrationProgress));
        const long firstBits = 0x7ff8000000000011;
        s.State.MigrationProgress = BitConverter.Int64BitsToDouble(firstBits);
        s.State.MigrationProgress = BitConverter.Int64BitsToDouble(0x7ff8000000000022);
        Assert.Equal(firstBits, BitConverter.DoubleToInt64Bits(s.State.MigrationProgress));
        Assert.Single(s.Trace);
        s.State.MigrationProgress = double.PositiveInfinity;
        s.State.MigrationProgress = double.PositiveInfinity;
        Assert.Equal(double.PositiveInfinity, s.State.MigrationProgress);
        s.State.MigrationProgress = double.NegativeInfinity;
        Assert.Equal(double.NegativeInfinity, s.State.MigrationProgress);
        Assert.Equal(3, s.Trace.Count);
    }

    [Fact]
    public void ModeNotifiesPrimaryThenDerivedWithCurrentSnapshots()
    {
        var s = new Subject();
        var snapshots = new List<(string, bool, bool)>();
        s.Observe = name => snapshots.Add((name, s.State.IsMigrationMode, s.State.IsReadyBundlesContentVisible));
        s.State.IsMigrationMode = true;
        s.State.IsMigrationMode = true;
        s.State.IsMigrationMode = false;
        Assert.Equal(new[]
        {
            ("IsMigrationMode", true, false), ("IsReadyBundlesContentVisible", true, false),
            ("IsMigrationMode", false, true), ("IsReadyBundlesContentVisible", false, true)
        }, snapshots);
    }

    [Fact]
    public void NestedModeReversalStillLeavesOuterDerived()
    {
        var s = new Subject();
        var derived = new List<bool>();
        s.Observe = name =>
        {
            if (name == "IsMigrationMode" && s.State.IsMigrationMode) s.State.IsMigrationMode = false;
            if (name == "IsReadyBundlesContentVisible") derived.Add(s.State.IsReadyBundlesContentVisible);
        };
        s.State.IsMigrationMode = true;
        Assert.Equal(new[] { "event:IsMigrationMode", "event:IsMigrationMode",
            "event:IsReadyBundlesContentVisible", "event:IsReadyBundlesContentVisible" }, s.Trace);
        Assert.Equal(new[] { true, true }, derived);
        Assert.False(s.State.IsMigrationMode);
    }

    [Fact]
    public void ModeObserverFailureStopsDerivedAndEqualRetryStaysSilent()
    {
        var s = new Subject { Fault = "event:IsMigrationMode" };
        Assert.Same(s.Failure, Assert.Throws<Sentinel>(() => s.State.IsMigrationMode = true));
        Assert.True(s.State.IsMigrationMode);
        Assert.False(s.State.IsReadyBundlesContentVisible);
        Assert.Equal(new[] { "event:IsMigrationMode" }, s.Trace);
        s.Fault = null; s.Trace.Clear();
        s.State.IsMigrationMode = true;
        Assert.Empty(s.Trace);
        s.State.IsMigrationMode = false;
        Assert.Equal(new[] { "event:IsMigrationMode", "event:IsReadyBundlesContentVisible" }, s.Trace);
    }

    [Fact]
    public void StatusAssignsTitleBeforeResolvingMessage()
    {
        var s = new Subject();
        string message = "before";
        s.Resolve = key => key.EndsWith("/message", StringComparison.Ordinal) ? message : "title";
        s.Observe = name => { if (name == "MigrationStatusTitle") message = "after title observer"; };
        s.State.ShowStatus("title", "message", Severity.Other);
        Assert.Equal("after title observer", s.State.MigrationStatusMessage);
        Assert.Equal(new[] { "text:/WelcomeWizard/title", "event:MigrationStatusTitle",
            "text:/WelcomeWizard/message", "event:MigrationStatusMessage", "event:MigrationStatusSeverity" }, s.Trace);
    }

    [Fact]
    public void StatusKeepsRawKeysNullValuesAndUnknownSeverity()
    {
        var s = new Subject { Resolve = _ => null };
        s.State.ShowStatus(null, "/raw/key", (Severity)(-7));
        Assert.Null(s.State.MigrationStatusTitle);
        Assert.Null(s.State.MigrationStatusMessage);
        Assert.Equal((Severity)(-7), s.State.MigrationStatusSeverity);
        Assert.Equal(new[] { "text:/WelcomeWizard/", "event:MigrationStatusTitle",
            "text:/WelcomeWizard//raw/key", "event:MigrationStatusMessage", "event:MigrationStatusSeverity" }, s.Trace);
    }

    [Fact]
    public void RepeatedStatusStillLooksUpBothTextsWhileEqualPropertiesStaySilent()
    {
        var s = new Subject();
        s.State.ShowStatus("title", "message", Severity.Other);
        s.Trace.Clear();
        s.State.ShowStatus("title", "message", Severity.Other);
        Assert.Equal(new[] { "text:/WelcomeWizard/title", "text:/WelcomeWizard/message" }, s.Trace);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NestedStatusFromLookupOrTitleKeepsOuterArgumentsAndCurrentEquality(bool fromLookup)
    {
        var s = new Subject();
        bool nested = false;
        void Inner()
        {
            if (nested) return;
            nested = true;
            s.State.ShowStatus("inner-title", "outer-message", Severity.Other);
        }
        s.Resolve = key => { if (fromLookup && key == "/WelcomeWizard/outer-title") Inner(); return key; };
        s.Observe = name => { if (!fromLookup && name == "MigrationStatusTitle") Inner(); };
        s.State.ShowStatus("outer-title", "outer-message", Severity.Other);
        Assert.Equal(fromLookup ? "/WelcomeWizard/outer-title" : "/WelcomeWizard/inner-title", s.State.MigrationStatusTitle);
        Assert.Equal("/WelcomeWizard/outer-message", s.State.MigrationStatusMessage);
        Assert.Equal(Severity.Other, s.State.MigrationStatusSeverity);
        Assert.Equal(1, s.Trace.Count(x => x == "event:MigrationStatusMessage"));
        Assert.Equal(1, s.Trace.Count(x => x == "event:MigrationStatusSeverity"));
        Assert.Equal(2, s.Trace.Count(x => x == "text:/WelcomeWizard/outer-message"));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void StatusFaultAtEachBoundaryKeepsPartialStateAndRetries(int at)
    {
        string[] steps = ["text:/WelcomeWizard/title", "event:MigrationStatusTitle",
            "text:/WelcomeWizard/message", "event:MigrationStatusMessage", "event:MigrationStatusSeverity"];
        var s = new Subject { Fault = steps[at] };
        Assert.Same(s.Failure, Assert.Throws<Sentinel>(() => s.State.ShowStatus("title", "message", Severity.Other)));
        Assert.Equal(steps.Take(at + 1), s.Trace);
        Assert.Equal(at >= 1 ? "/WelcomeWizard/title" : "", s.State.MigrationStatusTitle);
        Assert.Equal(at >= 3 ? "/WelcomeWizard/message" : "", s.State.MigrationStatusMessage);
        Assert.Equal(at >= 4 ? Severity.Other : Severity.Initial, s.State.MigrationStatusSeverity);
        s.Fault = null; s.Trace.Clear();
        s.State.ShowStatus("title", "message", Severity.Other);
        Assert.Equal("/WelcomeWizard/title", s.State.MigrationStatusTitle);
        Assert.Equal("/WelcomeWizard/message", s.State.MigrationStatusMessage);
        Assert.Equal(Severity.Other, s.State.MigrationStatusSeverity);
        Assert.Equal(steps.Where((_, i) => i is 0 or 2 || i > at), s.Trace);
    }

    [Theory]
    [InlineData(null, "caller\n\nCODE")]
    [InlineData("", "caller\n\nCODE")]
    [InlineData(" \t\r\n", "caller\n\nCODE")]
    [InlineData("translated", "translated\n\nCODE")]
    public void ErrorChoosesOnlyNonWhitespaceLocalizedMessage(string? localized, string expected)
    {
        var s = new Subject { Resolve = key => key.Contains("MigrationError_", StringComparison.Ordinal) ? localized : "failed-title" };
        s.State.ShowError("caller", "CODE");
        Assert.Equal(expected, s.State.MigrationStatusMessage);
        Assert.Equal("failed-title", s.State.MigrationStatusTitle);
        Assert.Equal(Severity.Error, s.State.MigrationStatusSeverity);
        Assert.True(s.State.IsRetryMigrationVisible);
        Assert.Equal(new[] { "text:/WelcomeWizard/MigrationError_CODE", "text:/WelcomeWizard/MigrationFailedTitle",
            "event:MigrationStatusTitle", "event:MigrationStatusMessage", "event:MigrationStatusSeverity", "event:IsRetryMigrationVisible" }, s.Trace);
    }

    [Theory]
    [InlineData(null, null, "\n\n")]
    [InlineData(null, "CODE", "\n\nCODE")]
    [InlineData("caller", null, "caller\n\n")]
    [InlineData("a\nb", " c ", "a\nb\n\n c ")]
    public void ErrorKeepsRawArgumentsAndLiteralTwoNewlines(string? message, string? code, string expected)
    {
        var s = new Subject { Resolve = _ => null };
        s.State.ShowError(message, code);
        Assert.Null(s.State.MigrationStatusTitle);
        Assert.Equal(expected, s.State.MigrationStatusMessage);
        Assert.Equal("text:/WelcomeWizard/MigrationError_" + code, s.Trace[0]);
    }

    [Fact]
    public void ErrorUsesExplicitErrorSeverityRatherThanDefaultOrNumericMapping()
    {
        var state = new WelcomeMigrationPresentation<Severity>(Severity.Other, (Severity)(-99), _ => "text");
        state.ShowError("caller", "code");
        Assert.Equal((Severity)(-99), state.MigrationStatusSeverity);
    }

    [Fact]
    public void NestedErrorDuringTitleKeepsOuterLocalMessageAndCurrentEquality()
    {
        var s = new Subject { Resolve = key => key.EndsWith("_OUTER", StringComparison.Ordinal) ? "outer-local" :
            key.EndsWith("_INNER", StringComparison.Ordinal) ? "" : "failed-title" };
        bool nested = false;
        s.Observe = name =>
        {
            if (name != "MigrationStatusTitle" || nested) return;
            nested = true; s.State.ShowError("inner", "INNER");
        };
        s.State.ShowError("outer-caller", "OUTER");
        Assert.Equal("outer-local\n\nOUTER", s.State.MigrationStatusMessage);
        Assert.Equal(new[] { "text:/WelcomeWizard/MigrationError_OUTER", "text:/WelcomeWizard/MigrationFailedTitle",
            "event:MigrationStatusTitle", "text:/WelcomeWizard/MigrationError_INNER", "text:/WelcomeWizard/MigrationFailedTitle",
            "event:MigrationStatusMessage", "event:MigrationStatusSeverity", "event:IsRetryMigrationVisible",
            "event:MigrationStatusMessage" }, s.Trace);
    }

    [Fact]
    public void NestedStatusFromErrorLookupPrecedesOuterTitleAndDoesNotReplaceOuterLocal()
    {
        var s = new Subject();
        bool nested = false;
        s.Resolve = key =>
        {
            if (!nested && key == "/WelcomeWizard/MigrationError_CODE")
            {
                nested = true; s.State.ShowStatus("inner-title", "inner-message", Severity.Other);
                return "outer-local";
            }
            return key;
        };
        s.State.ShowError("caller", "CODE");
        Assert.Equal("/WelcomeWizard/MigrationFailedTitle", s.State.MigrationStatusTitle);
        Assert.Equal("outer-local\n\nCODE", s.State.MigrationStatusMessage);
        Assert.Equal(Severity.Error, s.State.MigrationStatusSeverity);
        Assert.True(s.State.IsRetryMigrationVisible);
        Assert.Equal(2, s.Trace.Count(x => x == "event:MigrationStatusTitle"));
        Assert.Equal(2, s.Trace.Count(x => x == "event:MigrationStatusMessage"));
        Assert.Equal(2, s.Trace.Count(x => x == "event:MigrationStatusSeverity"));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void ErrorFaultAtEachBoundaryKeepsPartialStateAndRetries(int at)
    {
        string[] steps = ["text:/WelcomeWizard/MigrationError_CODE", "text:/WelcomeWizard/MigrationFailedTitle",
            "event:MigrationStatusTitle", "event:MigrationStatusMessage", "event:MigrationStatusSeverity", "event:IsRetryMigrationVisible"];
        var s = new Subject { Resolve = key => key.EndsWith("_CODE", StringComparison.Ordinal) ? "localized" : "failed-title", Fault = steps[at] };
        Assert.Same(s.Failure, Assert.Throws<Sentinel>(() => s.State.ShowError("caller", "CODE")));
        Assert.Equal(steps.Take(at + 1), s.Trace);
        Assert.Equal(at >= 2 ? "failed-title" : "", s.State.MigrationStatusTitle);
        Assert.Equal(at >= 3 ? "localized\n\nCODE" : "", s.State.MigrationStatusMessage);
        Assert.Equal(at >= 4 ? Severity.Error : Severity.Initial, s.State.MigrationStatusSeverity);
        Assert.Equal(at >= 5, s.State.IsRetryMigrationVisible);
        s.Fault = null; s.Trace.Clear();
        s.State.ShowError("caller", "CODE");
        Assert.Equal("failed-title", s.State.MigrationStatusTitle);
        Assert.Equal("localized\n\nCODE", s.State.MigrationStatusMessage);
        Assert.Equal(Severity.Error, s.State.MigrationStatusSeverity);
        Assert.True(s.State.IsRetryMigrationVisible);
        Assert.Equal(steps.Where((_, i) => i < 2 || i > at), s.Trace);
    }

    [Fact]
    public void NestedErrorFailureRetainsInnerEffectsAndStopsOuterMessageLookup()
    {
        var s = new Subject { Fault = "event:MigrationStatusSeverity" };
        bool nested = false;
        s.Observe = name =>
        {
            if (name != "MigrationStatusTitle" || nested) return;
            nested = true; s.State.ShowError("inner", "INNER");
        };
        Assert.Same(s.Failure, Assert.Throws<Sentinel>(() => s.State.ShowStatus("outer-title", "outer-message", Severity.Other)));
        Assert.Equal("/WelcomeWizard/MigrationFailedTitle", s.State.MigrationStatusTitle);
        Assert.Equal("/WelcomeWizard/MigrationError_INNER\n\nINNER", s.State.MigrationStatusMessage);
        Assert.Equal(Severity.Error, s.State.MigrationStatusSeverity);
        Assert.False(s.State.IsRetryMigrationVisible);
        Assert.DoesNotContain("text:/WelcomeWizard/outer-message", s.Trace);
        s.Fault = null; s.Observe = null;
        s.State.ShowStatus("outer-title", "outer-message", Severity.Other);
        Assert.Equal("/WelcomeWizard/outer-message", s.State.MigrationStatusMessage);
    }

    [Fact]
    public void IndependentOwnersDoNotShareValuesCallbacksOrLists()
    {
        var first = new Subject { Resolve = _ => "first" };
        var second = new Subject { Resolve = _ => "second" };
        first.State.ShowError("caller", "CODE");
        first.State.IsMigrationMode = true;
        first.State.MigrationComponentItems = [new()];
        Assert.Empty(second.Trace);
        Assert.Equal(string.Empty, second.State.MigrationStatusTitle);
        Assert.Null(second.State.MigrationComponentItems);
        Assert.False(second.State.IsMigrationMode);
        Assert.False(second.State.IsRetryMigrationVisible);
        second.State.ShowStatus("title", "message", Severity.Other);
        Assert.Equal("second", second.State.MigrationStatusTitle);
        Assert.Equal("first", first.State.MigrationStatusTitle);
    }
}
