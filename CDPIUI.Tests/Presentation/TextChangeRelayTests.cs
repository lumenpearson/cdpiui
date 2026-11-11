using CDPIUI.Helper.UserExperience;

namespace CDPIUI.Tests.Presentation;

public class TextChangeRelayTests
{
    [Fact]
    public void ConstructionDoesNotReadOrPublishAnyPort()
    {
        static InvalidOperationException Unexpected() => new("eager callback");
        var relay = new TextChangeRelay(() => throw Unexpected(), () => throw Unexpected(),
            _ => throw Unexpected(), _ => throw Unexpected(), _ => throw Unexpected(),
            () => throw Unexpected(), () => throw Unexpected(), () => throw Unexpected(), _ => throw Unexpected());
        Assert.False(relay.IsUpdating);
    }

    [Fact]
    public void UserInputPublishesCurrentTextBeforeSchedulingDiagnostics()
    {
        var f = new Fixture(); f.Relay.OnTextChanged("input");
        Assert.Equal(["readonly", "write:input", "read:input", "accept:input", "rebuild", "read:input", "notify:input", "schedule"], f.Trace);
        Assert.Equal("input", f.Current); Assert.Equal("input", Assert.Single(f.Accepted));
        Assert.Equal("input", Assert.Single(f.Published)); Assert.False(f.Relay.IsUpdating);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReplacementUsesCapturedEditorResultAndImmediateDiagnostics(bool updateProperty)
    {
        var f = new Fixture { ReplaceReply = _ => "normalized" };
        f.Relay.ReplaceText("input", updateProperty);
        string[] expected = updateProperty
            ? ["replace:input", "write:normalized", "accept:normalized", "rebuild", "immediate", "notify:normalized"]
            : ["replace:input", "accept:normalized", "rebuild", "immediate", "notify:normalized"];
        Assert.Equal(expected, f.Trace); Assert.Equal(updateProperty ? "normalized" : "old", f.Current);
        Assert.Equal("normalized", Assert.Single(f.Accepted)); Assert.Equal("normalized", Assert.Single(f.Published));
        Assert.False(f.Relay.IsUpdating);
    }

    [Fact]
    public void ReadOnlyInputRestoresCurrentTextWithoutWritingPropertyOrScheduling()
    {
        var f = new Fixture { ReadOnly = true, Current = "stored", ReplaceReply = _ => "restored" };
        f.Relay.OnTextChanged("rejected");
        Assert.Equal(["readonly", "read:stored", "replace:stored", "accept:restored", "rebuild", "immediate", "notify:restored"], f.Trace);
        Assert.Equal("stored", f.Current); Assert.Equal("restored", Assert.Single(f.Accepted));
        Assert.Equal("restored", Assert.Single(f.Published)); Assert.False(f.Relay.IsUpdating);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IncomingNullIsNormalizedToEmptyForEitherPath(bool replace)
    {
        var f = new Fixture();
        if (replace) f.Relay.ReplaceText(null!); else f.Relay.OnTextChanged(null!);
        Assert.Equal("", f.Current); Assert.Equal("", Assert.Single(f.Accepted));
        Assert.Equal("", Assert.Single(f.Published));
        Assert.Contains(replace ? "replace:" : "write:", f.Trace);
    }

    [Fact]
    public void RuntimeNullEditorResultIsNotNormalizedAgain()
    {
        var f = new Fixture { ReplaceReply = _ => null! }; f.Relay.ReplaceText("input");
        Assert.Null(f.Current); Assert.Null(Assert.Single(f.Accepted)); Assert.Null(Assert.Single(f.Published));
        Assert.False(f.Relay.IsUpdating);
    }

    [Fact]
    public void UserInputReadsCurrentValueAgainAfterAcceptAndRebuildCallbacks()
    {
        var f = new Fixture();
        f.OnStep = step =>
        {
            if (step == "write:input") f.Current = "property-change";
            if (step == "accept:property-change") f.Current = "accepted";
            if (step == "rebuild") f.Current = "rebuilt";
        };
        f.Relay.OnTextChanged("input");
        Assert.Equal("property-change", Assert.Single(f.Accepted));
        Assert.Equal("rebuilt", Assert.Single(f.Published));
        Assert.Equal(["read:property-change", "read:rebuilt"], f.Trace.Where(x => x.StartsWith("read:")));
    }

    [Fact]
    public void ReplacementNotificationRetainsCapturedResultAcrossReentry()
    {
        var f = new Fixture { ReplaceReply = _ => "captured" };
        f.OnStep = step =>
        {
            if (step == "write:captured") f.Current = "property-change";
            if (step == "rebuild") f.Current = "rebuilt";
        };
        f.Relay.ReplaceText("input");
        Assert.Equal("rebuilt", f.Current); Assert.Equal("captured", Assert.Single(f.Accepted));
        Assert.Equal("captured", Assert.Single(f.Published)); Assert.DoesNotContain(f.Trace, x => x.StartsWith("read:"));
    }

    [Theory]
    [InlineData(false, "write:input")]
    [InlineData(true, "replace:input")]
    [InlineData(true, "write:input")]
    [InlineData(true, "accept:input")]
    public void SuppressedInputDoesNotEvenReadReadonlyState(bool replace, string step)
    {
        var f = new Fixture();
        f.OnStep = name =>
        {
            if (name != step) return;
            Assert.True(f.Relay.IsUpdating); int before = f.Trace.Count;
            f.Relay.OnTextChanged("nested"); Assert.Equal(before, f.Trace.Count);
        };
        if (replace) f.Relay.ReplaceText("input"); else f.Relay.OnTextChanged("input");
        Assert.Equal("input", Assert.Single(f.Published));
    }

    [Fact]
    public void NestedReplacementRetainsBooleanRatherThanDepthBasedSuppression()
    {
        var f = new Fixture();
        f.OnStep = step =>
        {
            if (step != "write:outer") return;
            f.Relay.ReplaceText("inner"); Assert.False(f.Relay.IsUpdating);
            f.Relay.OnTextChanged("nested");
        };
        f.Relay.OnTextChanged("outer");
        Assert.Equal(["inner", "nested", "nested"], f.Published);
        Assert.Equal(["inner", "nested", "nested"], f.Accepted);
        Assert.Equal("nested", f.Current); Assert.False(f.Relay.IsUpdating);
    }

    [Theory]
    [InlineData(false, "readonly", false)]
    [InlineData(false, "write:input", true)]
    [InlineData(false, "read:input", false)]
    [InlineData(false, "accept:input", false)]
    [InlineData(false, "rebuild", false)]
    [InlineData(false, "notify:input", false)]
    [InlineData(false, "schedule", false)]
    [InlineData(true, "replace:input", true)]
    [InlineData(true, "write:input", true)]
    [InlineData(true, "accept:input", true)]
    [InlineData(true, "rebuild", false)]
    [InlineData(true, "immediate", false)]
    [InlineData(true, "notify:input", false)]
    public void CallbackFailurePreservesExceptionIdentityAndOriginalGuardState(bool replace, string step, bool updating)
    {
        var f = new Fixture(); var error = new InvalidOperationException(step);
        f.OnStep = name => { if (name == step) throw error; };
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() =>
        {
            if (replace) f.Relay.ReplaceText("input"); else f.Relay.OnTextChanged("input");
        }));
        Assert.Equal(step, f.Trace.Last()); Assert.Equal(updating, f.Relay.IsUpdating);
        f.OnStep = null; int before = f.Trace.Count; f.Relay.OnTextChanged("next");
        if (updating) Assert.Equal(before, f.Trace.Count);
        else Assert.Equal("next", f.Published.Last());
    }

    [Fact]
    public void ReadOnlyCurrentReadFailureDoesNotOpenSuppression()
    {
        var f = new Fixture { ReadOnly = true }; var error = new InvalidOperationException("read");
        f.OnStep = step => { if (step == "read:old") throw error; };
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => f.Relay.OnTextChanged("input")));
        Assert.False(f.Relay.IsUpdating); Assert.Empty(f.Accepted); Assert.Empty(f.Published);
    }

    private sealed class Fixture
    {
        internal readonly TextChangeRelay Relay;
        internal readonly List<string> Trace = [], Accepted = [], Published = [];
        internal string Current = "old";
        internal bool ReadOnly;
        internal Action<string>? OnStep;
        internal Func<string, string>? ReplaceReply;
        internal Fixture()
        {
            Relay = new(() => { Step("readonly"); return ReadOnly; },
                () => { Step("read:" + Current); return Current; },
                text => { Current = text; Step("write:" + text); },
                text => { Step("replace:" + text); return ReplaceReply == null ? text : ReplaceReply(text); },
                text => { Accepted.Add(text); Step("accept:" + text); },
                () => Step("rebuild"), () => Step("immediate"), () => Step("schedule"),
                text => { Published.Add(text); Step("notify:" + text); });
        }
        private void Step(string value) { Trace.Add(value); OnStep?.Invoke(value); }
    }
}
