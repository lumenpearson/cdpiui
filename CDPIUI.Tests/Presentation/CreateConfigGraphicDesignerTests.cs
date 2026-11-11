using System.Collections.ObjectModel;
using CDPIUI.Core.Store.Data;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.ViewModels.CreateConfig;
using CDPIUI.Views.CreateConfigHelper;

namespace CDPIUI.Tests.Presentation;

public sealed class CreateConfigGraphicDesignerTests
{
    [Fact]
    public void ConstructorStoresDependenciesWithoutReadingIdsOrLoading()
    {
        var f = new Fixture();
        Assert.Empty(f.Trace); Assert.Empty(f.Events);
        Assert.Empty(f.Designer.AdditionalSettings); Assert.Empty(f.Designer.LoadedComponentId);
        Assert.Empty(f.Items); Assert.Empty(f.Groups);
    }

    [Fact]
    public void AdditionalSettingsPreservesNullNormalizationEqualityAndSender()
    {
        var f = new Fixture();
        f.Designer.AdditionalSettings = null!;
        Assert.Empty(f.Events);
        f.Designer.AdditionalSettings = "extra";
        f.Designer.AdditionalSettings = "extra";
        f.Designer.AdditionalSettings = null!;
        Assert.Equal(["AdditionalSettings", "AdditionalSettings"], f.Events);
        Assert.Empty(f.Designer.AdditionalSettings);
    }

    [Fact]
    public void AdditionalObserverFailureRetainsAssignedValue()
    {
        var f = new Fixture();
        f.Designer.PropertyChanged += (_, _) => throw new IOException("observer");
        Assert.Throws<IOException>(() => f.Designer.AdditionalSettings = "assigned");
        Assert.Equal("assigned", f.Designer.AdditionalSettings);
    }

    [Theory]
    [InlineData(Components.GoodbyeDPI, "good")]
    [InlineData(Components.SpoofDPI, "spoof")]
    [InlineData(Components.NoDPI, "no")]
    public void KnownLoadCachesComponentAndReusesTheProvidedCollections(Components component, string id)
    {
        var f = new Fixture();
        f.Designer.Load(id);
        Assert.Equal(id, f.Designer.LoadedComponentId);
        Assert.Single(f.Backend.Loads);
        f.Designer.Load(id);
        Assert.Single(f.Backend.Loads);
        Assert.Equal(id, f.Ids[component]);
    }

    [Fact]
    public void LoadReadsMappedIdAgainAfterCollectionCallbacks()
    {
        var f = new Fixture();
        f.Backend.Loading = _ => f.Ids[Components.GoodbyeDPI] = "changed-good";
        f.Designer.Load("good");
        Assert.Equal(["id:GoodbyeDPI", "id:GoodbyeDPI", "good", "id:GoodbyeDPI"], f.Trace);
        Assert.Equal("changed-good", f.Designer.LoadedComponentId);
    }

    [Fact]
    public void MissingAnnotationKeepsOldCacheAndRetries()
    {
        var f = new Fixture();
        f.Designer.Load("good");
        f.Backend.NoDpiAvailable = false;
        f.Designer.Load("no"); f.Designer.Load("no");
        Assert.Equal("good", f.Designer.LoadedComponentId);
        Assert.Equal(["good", "no:no", "no:no"], f.Backend.Loads);
        f.Backend.NoDpiAvailable = true;
        f.Designer.Load("no");
        Assert.Equal("no", f.Designer.LoadedComponentId);
    }

    [Theory]
    [InlineData("GOOD")]
    [InlineData("unknown")]
    [InlineData("")]
    [InlineData(null)]
    public void UnsupportedOrDifferentlyCasedIdKeepsModelsAndCache(string? id)
    {
        var f = new Fixture();
        var item = new GraphicDesignerSettingItemModel { Guid = "kept" };
        f.Items.Add(item);
        f.Designer.Load("good");
        f.Designer.Load(id!);
        Assert.Equal(["good"], f.Backend.Loads);
        Assert.Same(item, Assert.Single(f.Items));
        Assert.Equal("good", f.Designer.LoadedComponentId);
    }

    [Fact]
    public void SwitchingComponentsReloadsButReturningToCurrentDoesNot()
    {
        var f = new Fixture();
        f.Designer.Load("good"); f.Designer.Load("spoof"); f.Designer.Load("spoof"); f.Designer.Load("good");
        Assert.Equal(["good", "spoof", "good"], f.Backend.Loads);
        Assert.Equal("good", f.Designer.LoadedComponentId);
    }

    [Fact]
    public void EqualMappedIdsKeepTheOriginalBranchPriority()
    {
        var f = new Fixture();
        f.Ids[Components.SpoofDPI] = "good"; f.Ids[Components.NoDPI] = "good";
        f.Designer.Load("good"); f.Designer.Load("good");
        Assert.Equal(["good"], f.Backend.Loads);
    }

    [Fact]
    public void ReentrantLoadPublishesOuterCacheAfterNestedLoad()
    {
        var f = new Fixture();
        bool entered = false;
        f.Backend.Loading = _ => { if (!entered) { entered = true; f.Designer.Load("spoof"); } };
        f.Designer.Load("good");
        Assert.Equal(["good", "spoof"], f.Backend.Loads);
        Assert.Equal("good", f.Designer.LoadedComponentId);
    }

    [Fact]
    public void FailedLoadLeavesPreviousCacheAndPartialCollectionMutation()
    {
        var f = new Fixture();
        f.Designer.Load("good");
        var partial = new GraphicDesignerSettingItemModel { Guid = "partial" };
        f.Backend.Loading = _ => { f.Items.Add(partial); throw new IOException("load"); };
        Assert.Throws<IOException>(() => f.Designer.Load("spoof"));
        Assert.Equal("good", f.Designer.LoadedComponentId);
        Assert.Same(partial, Assert.Single(f.Items));
    }

    [Fact]
    public void TextPrefersFirstStandaloneMatchBeforeExclusiveChildren()
    {
        var f = new Fixture();
        var first = new GraphicDesignerSettingItemModel { Guid = "x" };
        var duplicate = new GraphicDesignerSettingItemModel { Guid = "x" };
        var child = new GraphicDesignerSettingItemModel { Guid = "x" };
        f.Items.Add(first); f.Items.Add(duplicate); f.Groups.Add(new() { Guid = "g", Items = [child] });
        f.Designer.ChangeText(Tuple.Create("x", "new"));
        Assert.Equal("new", first.Value); Assert.Equal("", duplicate.Value); Assert.Equal("", child.Value);
        Assert.Empty(f.Events);
    }

    [Fact]
    public void TextAndBoolUseFirstChildOfFirstMatchingGroup()
    {
        var f = new Fixture();
        var first = new GraphicDesignerSettingItemModel { Guid = "x" };
        var duplicate = new GraphicDesignerSettingItemModel { Guid = "x" };
        var later = new GraphicDesignerSettingItemModel { Guid = "x" };
        f.Groups.Add(new() { Guid = "g1", Items = [first, duplicate] });
        f.Groups.Add(new() { Guid = "g2", Items = [later] });
        f.Designer.ChangeText(Tuple.Create("x", "text"));
        f.Designer.ToggleBool(Tuple.Create("x", true));
        Assert.Equal("text", first.Value); Assert.True(first.IsChecked);
        Assert.Equal("", duplicate.Value); Assert.False(duplicate.IsChecked);
        Assert.Equal("", later.Value); Assert.False(later.IsChecked);
    }

    [Fact]
    public void BoolPrefersStandaloneAndSuppressesEqualModelNotifications()
    {
        var f = new Fixture();
        var first = new GraphicDesignerSettingItemModel { Guid = "x" };
        var child = new GraphicDesignerSettingItemModel { Guid = "x" };
        int changes = 0; first.PropertyChanged += (_, _) => changes++;
        f.Items.Add(first); f.Groups.Add(new() { Items = [child] });
        f.Designer.ToggleBool(Tuple.Create("x", true)); f.Designer.ToggleBool(Tuple.Create("x", true));
        Assert.True(first.IsChecked); Assert.False(child.IsChecked); Assert.Equal(1, changes);
    }

    [Fact]
    public void ScalarEditsRemainCaseSensitiveAndIgnoreUnknownGuid()
    {
        var f = new Fixture();
        var item = new GraphicDesignerSettingItemModel { Guid = "X" };
        f.Items.Add(item);
        f.Designer.ChangeText(Tuple.Create("x", "wrong"));
        f.Designer.ToggleBool(Tuple.Create("unknown", true));
        Assert.Equal("", item.Value); Assert.False(item.IsChecked);
    }

    [Fact]
    public void TextUsesActualGroupingSetterAndAcceptsNullWithoutCoercion()
    {
        var f = new Fixture();
        var item = new GraphicDesignerSettingItemModel { Guid = "x" };
        item.SetParsedValue("value", preserveGrouping: true);
        f.Items.Add(item);
        f.Designer.ChangeText(Tuple.Create("x", "value"));
        Assert.True(item.PreserveValueGrouping);
        f.Designer.ChangeText(Tuple.Create("x", "changed"));
        Assert.False(item.PreserveValueGrouping);
        f.Designer.ChangeText(Tuple.Create("x", (string)null!));
        Assert.Null(item.Value);
    }

    [Fact]
    public void ReentrantCollectionChangeDoesNotRetargetTheAssignedItem()
    {
        var f = new Fixture();
        var original = new GraphicDesignerSettingItemModel { Guid = "x" };
        var replacement = new GraphicDesignerSettingItemModel { Guid = "x" };
        f.Items.Add(original);
        original.PropertyChanged += (_, _) => { f.Items.Clear(); f.Items.Add(replacement); };
        f.Designer.ChangeText(Tuple.Create("x", "assigned"));
        Assert.Equal("assigned", original.Value); Assert.Equal("", replacement.Value);
        Assert.Same(replacement, Assert.Single(f.Items));
    }

    [Fact]
    public void BoolObserverFailureLeavesAssignedFlag()
    {
        var f = new Fixture();
        var item = new GraphicDesignerSettingItemModel { Guid = "x" };
        item.PropertyChanged += (_, _) => throw new IOException("item");
        f.Items.Add(item);
        Assert.Throws<IOException>(() => f.Designer.ToggleBool(Tuple.Create("x", true)));
        Assert.True(item.IsChecked);
    }

    [Fact]
    public void SelectionUsesFirstMatchingGroupWithoutValidatingChildOrCheckedState()
    {
        var f = new Fixture();
        var first = new GraphicDesignerExclusiveSettingItemModel { Guid = "g", Items = [] };
        var duplicate = new GraphicDesignerExclusiveSettingItemModel { Guid = "g", Items = [] };
        f.Groups.Add(first); f.Groups.Add(duplicate);
        f.Designer.SelectGuid(Tuple.Create("g", "nonexistent"));
        Assert.Equal("nonexistent", first.SelectedItemGuid); Assert.Equal("", duplicate.SelectedItemGuid);
        Assert.False(first.IsChecked);
        f.Designer.SelectGuid(Tuple.Create("g", (string)null!));
        Assert.Null(first.SelectedItemGuid);
    }

    [Fact]
    public void SelectionRemainsCaseSensitive()
    {
        var f = new Fixture();
        var group = new GraphicDesignerExclusiveSettingItemModel { Guid = "G", Items = [] };
        f.Groups.Add(group);
        f.Designer.SelectGuid(Tuple.Create("g", "value"));
        Assert.Equal("", group.SelectedItemGuid);
    }

    [Theory]
    [InlineData("text")]
    [InlineData("bool")]
    [InlineData("select")]
    public void NullTupleKeepsOriginalFailure(string kind)
    {
        var f = new Fixture();
        Assert.Throws<NullReferenceException>(() =>
        {
            if (kind == "text") f.Designer.ChangeText(null!);
            else if (kind == "bool") f.Designer.ToggleBool(null!);
            else f.Designer.SelectGuid(null!);
        });
        Assert.Empty(f.Events);
    }

    [Fact]
    public void PrimaryMatchDoesNotInspectMalformedGroupsButFallbackRetainsFailure()
    {
        var f = new Fixture();
        var item = new GraphicDesignerSettingItemModel { Guid = "x" };
        f.Items.Add(item); f.Groups.Add(new() { Items = null! });
        f.Designer.ChangeText(Tuple.Create("x", "safe"));
        Assert.Equal("safe", item.Value);
        Assert.Throws<ArgumentNullException>(() => f.Designer.ChangeText(Tuple.Create("missing", "value")));
    }

    [Fact]
    public void ParseUsesSameCollectionsAndPublishesResultAfterBackendCallbacks()
    {
        var f = new Fixture();
        f.Backend.Parsing = text => { Assert.Equal("original\ntext", text); f.Designer.AdditionalSettings = "during"; return "after"; };
        f.Designer.Parse("original\ntext");
        Assert.Equal("after", f.Designer.AdditionalSettings);
        Assert.Equal(["parse", "event:AdditionalSettings", "event:AdditionalSettings"], f.Trace);
    }

    [Fact]
    public void NullParseResultUsesExistingAdditionalNormalization()
    {
        var f = new Fixture();
        f.Designer.AdditionalSettings = "old";
        f.Backend.Parsing = _ => null!;
        f.Designer.Parse("input");
        Assert.Equal("", f.Designer.AdditionalSettings);
    }

    [Fact]
    public void BuildCapturesAdditionalBeforeBackendReentry()
    {
        var f = new Fixture();
        f.Designer.AdditionalSettings = "before"; f.Trace.Clear();
        f.Backend.Building = additional => { f.Designer.AdditionalSettings = "during"; return additional; };
        Assert.Equal("before", f.Designer.Build());
        Assert.Equal("during", f.Designer.AdditionalSettings);
        Assert.Equal(["build:before", "event:AdditionalSettings"], f.Trace);
    }

    [Fact]
    public void ParseFailureKeepsOldAdditionalAndPartialModels()
    {
        var f = new Fixture();
        f.Designer.AdditionalSettings = "old";
        var item = new GraphicDesignerSettingItemModel { Guid = "partial" };
        f.Backend.Parsing = _ => { f.Items.Add(item); throw new IOException("parse"); };
        Assert.Throws<IOException>(() => f.Designer.Parse("input"));
        Assert.Equal("old", f.Designer.AdditionalSettings); Assert.Same(item, Assert.Single(f.Items));
    }

    [Fact]
    public void BuildFailureKeepsBackendReentrantChanges()
    {
        var f = new Fixture();
        f.Backend.Building = _ => { f.Designer.AdditionalSettings = "changed"; throw new IOException("build"); };
        Assert.Throws<IOException>(() => f.Designer.Build());
        Assert.Equal("changed", f.Designer.AdditionalSettings);
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Backend = new(this);
            Designer = new(Items, Groups, Backend, component => { Trace.Add("id:" + component); return Ids[component]; });
            Designer.PropertyChanged += (sender, e) =>
            {
                Assert.Same(Designer, sender);
                Events.Add(e.PropertyName!); Trace.Add("event:" + e.PropertyName);
            };
        }
        public ObservableCollection<GraphicDesignerSettingItemModel> Items { get; } = [];
        public ObservableCollection<GraphicDesignerExclusiveSettingItemModel> Groups { get; } = [];
        public Dictionary<Components, string> Ids { get; } = new()
        {
            [Components.GoodbyeDPI] = "good", [Components.SpoofDPI] = "spoof", [Components.NoDPI] = "no"
        };
        public List<string> Trace { get; } = [];
        public List<string> Events { get; } = [];
        public Backend Backend { get; }
        public CreateConfigGraphicDesigner Designer { get; }
    }

    private sealed class Backend(Fixture fixture) : ICreateConfigGraphicDesignerBackend
    {
        public bool NoDpiAvailable { get; set; } = true;
        public List<string> Loads { get; } = [];
        public Action<string>? Loading { get; set; }
        public Func<string, string>? Parsing { get; set; }
        public Func<string, string>? Building { get; set; }
        private void Collections(ObservableCollection<GraphicDesignerSettingItemModel> items,
            ObservableCollection<GraphicDesignerExclusiveSettingItemModel> groups)
        {
            Assert.Same(fixture.Items, items); Assert.Same(fixture.Groups, groups);
        }
        private void Load(string kind)
        {
            Loads.Add(kind); fixture.Trace.Add(kind); Loading?.Invoke(kind);
        }
        public void LoadGoodbye(ObservableCollection<GraphicDesignerSettingItemModel> items, ObservableCollection<GraphicDesignerExclusiveSettingItemModel> groups)
        { Collections(items, groups); Load("good"); }
        public void LoadSpoof(ObservableCollection<GraphicDesignerSettingItemModel> items, ObservableCollection<GraphicDesignerExclusiveSettingItemModel> groups)
        { Collections(items, groups); Load("spoof"); }
        public bool TryLoadNoDpi(string id, ObservableCollection<GraphicDesignerSettingItemModel> items, ObservableCollection<GraphicDesignerExclusiveSettingItemModel> groups)
        { Collections(items, groups); Load("no:" + id); return NoDpiAvailable; }
        public string Parse(ObservableCollection<GraphicDesignerSettingItemModel> items, ObservableCollection<GraphicDesignerExclusiveSettingItemModel> groups, string text)
        { Collections(items, groups); fixture.Trace.Add("parse"); return Parsing is null ? "--extra" : Parsing(text); }
        public string Build(ObservableCollection<GraphicDesignerSettingItemModel> items, ObservableCollection<GraphicDesignerExclusiveSettingItemModel> groups, string additional)
        { Collections(items, groups); fixture.Trace.Add("build:" + additional); return Building?.Invoke(additional) ?? "built:" + additional; }
    }
}
