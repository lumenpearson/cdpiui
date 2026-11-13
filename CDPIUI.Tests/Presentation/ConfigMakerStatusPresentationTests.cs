using CDPIUI.ViewModels.ConfigMaker;

namespace CDPIUI.Tests.Presentation;

public sealed class ConfigMakerStatusPresentationTests
{
    private enum Severity { Information = 11, Warning = 27, Error = 49 }

    [Fact]
    public void Constructor_keeps_explicit_initial_values_and_does_not_call_ports()
    {
        var state = new ConfigMakerStatusPresentation<Severity>(Severity.Information,
            _ => throw new InvalidOperationException("title"),
            (_, _, _) => throw new InvalidOperationException("notice"),
            () => throw new InvalidOperationException("inline"));

        Assert.Equal(string.Empty, state.StatusTitle);
        Assert.Equal(string.Empty, state.StatusMessage);
        Assert.Equal(Severity.Information, state.StatusSeverity);
        Assert.False(state.IsStatusOpen);
    }

    [Fact]
    public void Constructor_only_stores_even_null_ports()
    {
        var state = new ConfigMakerStatusPresentation<Severity>((Severity)863, null!, null!, null!);
        Assert.Equal((Severity)863, state.StatusSeverity);
        Assert.Equal(string.Empty, state.StatusTitle);
        Assert.Equal(string.Empty, state.StatusMessage);
        Assert.False(state.IsStatusOpen);
    }

    [Fact]
    public void Setters_publish_current_values_with_owner_sender_and_suppress_equal_values()
    {
        var f = new Fixture();
        var seen = new List<(string?, object?)>();
        f.State.PropertyChanged += (sender, e) =>
        {
            Assert.Same(f.State, sender);
            seen.Add((e.PropertyName, e.PropertyName switch
            {
                nameof(f.State.StatusTitle) => f.State.StatusTitle,
                nameof(f.State.StatusMessage) => f.State.StatusMessage,
                nameof(f.State.StatusSeverity) => f.State.StatusSeverity,
                nameof(f.State.IsStatusOpen) => f.State.IsStatusOpen,
                _ => throw new InvalidOperationException("Unexpected property")
            }));
        };
        f.State.StatusTitle = "title";
        f.State.StatusTitle = new string("title".ToCharArray());
        f.State.StatusTitle = null;
        f.State.StatusTitle = null;
        f.State.StatusMessage = "message";
        f.State.StatusMessage = new string("message".ToCharArray());
        f.State.StatusMessage = null;
        f.State.StatusMessage = null;
        f.State.StatusSeverity = (Severity)999;
        f.State.StatusSeverity = (Severity)999;
        f.State.StatusSeverity = Severity.Information;
        f.State.IsStatusOpen = true;
        f.State.IsStatusOpen = true;
        f.State.IsStatusOpen = false;
        f.State.IsStatusOpen = false;

        Assert.Equal(new (string?, object?)[]
        {
            ("StatusTitle", "title"), ("StatusTitle", null),
            ("StatusMessage", "message"), ("StatusMessage", null),
            ("StatusSeverity", (Severity)999), ("StatusSeverity", Severity.Information),
            ("IsStatusOpen", true), ("IsStatusOpen", false)
        }, seen);
        Assert.Equal(0, f.InlineReads);
        Assert.DoesNotContain(f.Trace, item => item is "title" or "notice" or "inline");
    }

    [Theory]
    [InlineData(11)]
    [InlineData(27)]
    [InlineData(49)]
    [InlineData(999)]
    public void Show_resolves_then_notifies_then_reads_inline_before_ordered_publication(int severityValue)
    {
        var f = new Fixture();
        var severity = (Severity)severityValue;
        f.Notice = (value, title, message) =>
        {
            Assert.Equal(severity, value);
            Assert.Equal($"title:{severityValue}", title);
            Assert.Equal("message", message);
            Assert.Equal((string.Empty, string.Empty, Severity.Information, false), f.Values);
        };
        f.State.Show("message", severity);

        var expected = new List<string> { "title", "notice", "inline", "StatusTitle", "StatusMessage" };
        if (severity != Severity.Information) expected.Add("StatusSeverity");
        expected.Add("IsStatusOpen");
        Assert.Equal(expected, f.Trace);
        Assert.Equal(($"title:{severityValue}", "message", severity, true), f.Values);
        Assert.Equal(1, f.InlineReads);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, "message")]
    [InlineData("title", null)]
    [InlineData("", "")]
    [InlineData(" title ", " message ")]
    public void Raw_title_message_and_unknown_severity_reach_notice_and_inline_without_normalization(
        string? title, string? message)
    {
        var f = new Fixture { Resolve = _ => title };
        var calls = 0;
        f.Notice = (severity, actualTitle, actualMessage) =>
        {
            calls++;
            Assert.Equal((Severity)(-67), severity);
            Assert.Equal(title, actualTitle);
            Assert.Equal(message, actualMessage);
        };
        f.State.Show(message, (Severity)(-67));
        Assert.Equal((title, message, (Severity)(-67), true), f.Values);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Repeated_message_still_calls_all_ports_but_emits_no_equal_property_events()
    {
        var f = new Fixture { Resolve = _ => null };
        f.State.Show(null, Severity.Error);
        f.Clear();
        f.State.Show(null, Severity.Error);
        Assert.Equal(new[] { "title", "notice", "inline" }, f.Trace);
        Assert.Equal((null, null, Severity.Error, true), f.Values);
        Assert.Equal(1, f.InlineReads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Disabled_inline_only_closes_current_open_and_preserves_other_values(bool initiallyOpen)
    {
        var f = new Fixture { Inline = false };
        f.Seed(initiallyOpen);
        f.Notice = (_, _, _) => Assert.Equal(("old-title", "old-message", Severity.Warning, initiallyOpen), f.Values);
        f.State.Show("ignored-inline", Severity.Error);
        var expected = new List<string> { "title", "notice", "inline" };
        if (initiallyOpen) expected.Add("IsStatusOpen");
        Assert.Equal(expected, f.Trace);
        Assert.Equal(("old-title", "old-message", Severity.Warning, false), f.Values);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Notice_can_change_inline_before_its_single_current_read(bool initial, bool afterNotice)
    {
        var f = new Fixture { Inline = initial };
        f.Seed(true);
        f.Notice = (_, _, _) => f.Inline = afterNotice;
        f.State.Show("outer", Severity.Error);
        Assert.Equal(afterNotice
            ? ("title:49", "outer", Severity.Error, true)
            : ("old-title", "old-message", Severity.Warning, false), f.Values);
        Assert.Equal(new[] { "title", "notice", "inline" }, f.Trace.Take(3));
        Assert.Equal(1, f.InlineReads);
    }

    [Fact]
    public void Later_property_callback_changing_inline_does_not_interrupt_the_outer_sequence()
    {
        var f = new Fixture();
        f.Property = name => { if (name == "StatusTitle") f.Inline = false; };
        f.State.Show("outer", Severity.Error);
        Assert.False(f.Inline);
        Assert.Equal(("title:49", "outer", Severity.Error, true), f.Values);
        Assert.Equal(1, f.InlineReads);
        Assert.Equal(new[] { "title", "notice", "inline", "StatusTitle", "StatusMessage", "StatusSeverity", "IsStatusOpen" }, f.Trace);
    }

    [Fact]
    public void Title_resolution_can_replace_the_current_notice_subscriber_before_publication()
    {
        var f = new Fixture();
        var oldCalls = 0;
        var newCalls = 0;
        f.Notice = (_, _, _) => oldCalls++;
        f.Resolve = _ =>
        {
            f.Notice = (_, title, _) => { newCalls++; Assert.Equal("resolved", title); };
            return "resolved";
        };
        f.State.Show("message", Severity.Warning);
        Assert.Equal(0, oldCalls);
        Assert.Equal(1, newCalls);
        Assert.Equal("resolved", f.State.StatusTitle);
    }

    [Fact]
    public void Nested_notice_finishes_then_outer_publication_uses_its_own_arguments()
    {
        var f = new Fixture();
        f.Notice = (_, _, message) =>
        {
            if (message != "outer") return;
            f.State.Show("inner", Severity.Warning);
            Assert.Equal(("title:27", "inner", Severity.Warning, true), f.Values);
        };
        f.State.Show("outer", Severity.Error);
        Assert.Equal(("title:49", "outer", Severity.Error, true), f.Values);
        Assert.Equal(2, f.InlineReads);
        Assert.Equal(2, f.Trace.Count(item => item == "StatusTitle"));
        Assert.Equal(1, f.Trace.Count(item => item == "IsStatusOpen"));
    }

    [Fact]
    public void Equality_after_nested_notice_compares_with_current_inner_values()
    {
        var f = new Fixture { Resolve = _ => "shared-title" };
        f.Notice = (_, _, message) =>
        {
            if (message == "outer") f.State.Show("inner", Severity.Warning);
        };
        f.State.Show("outer", Severity.Error);
        Assert.Equal(1, f.Trace.Count(item => item == "StatusTitle"));
        Assert.Equal(2, f.Trace.Count(item => item == "StatusMessage"));
        Assert.Equal(2, f.Trace.Count(item => item == "StatusSeverity"));
        Assert.Equal(1, f.Trace.Count(item => item == "IsStatusOpen"));
        Assert.Equal(("shared-title", "outer", Severity.Error, true), f.Values);
    }

    [Fact]
    public void Nested_title_observer_can_overwrite_title_while_outer_later_arguments_continue()
    {
        var f = new Fixture();
        var nested = false;
        f.Property = name =>
        {
            if (nested || name != "StatusTitle") return;
            nested = true;
            Assert.Equal("title:49", f.State.StatusTitle);
            Assert.Equal(string.Empty, f.State.StatusMessage);
            f.State.Show("inner", Severity.Warning);
        };
        f.State.Show("outer", Severity.Error);
        Assert.Equal(("title:27", "outer", Severity.Error, true), f.Values);
        Assert.Equal(2, f.InlineReads);
    }

    [Fact]
    public void Nested_title_resolution_finishes_before_outer_notice_and_keeps_outer_local_title()
    {
        var f = new Fixture();
        f.Resolve = severity =>
        {
            if (severity == Severity.Error) f.State.Show("inner", Severity.Warning);
            return $"resolved:{(int)severity}";
        };
        f.Notice = (_, title, message) =>
        {
            if (message != "outer") return;
            Assert.Equal("resolved:49", title);
            Assert.Equal(("resolved:27", "inner", Severity.Warning, true), f.Values);
        };
        f.State.Show("outer", Severity.Error);
        Assert.Equal(("resolved:49", "outer", Severity.Error, true), f.Values);
    }

    [Theory]
    [InlineData("title")]
    [InlineData("notice")]
    [InlineData("inline")]
    public void Port_failure_preserves_identity_and_prevents_later_ports_and_field_assignments(string failing)
    {
        var f = new Fixture();
        f.Seed(false);
        var failure = new InvalidOperationException(failing);
        if (failing == "title") f.Resolve = _ => throw failure;
        if (failing == "notice") f.Notice = (_, _, _) => throw failure;
        if (failing == "inline") f.ReadInline = () => throw failure;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => f.State.Show("outer", Severity.Error)));
        Assert.Equal(("old-title", "old-message", Severity.Warning, false), f.Values);
        Assert.Equal(new[] { "title", "notice", "inline" }.Take(Array.IndexOf(new[] { "title", "notice", "inline" }, failing) + 1), f.Trace);
        f.Resolve = severity => $"title:{(int)severity}";
        f.Notice = null;
        f.ReadInline = null;
        f.State.Show("retry", Severity.Error);
        Assert.Equal(("title:49", "retry", Severity.Error, true), f.Values);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Each_property_failure_leaves_its_assigned_value_and_retry_resumes_from_partial_state(int index)
    {
        var names = new[] { "StatusTitle", "StatusMessage", "StatusSeverity", "IsStatusOpen" };
        var f = new Fixture();
        f.Seed(false);
        var failure = new InvalidOperationException(names[index]);
        f.Property = name => { if (name == names[index]) throw failure; };
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => f.State.Show("outer", Severity.Error)));
        Assert.Equal(("title:49", index >= 1 ? "outer" : "old-message",
            index >= 2 ? Severity.Error : Severity.Warning, index >= 3), f.Values);
        Assert.Equal(new[] { "title", "notice", "inline" }.Concat(names.Take(index + 1)), f.Trace);
        f.Property = null;
        f.Clear();
        f.State.Show("outer", Severity.Error);
        Assert.Equal(("title:49", "outer", Severity.Error, true), f.Values);
        Assert.Equal(new[] { "title", "notice", "inline" }.Concat(names.Skip(index + 1)), f.Trace);
    }

    [Fact]
    public void Disabled_close_observer_failure_keeps_closed_value_and_equal_retry_does_not_notify()
    {
        var f = new Fixture { Inline = false };
        f.Seed(true);
        var failure = new InvalidOperationException("close");
        f.Property = _ => throw failure;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => f.State.Show("outer", Severity.Error)));
        Assert.Equal(("old-title", "old-message", Severity.Warning, false), f.Values);
        f.Clear();
        f.State.Show("outer", Severity.Error);
        Assert.Equal(new[] { "title", "notice", "inline" }, f.Trace);
    }

    [Fact]
    public void Notice_failure_prevents_even_a_disabled_close()
    {
        var f = new Fixture { Inline = false };
        f.Seed(true);
        var failure = new InvalidOperationException("notice");
        f.Notice = (_, _, _) => throw failure;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => f.State.Show("outer", Severity.Error)));
        Assert.True(f.State.IsStatusOpen);
        Assert.Equal(0, f.InlineReads);
    }

    [Fact]
    public void Notice_failure_after_nested_show_keeps_inner_effects_without_outer_flag_read()
    {
        var f = new Fixture();
        var failure = new InvalidOperationException("outer-notice");
        f.Notice = (_, _, message) =>
        {
            if (message != "outer") return;
            f.State.Show("inner", Severity.Warning);
            throw failure;
        };
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => f.State.Show("outer", Severity.Error)));
        Assert.Equal(("title:27", "inner", Severity.Warning, true), f.Values);
        Assert.Equal(1, f.InlineReads);
    }

    [Fact]
    public void Property_failure_after_nested_show_preserves_inner_changes_and_stops_outer_tail()
    {
        var f = new Fixture();
        var failure = new InvalidOperationException("outer-message");
        var nested = false;
        f.Property = name =>
        {
            if (nested || name != "StatusMessage") return;
            nested = true;
            f.State.Show("inner", Severity.Warning);
            throw failure;
        };
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => f.State.Show("outer", Severity.Error)));
        Assert.Equal(("title:27", "inner", Severity.Warning, true), f.Values);
        Assert.Equal(2, f.InlineReads);
    }

    [Fact]
    public void Instances_have_independent_values_ports_flags_and_events()
    {
        var first = new Fixture();
        var second = new Fixture { Inline = false };
        first.State.Show("one", Severity.Error);
        Assert.Empty(second.Trace);
        Assert.Equal((string.Empty, string.Empty, Severity.Information, false), second.Values);
        second.State.Show("two", Severity.Warning);
        Assert.Equal(("title:49", "one", Severity.Error, true), first.Values);
        Assert.Equal((string.Empty, string.Empty, Severity.Information, false), second.Values);
        Assert.Equal(1, first.InlineReads);
        Assert.Equal(1, second.InlineReads);
    }

    private sealed class Fixture
    {
        internal readonly ConfigMakerStatusPresentation<Severity> State;
        internal readonly List<string> Trace = [];
        internal Func<Severity, string?> Resolve = severity => $"title:{(int)severity}";
        internal Action<Severity, string?, string?>? Notice;
        internal Func<bool>? ReadInline;
        internal Action<string>? Property;
        internal bool Inline = true;
        internal int InlineReads;
        internal (string?, string?, Severity, bool) Values =>
            (State.StatusTitle, State.StatusMessage, State.StatusSeverity, State.IsStatusOpen);

        internal Fixture()
        {
            State = new(Severity.Information,
                severity => { Trace.Add("title"); return Resolve(severity); },
                (severity, title, message) => { Trace.Add("notice"); Notice?.Invoke(severity, title, message); },
                () => { Trace.Add("inline"); InlineReads++; return ReadInline is null ? Inline : ReadInline(); });
            State.PropertyChanged += (sender, e) =>
            {
                Assert.Same(State, sender);
                Trace.Add(e.PropertyName!);
                Property?.Invoke(e.PropertyName!);
            };
        }

        internal void Seed(bool open)
        {
            State.StatusTitle = "old-title";
            State.StatusMessage = "old-message";
            State.StatusSeverity = Severity.Warning;
            State.IsStatusOpen = open;
            Clear();
        }

        internal void Clear() { Trace.Clear(); InlineReads = 0; }
    }
}
