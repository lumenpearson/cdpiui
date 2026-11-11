using System.Collections.ObjectModel;
using CDPIUI.Core.LScript;
using CDPIUI.ViewModels.CreateConfig;
using CDPIUI.Views.CreateConfigHelper;

namespace CDPIUI.Tests.Presentation;

public sealed class CreateConfigConditionDraftTests
{
    [Fact]
    public void ConstructorOnlyStoresDependenciesAndKeepsEmptyFields()
    {
        var f = new Fixture();
        Assert.Empty(f.Trace); Assert.Empty(f.Events); Assert.Empty(f.Items);
        Assert.Equal("", f.Draft.ConditionName);
        Assert.Equal("", f.Draft.ConditionDescription);
        Assert.Equal("", f.Draft.ConditionOnValue);
        Assert.Equal("", f.Draft.ConditionOffValue);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("description")]
    [InlineData("on")]
    [InlineData("off")]
    public void SettersNormalizeNullAndSuppressEqualValuesWithOriginalNotifications(string property)
    {
        var f = new Fixture();
        void Set(string? value)
        {
            switch (property)
            {
                case "name": f.Draft.ConditionName = value!; break;
                case "description": f.Draft.ConditionDescription = value!; break;
                case "on": f.Draft.ConditionOnValue = value!; break;
                default: f.Draft.ConditionOffValue = value!; break;
            }
        }
        Set(null); Assert.Empty(f.Events);
        Set("x");
        string own = property switch { "name" => "ConditionName", "description" => "ConditionDescription", "on" => "ConditionOnValue", _ => "ConditionOffValue" };
        Assert.Equal(property == "description" ? new[] { own } : new[] { own, "ConditionPreview", "IsConditionSaveAvailable" }, f.Events);
        f.Events.Clear(); Set("x"); Assert.Empty(f.Events);
        Set(null);
        Assert.Equal(property == "description" ? new[] { own } : new[] { own, "ConditionPreview", "IsConditionSaveAvailable" }, f.Events);
        Assert.Empty(f.Trace);
    }

    [Theory]
    [InlineData(false, "on", "off", false)]
    [InlineData(true, "", "off", false)]
    [InlineData(true, "on", "", false)]
    [InlineData(true, " ", "\t", true)]
    public void AvailabilityNormalizesOnlySpacesAndKeepsOriginalEmptyValueRules(bool valid, string on, string off, bool expected)
    {
        var f = new Fixture(); f.Ready(" A B ", on, off); f.Validate = _ => valid;
        Assert.Equal(expected, f.Draft.IsConditionSaveAvailable);
        Assert.Equal(new[] { "validate:AB" }, f.Trace);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreviewIsFreshAndUsesOnlyTheChosenFormatterOrLocalizationBranch(bool valid)
    {
        var f = new Fixture(); f.Ready(" A B ", "on", "off"); f.Validate = _ => valid;
        Assert.Equal(valid ? "AB[on|off]" : "localized:NothingToPreview", f.Draft.ConditionPreview);
        Assert.Equal(valid ? new[] { "validate:AB", "format:AB:on:off" } : new[] { "validate:AB", "text:NothingToPreview" }, f.Trace);
        f.Trace.Clear();
        f.Localize = _ => "changed"; f.Format = (_, _, _) => "next";
        Assert.Equal(valid ? "next" : "changed", f.Draft.ConditionPreview);
    }

    [Fact]
    public void PreviewReadsCurrentValuesAgainAfterValidationCallback()
    {
        var f = new Fixture(); f.Ready("AB", "before-on", "before-off");
        f.Validate = _ =>
        {
            f.Draft.ConditionName = " C D "; f.Draft.ConditionOnValue = "after-on"; f.Draft.ConditionOffValue = "after-off";
            return true;
        };
        Assert.Equal("CD[after-on|after-off]", f.Draft.ConditionPreview);
        Assert.Equal(new[] { "validate:AB", "format:CD:after-on:after-off" }, f.Trace);
    }

    [Fact]
    public void ResetUsesOriginalSetterOrderAndAlwaysPublishesFinalPreview()
    {
        var f = new Fixture(); f.Ready("AB", "on", "off"); f.Draft.ConditionDescription = "desc"; f.Events.Clear();
        f.Draft.ResetConditionDraft();
        Assert.Equal(new[] { "ConditionName", "ConditionPreview", "IsConditionSaveAvailable", "ConditionDescription",
            "ConditionOnValue", "ConditionPreview", "IsConditionSaveAvailable", "ConditionOffValue", "ConditionPreview", "IsConditionSaveAvailable", "ConditionPreview" }, f.Events);
        Assert.Empty(f.Trace);
        f.Events.Clear(); f.Draft.ResetConditionDraft();
        Assert.Equal(new[] { "ConditionPreview" }, f.Events);
    }

    [Fact]
    public void DraftNotificationsRemainReentrantBeforeOuterDerivedNotifications()
    {
        var f = new Fixture(); bool entered = false;
        f.Draft.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != "ConditionName" || entered) return;
            entered = true; f.Draft.ConditionOnValue = "nested";
        };
        f.Draft.ConditionName = "AB";
        Assert.Equal(new[] { "ConditionName", "ConditionOnValue", "ConditionPreview", "IsConditionSaveAvailable", "ConditionPreview", "IsConditionSaveAvailable" }, f.Events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddHasNoValidationGuardAndKeepsRawFieldsAndFreshModelIdentity(bool invalid)
    {
        var f = new Fixture(); f.Ready(invalid ? "" : " A B ", invalid ? "" : "on", "off");
        f.Draft.ConditionDescription = "desc"; f.Events.Clear();
        f.Draft.AddCondition(); f.Draft.AddCondition();
        Assert.Equal(2, f.Items.Count); Assert.NotSame(f.Items[0], f.Items[1]);
        foreach (var item in f.Items)
        {
            Assert.Equal(invalid ? "" : " A B ", item.Name);
            Assert.Equal(invalid ? "" : "on", item.OnValue);
            Assert.Equal("off", item.OffValue); Assert.Equal("desc", item.Description);
        }
        Assert.Equal(new[] { "exit", "add", "exit", "add" }, f.Trace);
        Assert.Empty(f.Events);
    }

    [Fact]
    public void AddAllowsExitCallbackToChangeDraftBeforeReadingAndDoesNotResetIt()
    {
        var f = new Fixture(); f.Ready("before", "on", "off");
        f.EnableExit = () =>
        {
            Assert.Empty(f.Items);
            f.Draft.ConditionName = "after"; f.Draft.ConditionDescription = "changed";
            f.Draft.ConditionOnValue = "new-on"; f.Draft.ConditionOffValue = "new-off";
        };
        f.Draft.AddCondition();
        var added = Assert.Single(f.Items);
        Assert.Equal(("after", "changed", "new-on", "new-off"), (added.Name, added.Description, added.OnValue, added.OffValue));
        Assert.Equal("after", f.Draft.ConditionName);
        Assert.Equal(new[] { "exit", "add" }, f.Trace);
    }

    [Theory]
    [InlineData("validate")]
    [InlineData("format")]
    [InlineData("text")]
    [InlineData("exit")]
    [InlineData("collection")]
    public void CallbackFailuresEscapeSynchronouslyWithOriginalPartialMutation(string point)
    {
        var f = new Fixture(); f.Ready("AB", "on", "off");
        var failure = new IOException(point);
        if (point == "validate") f.Validate = _ => throw failure;
        if (point == "format") f.Format = (_, _, _) => throw failure;
        if (point == "text") { f.Validate = _ => false; f.Localize = _ => throw failure; }
        if (point == "exit") f.EnableExit = () => throw failure;
        if (point == "collection") f.Items.CollectionChanged += (_, _) => throw failure;
        Assert.Same(failure, Assert.Throws<IOException>(() =>
        {
            if (point is "exit" or "collection") f.Draft.AddCondition(); else _ = f.Draft.ConditionPreview;
        }));
        Assert.Equal(point == "collection" ? 1 : 0, f.Items.Count);
    }

    [Fact]
    public void SetterObserverFailureKeepsAssignedValueAndSkipsDerivedNotifications()
    {
        var f = new Fixture(); var failure = new IOException("observer");
        f.Draft.PropertyChanged += (_, _) => throw failure;
        Assert.Same(failure, Assert.Throws<IOException>(() => f.Draft.ConditionName = "AB"));
        Assert.Equal("AB", f.Draft.ConditionName);
        Assert.Equal(new[] { "ConditionName" }, f.Events);
    }

    [Fact]
    public void ExistingCoreFormatterRemainsUsableWithoutWinUi()
    {
        var f = new Fixture(); f.Ready(" A B ", "on", "off");
        f.Format = LScriptCore.CreateCondition;
        Assert.Equal("AB==true ? on : off", f.Draft.ConditionPreview);
    }

    private sealed class Fixture
    {
        public ObservableCollection<ConditionVariableModel> Items { get; } = [];
        public List<string> Trace { get; } = [];
        public List<string?> Events { get; } = [];
        public Func<string, bool> Validate = _ => true;
        public Func<string, string, string, string> Format = (name, on, off) => $"{name}[{on}|{off}]";
        public Func<string, string> Localize = key => "localized:" + key;
        public Action? EnableExit;
        public CreateConfigConditionDraft Draft { get; }
        public Fixture()
        {
            Draft = new(Items,
                name => { Trace.Add("validate:" + name); return Validate(name); },
                (name, on, off) => { Trace.Add($"format:{name}:{on}:{off}"); return Format(name, on, off); },
                key => { Trace.Add("text:" + key); return Localize(key); },
                () => { Trace.Add("exit"); EnableExit?.Invoke(); });
            Draft.PropertyChanged += (sender, args) => { Assert.Same(Draft, sender); Events.Add(args.PropertyName); };
            Items.CollectionChanged += (_, _) => Trace.Add("add");
        }
        public void Ready(string name, string on, string off)
        {
            Draft.ConditionName = name; Draft.ConditionOnValue = on; Draft.ConditionOffValue = off;
            Events.Clear(); Trace.Clear();
        }
    }
}
