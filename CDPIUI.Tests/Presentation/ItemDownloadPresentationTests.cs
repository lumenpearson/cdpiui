using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using CDPIUI.ViewModels.Store;
using Xunit;

namespace CDPIUI.Tests.Presentation;

public sealed class ItemDownloadPresentationTests
{
    private sealed class Sentinel : Exception;
    private static ItemDownloadPresentation New(Func<string, string>? text = null) => new(text ?? (key => "loc:" + key));
    private static List<string> Observe(ItemDownloadPresentation subject)
    {
        var events = new List<string>();
        subject.PropertyChanged += (sender, args) => { Assert.Same(subject, sender); events.Add(args.PropertyName!); };
        return events;
    }
    private static object?[] Values(ItemDownloadPresentation s) => [s.IsActionVisible, s.IsDownloadStatusVisible,
        s.IsErrorStatusVisible, s.IsStopEnabled, s.StatusText, s.IsSpeedVisible, s.SpeedText, s.StatusTip,
        s.IsProgressIndeterminate, s.ProgressValue, s.ErrorName, s.ErrorHappens];
    private static void DirtyDownload(ItemDownloadPresentation s)
    {
        s.IsStopEnabled = false; s.IsProgressIndeterminate = false; s.IsErrorStatusVisible = true;
        s.IsActionVisible = true; s.IsDownloadStatusVisible = false;
    }
    [Fact]
    public void Constructor_keeps_cold_values_without_localizing()
    {
        var subject = new ItemDownloadPresentation(_ => throw new InvalidOperationException("Cold constructor localized."));
        Assert.True(subject.IsActionVisible);
        Assert.False(subject.IsDownloadStatusVisible);
        Assert.False(subject.IsErrorStatusVisible);
        Assert.True(subject.IsStopEnabled);
        Assert.Equal(string.Empty, subject.StatusText);
        Assert.False(subject.IsSpeedVisible);
        Assert.Equal(string.Empty, subject.SpeedText);
        Assert.Null(subject.StatusTip);
        Assert.True(subject.IsProgressIndeterminate);
        Assert.Equal(0d, subject.ProgressValue);
        Assert.Equal("ERR_FLASHLIGHT_BISY", subject.ErrorName);
        Assert.False(subject.ErrorHappens);
    }

    public static IEnumerable<object[]> Setters()
    {
        yield return ["IsActionVisible", false]; yield return ["IsDownloadStatusVisible", true];
        yield return ["IsErrorStatusVisible", true]; yield return ["IsStopEnabled", false];
        yield return ["StatusText", "status"]; yield return ["IsSpeedVisible", true];
        yield return ["SpeedText", "speed"]; yield return ["StatusTip", "tip"];
        yield return ["IsProgressIndeterminate", false]; yield return ["ProgressValue", 13.5d];
        yield return ["ErrorName", "error"];
    }
    [Theory, MemberData(nameof(Setters))]
    public void Setter_assigns_before_event_and_preserves_its_equality_rule(string name, object value)
    {
        var s = New(_ => throw new Sentinel()); var property = typeof(ItemDownloadPresentation).GetProperty(name)!;
        int count = 0;
        s.PropertyChanged += (sender, args) =>
        {
            Assert.Same(s, sender); Assert.Equal(name, args.PropertyName);
            Assert.Equal(value, property.GetValue(s)); count++;
        };
        property.SetValue(s, value); property.SetValue(s, value);
        Assert.Equal(name == "StatusTip" ? 2 : 1, count);
    }

    [Theory]
    [InlineData("StatusText"), InlineData("SpeedText"), InlineData("StatusTip"), InlineData("ErrorName")]
    public void String_setters_retain_raw_null_and_tip_always_notifies(string name)
    {
        var s = New(); var property = typeof(ItemDownloadPresentation).GetProperty(name)!; var events = Observe(s);
        property.SetValue(s, null); property.SetValue(s, null);
        Assert.Null(property.GetValue(s)); Assert.Equal(name == "StatusTip" ? 2 : 1, events.Count);
        Assert.All(events, e => Assert.Equal(name, e));
    }

    [Fact]
    public void Error_latch_is_plain_state_with_no_property_event_or_dependency_call()
    {
        var s = New(_ => throw new Sentinel()); var events = Observe(s);
        s.ErrorHappens = true; s.ErrorHappens = true; s.ErrorHappens = false; s.ErrorHappens = false;
        Assert.False(s.ErrorHappens); Assert.Empty(events);
    }

    [Fact]
    public void ShowDownloadState_preserves_five_ordered_partial_publications_and_equality()
    {
        var s = New(_ => throw new Sentinel()); DirtyDownload(s);
        var trace = new List<(string?, bool, bool, bool, bool, bool)>();
        s.PropertyChanged += (_, e) => trace.Add((e.PropertyName, s.IsStopEnabled, s.IsProgressIndeterminate,
            s.IsErrorStatusVisible, s.IsActionVisible, s.IsDownloadStatusVisible));
        s.ShowDownloadState(); s.ShowDownloadState();
        Assert.Equal(new (string?, bool, bool, bool, bool, bool)[] {
            ("IsStopEnabled", true,false,true,true,false), ("IsProgressIndeterminate",true,true,true,true,false),
            ("IsErrorStatusVisible",true,true,false,true,false), ("IsActionVisible",true,true,false,false,false),
            ("IsDownloadStatusVisible",true,true,false,false,true) }, trace);
    }

    public static IEnumerable<object?[]> Stages()
    {
        foreach (bool initial in new[] { false, true })
        {
            yield return ["GETR", "GettingReady", true, false, initial];
            yield return ["END", "Finishing", true, false, initial];
            yield return ["Downloading", "Downloading", false, true, initial];
            yield return ["Extracting", "Installing", true, false, initial];
            yield return ["ErrorHappens", "ErrorHappens", null, false, initial];
            yield return ["Completed", "Finishing", true, false, initial];
            yield return ["CANC", "Cancel", true, false, initial];
            yield return ["ConnectingToService", "ConnectingToService", true, false, initial];
            yield return [null, "QueueWaiting", true, false, initial];
            yield return ["", "", true, false, initial];
            yield return ["unknown.raw", "unknown.raw", true, false, initial];
        }
    }
    [Theory, MemberData(nameof(Stages))]
    public void Stage_matrix_preserves_keys_order_and_values_it_does_not_assign(string? stage, string key,
        bool? indeterminate, bool showSpeed, bool initial)
    {
        var trace = new List<string>(); var s = New(k => { trace.Add("text:" + k); return "loc:" + k; });
        s.IsProgressIndeterminate = initial; s.IsSpeedVisible = initial; s.StatusTip = "old-tip"; s.StatusText = "old-header";
        s.PropertyChanged += (_, e) => trace.Add(e.PropertyName!); s.ApplyStage(stage);
        var expected = new List<string> { "text:" + key };
        if (key == "ConnectingToService") { expected.Add("text:ConnectingToServiceTip"); expected.Add("StatusTip"); }
        if (indeterminate.HasValue && initial != indeterminate.Value) expected.Add("IsProgressIndeterminate");
        if (showSpeed && !initial) expected.Add("IsSpeedVisible"); expected.Add("StatusText");
        Assert.Equal(expected, trace); Assert.Equal(indeterminate ?? initial, s.IsProgressIndeterminate);
        Assert.Equal(showSpeed || initial, s.IsSpeedVisible);
        Assert.Equal(key == "ConnectingToService" ? "loc:ConnectingToServiceTip" : "old-tip", s.StatusTip);
        Assert.Equal("loc:" + key, s.StatusText); Assert.False(s.ErrorHappens);
    }

    [Fact]
    public void Repeated_connecting_stage_localizes_again_and_always_publishes_equal_tip()
    {
        var calls = new List<string>(); var s = New(k => { calls.Add(k); return k; }); var events = Observe(s);
        s.ApplyStage("ConnectingToService"); s.ApplyStage("ConnectingToService");
        Assert.Equal(new[] { "ConnectingToService", "ConnectingToServiceTip", "ConnectingToService", "ConnectingToServiceTip" }, calls);
        Assert.Equal(new[] { "StatusTip", "StatusText", "StatusTip" }, events);
    }

    [Fact]
    public void OnStageChanged_preserves_show_reset_localize_and_final_header_order()
    {
        var trace = new List<string>(); var s = New(k => { trace.Add("text:" + k); return k; });
        DirtyDownload(s); s.IsSpeedVisible = true; s.PropertyChanged += (_, e) => trace.Add(e.PropertyName!);
        s.OnStageChanged("Downloading");
        Assert.Equal(new[] { "IsStopEnabled", "IsProgressIndeterminate", "IsErrorStatusVisible", "IsActionVisible",
            "IsDownloadStatusVisible", "IsSpeedVisible", "text:Downloading", "IsProgressIndeterminate", "IsSpeedVisible", "StatusText" }, trace);
    }

    [Theory]
    [InlineData("stage"), InlineData("progress"), InlineData("time"), InlineData("speed")]
    public void Error_gate_returns_before_all_writes_and_localization(string operation)
    {
        var s = New(_ => throw new Sentinel()); s.ErrorHappens = true; var before = Values(s); var events = Observe(s);
        switch (operation)
        {
            case "stage": s.OnStageChanged("Downloading"); break;
            case "progress": s.OnProgressChanged(45); break;
            case "time": s.OnTimeRemainingChanged(TimeSpan.FromMinutes(2)); break;
            case "speed": s.OnSpeedChanged("ignored"); break;
        }
        Assert.Equal(before, Values(s)); Assert.Empty(events);
    }

    [Fact]
    public void Stage_entry_does_not_recheck_error_after_show_callback_sets_it()
    {
        var s = New(); DirtyDownload(s); bool nested = false;
        s.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "IsStopEnabled" && !nested) { nested = true; s.OnInstallingError("nested"); }
        };
        s.OnStageChanged("Downloading");
        Assert.True(s.ErrorHappens); Assert.Equal("nested", s.ErrorName); Assert.False(s.IsErrorStatusVisible);
        Assert.False(s.IsActionVisible); Assert.True(s.IsDownloadStatusVisible); Assert.True(s.IsSpeedVisible);
        Assert.False(s.IsProgressIndeterminate); Assert.Equal("loc:Downloading", s.StatusText);
        s.OnProgressChanged(99); Assert.Equal(0d, s.ProgressValue);
    }

    [Fact]
    public void Progress_entry_finishes_value_after_an_indeterminate_observer_sets_error()
    {
        var s = New(); s.PropertyChanged += (_, e) =>
        { if (e.PropertyName == "IsProgressIndeterminate") s.OnInstallingError("nested"); };
        s.OnProgressChanged(13.5);
        Assert.True(s.ErrorHappens); Assert.False(s.IsProgressIndeterminate); Assert.Equal(13.5, s.ProgressValue);
    }

    [Theory]
    [InlineData(double.NaN), InlineData(double.PositiveInfinity), InlineData(double.NegativeInfinity)]
    [InlineData(-1d), InlineData(0d), InlineData(101d), InlineData(123.5d)]
    public void Progress_preserves_raw_values_and_repeated_double_equality(double value)
    {
        var s = New(_ => throw new Sentinel()); var events = Observe(s); s.OnProgressChanged(value);
        if (double.IsNaN(value)) Assert.True(double.IsNaN(s.ProgressValue)); else Assert.Equal(value, s.ProgressValue);
        Assert.False(s.IsProgressIndeterminate);
        Assert.Equal(value == 0d ? new[] { "IsProgressIndeterminate" } : new[] { "IsProgressIndeterminate", "ProgressValue" }, events);
        int count = events.Count; s.OnProgressChanged(value); Assert.Equal(count, events.Count);
    }

    [Theory]
    [InlineData(-60d, "soon"), InlineData(0d, "soon"), InlineData(59.999d, "soon")]
    [InlineData(60d, "left 1"), InlineData(119.999d, "left 1"), InlineData(120d, "left 2"), InlineData(3601d, "left 60")]
    public void Time_threshold_and_truncated_minutes_keep_unconditional_tip_events(double seconds, string expected)
    {
        var keys = new List<string>(); var s = New(k => { keys.Add(k); return k == "/UIHelper/LeftMinutes" ? "left {0}" : "soon"; });
        var events = Observe(s); s.OnTimeRemainingChanged(TimeSpan.FromSeconds(seconds)); s.OnTimeRemainingChanged(TimeSpan.FromSeconds(seconds));
        Assert.Equal(expected, s.StatusTip); Assert.Equal(new[] { "StatusTip", "StatusTip" }, events);
        Assert.Equal(new[] { seconds >= 60 ? "/UIHelper/LeftMinutes" : "/UIHelper/LeftSmall", seconds >= 60 ? "/UIHelper/LeftMinutes" : "/UIHelper/LeftSmall" }, keys);
    }

    [Fact]
    public void Time_format_uses_culture_after_localization()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var s = New(_ => { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE"); return "{0:N1}"; });
            s.OnTimeRemainingChanged(TimeSpan.FromMinutes(1234.75)); Assert.Equal("1.234,0", s.StatusTip);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Fact]
    public void Time_localization_reentry_keeps_outer_time_and_observes_current_latch_without_recheck()
    {
        ItemDownloadPresentation? s = null; bool nested = false;
        s = New(_ =>
        {
            if (!nested) { nested = true; s!.OnInstallingError("nested"); }
            return "left {0}";
        });
        s.OnTimeRemainingChanged(TimeSpan.FromMinutes(7.9));
        Assert.True(s.ErrorHappens); Assert.Equal("left 7", s.StatusTip);
    }

    [Fact]
    public void Speed_retains_raw_null_and_equal_value_suppression()
    {
        var s = New(_ => throw new Sentinel()); var events = Observe(s);
        s.OnSpeedChanged(null); s.OnSpeedChanged(null); s.OnSpeedChanged(" 2 MB/s, "); s.OnSpeedChanged(" 2 MB/s, ");
        Assert.Equal(" 2 MB/s, ", s.SpeedText); Assert.Equal(new[] { "SpeedText", "SpeedText" }, events);
    }

    [Fact]
    public void Error_assigns_four_display_values_before_its_own_terminal_latch()
    {
        var s = New(_ => throw new Sentinel()); s.IsDownloadStatusVisible = true;
        var events = new List<string>();
        s.PropertyChanged += (_, e) => { events.Add(e.PropertyName!); Assert.False(s.ErrorHappens); };
        s.OnInstallingError("outer");
        Assert.Equal(new[] { "IsErrorStatusVisible", "IsActionVisible", "IsDownloadStatusVisible", "ErrorName" }, events);
        Assert.True(s.ErrorHappens); Assert.Equal("outer", s.ErrorName);
    }

    [Fact]
    public void Error_has_no_entry_guard_and_can_publish_raw_null_while_already_latched()
    {
        var s = New(); s.ErrorHappens = true; s.IsDownloadStatusVisible = true; var events = Observe(s);
        s.OnInstallingError(null);
        Assert.Equal(new[] { "IsErrorStatusVisible", "IsActionVisible", "IsDownloadStatusVisible", "ErrorName" }, events);
        Assert.Null(s.ErrorName); Assert.True(s.ErrorHappens);
    }

    [Fact]
    public void Error_observer_can_run_guarded_calls_before_the_outer_terminal_assignment()
    {
        var s = New(); s.PropertyChanged += (_, e) =>
        { if (e.PropertyName == "IsErrorStatusVisible") { Assert.False(s.ErrorHappens); s.OnProgressChanged(71); s.OnSpeedChanged("inner-speed"); } };
        s.OnInstallingError("outer");
        Assert.Equal(71d, s.ProgressValue); Assert.Equal("inner-speed", s.SpeedText); Assert.True(s.ErrorHappens);
    }

    [Theory]
    [InlineData(false), InlineData(true)]
    public void Nested_error_changes_current_latch_before_later_outer_header_and_survives_outer_failure(bool throwOuter)
    {
        var s = New(); s.IsDownloadStatusVisible = true; bool nested = false; var error = new Sentinel();
        var names = new List<(string?, bool)>();
        s.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "IsErrorStatusVisible" && !nested) { nested = true; s.OnInstallingError("inner"); }
            if (e.PropertyName == "ErrorName")
            {
                names.Add((s.ErrorName, s.ErrorHappens));
                if (s.ErrorName == "outer")
                {
                    Assert.True(s.ErrorHappens); s.OnSpeedChanged("blocked");
                    if (throwOuter) throw error;
                }
            }
        };
        if (throwOuter) Assert.Same(error, Assert.Throws<Sentinel>(() => s.OnInstallingError("outer")));
        else s.OnInstallingError("outer");
        Assert.Equal(new (string?, bool)[] { ("inner", false), ("outer", true) }, names);
        Assert.Equal("outer", s.ErrorName); Assert.True(s.ErrorHappens); Assert.Equal(string.Empty, s.SpeedText);
    }

    [Theory]
    [InlineData(0), InlineData(1), InlineData(2), InlineData(3)]
    public void Error_observer_failure_keeps_partial_fields_and_unwritten_latch_then_retries(int fault)
    {
        var s = New(); s.IsDownloadStatusVisible = true; var events = Observe(s); var error = new Sentinel();
        string[] names = ["IsErrorStatusVisible", "IsActionVisible", "IsDownloadStatusVisible", "ErrorName"];
        PropertyChangedEventHandler fail = (_, e) => { if (e.PropertyName == names[fault]) throw error; };
        s.PropertyChanged += fail; Assert.Same(error, Assert.Throws<Sentinel>(() => s.OnInstallingError("new")));
        Assert.True(s.IsErrorStatusVisible); Assert.Equal(fault < 1, s.IsActionVisible); Assert.Equal(fault < 2, s.IsDownloadStatusVisible);
        Assert.Equal(fault == 3 ? "new" : "ERR_FLASHLIGHT_BISY", s.ErrorName); Assert.False(s.ErrorHappens);
        Assert.Equal(names.Take(fault + 1), events); s.PropertyChanged -= fail; s.OnInstallingError("new");
        Assert.Equal(names, events); Assert.True(s.ErrorHappens);
    }

    [Fact]
    public void Nested_stage_keeps_outer_local_header_and_inner_unassigned_state()
    {
        var s = New(); bool nested = false;
        s.PropertyChanged += (_, e) => { if (e.PropertyName == "IsProgressIndeterminate" && !nested) { nested = true; s.ApplyStage("ErrorHappens"); } };
        var events = Observe(s); s.ApplyStage("Downloading");
        Assert.Equal("loc:Downloading", s.StatusText); Assert.False(s.IsProgressIndeterminate); Assert.True(s.IsSpeedVisible);
        Assert.Equal(new[] { "StatusText", "IsProgressIndeterminate", "IsSpeedVisible", "StatusText" }, events);
    }

    [Fact]
    public void Connecting_second_localization_failure_leaves_tip_indeterminate_and_header_untouched()
    {
        var error = new Sentinel(); var calls = new List<string>();
        var s = New(k => { calls.Add(k); if (k == "ConnectingToServiceTip") throw error; return k; });
        s.IsProgressIndeterminate = false; s.StatusTip = "old-tip"; s.StatusText = "old-header"; var events = Observe(s);
        Assert.Same(error, Assert.Throws<Sentinel>(() => s.ApplyStage("ConnectingToService")));
        Assert.Equal(new[] { "ConnectingToService", "ConnectingToServiceTip" }, calls);
        Assert.Empty(events); Assert.False(s.IsProgressIndeterminate); Assert.Equal("old-tip", s.StatusTip); Assert.Equal("old-header", s.StatusText);
    }

    [Fact]
    public void Connecting_tip_observer_failure_retains_assignment_and_equal_tip_notifies_on_retry()
    {
        var s = New(); s.IsProgressIndeterminate = false; s.StatusText = "old"; var events = Observe(s); var error = new Sentinel();
        PropertyChangedEventHandler fail = (_, e) => { if (e.PropertyName == "StatusTip") throw error; };
        s.PropertyChanged += fail; Assert.Same(error, Assert.Throws<Sentinel>(() => s.ApplyStage("ConnectingToService")));
        Assert.Equal("loc:ConnectingToServiceTip", s.StatusTip); Assert.False(s.IsProgressIndeterminate); Assert.Equal("old", s.StatusText);
        s.PropertyChanged -= fail; s.ApplyStage("ConnectingToService");
        Assert.Equal(new[] { "StatusTip", "StatusTip", "IsProgressIndeterminate", "StatusText" }, events);
    }

    [Fact]
    public void Download_indeterminate_observer_failure_stops_speed_and_header_then_retries()
    {
        var s = New(); var events = Observe(s); var error = new Sentinel();
        PropertyChangedEventHandler fail = (_, e) => { if (e.PropertyName == "IsProgressIndeterminate") throw error; };
        s.PropertyChanged += fail; Assert.Same(error, Assert.Throws<Sentinel>(() => s.ApplyStage("Downloading")));
        Assert.False(s.IsProgressIndeterminate); Assert.False(s.IsSpeedVisible); Assert.Equal(string.Empty, s.StatusText);
        s.PropertyChanged -= fail; s.ApplyStage("Downloading");
        Assert.Equal(new[] { "IsProgressIndeterminate", "IsSpeedVisible", "StatusText" }, events);
    }

    [Fact]
    public void Stage_show_observer_failure_prevents_reset_and_localization()
    {
        var s = New(_ => throw new InvalidOperationException("Text should be later.")); DirtyDownload(s); s.IsSpeedVisible = true;
        var error = new Sentinel(); s.PropertyChanged += (_, _) => throw error;
        Assert.Same(error, Assert.Throws<Sentinel>(() => s.OnStageChanged("Downloading")));
        Assert.True(s.IsStopEnabled); Assert.False(s.IsProgressIndeterminate); Assert.True(s.IsErrorStatusVisible);
        Assert.True(s.IsActionVisible); Assert.False(s.IsDownloadStatusVisible); Assert.True(s.IsSpeedVisible);
    }

    [Fact]
    public void Progress_observer_failure_keeps_old_value_then_retries_after_equality_suppression()
    {
        var s = New(); s.ProgressValue = 5; var events = Observe(s); var error = new Sentinel();
        PropertyChangedEventHandler fail = (_, e) => { if (e.PropertyName == "IsProgressIndeterminate") throw error; };
        s.PropertyChanged += fail; Assert.Same(error, Assert.Throws<Sentinel>(() => s.OnProgressChanged(7.5)));
        Assert.False(s.IsProgressIndeterminate); Assert.Equal(5d, s.ProgressValue); s.PropertyChanged -= fail; s.OnProgressChanged(7.5);
        Assert.Equal(new[] { "IsProgressIndeterminate", "ProgressValue" }, events); Assert.Equal(7.5, s.ProgressValue);
    }

    [Theory]
    [InlineData(true), InlineData(false)]
    public void Time_dependency_failure_does_not_publish_tip(bool localizationFailure)
    {
        var error = new Sentinel(); var s = New(_ => localizationFailure ? throw error : "{1}"); s.StatusTip = "old"; var events = Observe(s);
        if (localizationFailure) Assert.Same(error, Assert.Throws<Sentinel>(() => s.OnTimeRemainingChanged(TimeSpan.FromMinutes(2))));
        else Assert.Throws<FormatException>(() => s.OnTimeRemainingChanged(TimeSpan.FromMinutes(2)));
        Assert.Equal("old", s.StatusTip); Assert.Empty(events);
    }

    [Fact]
    public void Independent_owners_do_not_share_values_events_or_latch()
    {
        var first = New(); var second = New(); var events = Observe(second);
        first.OnInstallingError("first"); first.StatusTip = "tip"; first.ProgressValue = 77;
        Assert.False(second.ErrorHappens); Assert.Equal("ERR_FLASHLIGHT_BISY", second.ErrorName);
        Assert.Null(second.StatusTip); Assert.Equal(0d, second.ProgressValue); Assert.Empty(events);
    }
}
