using System.Collections.ObjectModel;
using System.Windows.Input;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Views.CreateConfigHelper;

namespace CDPIUI.Tests.Presentation;

public class ConfigMakerSimpleDesignerTests
{
    [Fact]
    public void ConstructionDoesNotLoadModelsOrReadEditorState()
    {
        var fixture = new DesignerFixture();

        Assert.Empty(fixture.Trace);
        Assert.Equal(0, fixture.Backend.SupportChecks);
        Assert.Empty(fixture.Designer.DesignerSettingItemModels);
    }

    [Fact]
    public void EnteringLoadsModelsCommandsAndRemainingArgumentsInOrder()
    {
        var fixture = new DesignerFixture();

        fixture.Designer.OnEditorViewChanged(false, true);

        Assert.Equal(["read:--known=1 --extra=2", "load:supported", "read:--known=1 --extra=2", "parse:--known=1 --extra=2"], fixture.Trace);
        Assert.Equal("--extra=2", fixture.Designer.AdditionalDesignerArguments);
        Assert.Same(fixture.TextCommand, Assert.Single(fixture.Designer.DesignerSettingItemModels).DesignerTextValueChangedCommand);
        Assert.Same(fixture.BoolCommand, fixture.Designer.DesignerSettingItemModels[0].DesignerBoolValueToggledCommand);
        var group = Assert.Single(fixture.Designer.DesignerExclusiveSettingItemModels);
        Assert.Same(fixture.SelectCommand, group.DesignerSelectedGuidChangedCommand);
        Assert.Null(group.Items[0].DesignerTextValueChangedCommand);
    }

    [Fact]
    public void LoadReadsEditorAgainAfterModelCollectionNotifications()
    {
        var fixture = new DesignerFixture();
        fixture.Backend.AfterLoad = () => fixture.EditorText = "--changed=3";

        fixture.Designer.OnEditorViewChanged(false, true);

        Assert.Contains("parse:--changed=3", fixture.Trace);
    }

    [Fact]
    public void ALoadedDesignerIsReusedUntilTheEditorMarksItDirty()
    {
        var fixture = Loaded();
        var collection = fixture.Designer.DesignerSettingItemModels;
        fixture.Trace.Clear();

        fixture.Designer.OnEditorViewChanged(false, true);
        Assert.Empty(fixture.Trace);
        fixture.Designer.MarkDirty();
        fixture.Designer.OnEditorViewChanged(false, true);

        Assert.Same(collection, fixture.Designer.DesignerSettingItemModels);
        Assert.Contains("load:supported", fixture.Trace);
    }

    [Fact]
    public void MissingAnnotationReportsAndRetriesOnTheNextEntry()
    {
        var fixture = new DesignerFixture();
        fixture.Backend.LoadSucceeds = false;

        fixture.Designer.OnEditorViewChanged(false, true);

        Assert.Equal(ConfigMakerDesignerNotice.MissingAnnotation, Assert.Single(fixture.Notices).Kind);
        Assert.Empty(fixture.Designer.DesignerSettingItemModels);
        Assert.DoesNotContain(fixture.Trace, item => item.StartsWith("parse:", StringComparison.Ordinal));
        fixture.Backend.LoadSucceeds = true;
        fixture.Designer.OnEditorViewChanged(false, true);
        Assert.Single(fixture.Designer.DesignerSettingItemModels);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoadOrParseFailureClearsModelsAndReleasesTheUpdateGuard(bool parseFailure)
    {
        var fixture = new DesignerFixture();
        var failure = new IOException("load failed");
        if (parseFailure) fixture.Backend.ParseError = failure;
        else fixture.Backend.LoadError = failure;

        fixture.Designer.OnEditorViewChanged(false, true);

        Assert.Empty(fixture.Designer.DesignerSettingItemModels);
        Assert.Empty(fixture.Designer.DesignerExclusiveSettingItemModels);
        var notice = Assert.Single(fixture.Notices);
        Assert.Equal(ConfigMakerDesignerNotice.LoadFailed, notice.Kind);
        Assert.Same(failure, notice.Error);
        fixture.Backend.LoadError = null;
        fixture.Backend.ParseError = null;
        fixture.Designer.OnEditorViewChanged(false, true);
        Assert.Single(fixture.Designer.DesignerSettingItemModels);
    }

    [Theory]
    [InlineData("ordinary")]
    [InlineData("child")]
    public void TextChangesFindOrdinaryAndExclusiveItemsAndApplyToEditor(string id)
    {
        var fixture = Loaded();

        fixture.Designer.HandleDesignerTextValueChanged(Tuple.Create(id, "new-value"));

        var item = id == "ordinary" ? fixture.Designer.DesignerSettingItemModels[0]
            : fixture.Designer.DesignerExclusiveSettingItemModels[0].Items[0];
        Assert.Equal("new-value", item.Value);
        Assert.Equal($"--first=1{Environment.NewLine}--second=2", Assert.Single(fixture.EditedTexts));
    }

    [Fact]
    public void BoolAndExclusiveSelectionChangesRetainCaseSensitiveIdentifiers()
    {
        var fixture = Loaded();

        fixture.Designer.HandleDesignerBoolValueToggled(Tuple.Create("ordinary", true));
        fixture.Designer.HandleDesignerSelectedGuidChanged(Tuple.Create("group", "child"));
        fixture.Designer.HandleDesignerTextValueChanged(Tuple.Create("ORDINARY", "ignored"));
        fixture.Designer.HandleDesignerSelectedGuidChanged(Tuple.Create("GROUP", "ignored"));

        Assert.True(fixture.Designer.DesignerSettingItemModels[0].IsChecked);
        Assert.Equal("child", fixture.Designer.DesignerExclusiveSettingItemModels[0].SelectedItemGuid);
        Assert.Equal(2, fixture.EditedTexts.Count);
    }

    [Fact]
    public void AdditionalTextDoesNotNotifyBackToTheTextboxAndUsesTheNewValue()
    {
        var fixture = Loaded();
        int changes = 0;
        fixture.Designer.PropertyChanged += (_, args) => { if (args.PropertyName == "AdditionalDesignerArguments") changes++; };

        fixture.Designer.OnAdditionalDesignerArgumentsChanged("--new=3");

        Assert.Equal(0, changes);
        Assert.Equal("--new=3", fixture.Designer.AdditionalDesignerArguments);
        Assert.Contains("build:--new=3", fixture.Trace);
    }

    [Fact]
    public void ApplyingClearsDirtyAfterTheHostReplacesEditorText()
    {
        var fixture = Loaded();
        fixture.Designer.OnAdditionalDesignerArgumentsChanged("--new=3");
        fixture.Trace.Clear();

        fixture.Designer.OnEditorViewChanged(false, true);

        Assert.Empty(fixture.Trace);
    }

    [Fact]
    public void ReentrantTextboxChangeDuringEditorReplacementDoesNotApplyTwice()
    {
        var fixture = Loaded();
        fixture.AfterEditorSet = () => fixture.Designer.OnAdditionalDesignerArgumentsChanged("--inner=4");

        fixture.Designer.OnAdditionalDesignerArgumentsChanged("--outer=3");

        Assert.Single(fixture.EditedTexts);
        Assert.Equal("--inner=4", fixture.Designer.AdditionalDesignerArguments);
        Assert.Single(fixture.Trace, item => item.StartsWith("build:", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildFailurePropagatesAndDoesNotLeaveTheUpdateGuardSet()
    {
        var fixture = Loaded();
        fixture.Backend.BuildError = new IOException("build failed");

        Assert.Throws<IOException>(() => fixture.Designer.OnAdditionalDesignerArgumentsChanged("extra"));
        Assert.Empty(fixture.Notices);
        fixture.Backend.BuildError = null;
        fixture.Designer.OnAdditionalDesignerArgumentsChanged("extra");
        Assert.Single(fixture.EditedTexts);
    }

    [Fact]
    public void LeavingTheLoadedDesignerAppliesItsFormattedCommand()
    {
        var fixture = Loaded();

        fixture.Designer.OnEditorViewChanged(true, false);

        Assert.Equal($"--first=1{Environment.NewLine}--second=2", Assert.Single(fixture.EditedTexts));
        Assert.Contains("build:--extra=2", fixture.Trace);
    }

    [Fact]
    public void DirtyOrUnsupportedDesignerDoesNotOverwriteEditorText()
    {
        var fixture = new DesignerFixture();
        fixture.Designer.OnEditorViewChanged(true, false);
        Assert.Empty(fixture.Trace);
        fixture.ComponentId = "unsupported";
        fixture.Designer.OnEditorViewChanged(false, true);
        fixture.Designer.OnAdditionalDesignerArgumentsChanged("extra");

        Assert.Empty(fixture.EditedTexts);
        Assert.Empty(fixture.Trace);
    }

    [Fact]
    public void AvailabilityNotifiesBeforeClearingAndRechecksTheCurrentComponent()
    {
        var fixture = Loaded();
        int countAtNotification = -1;
        fixture.Designer.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != "IsSimpleDesignerSupported") return;
            countAtNotification = fixture.Designer.DesignerSettingItemModels.Count;
            fixture.ComponentId = "unsupported";
        };

        fixture.Designer.UpdateAvailability();

        Assert.Equal(1, countAtNotification);
        Assert.Empty(fixture.Designer.DesignerSettingItemModels);
        Assert.Empty(fixture.Designer.DesignerExclusiveSettingItemModels);
    }

    private static DesignerFixture Loaded()
    {
        var fixture = new DesignerFixture();
        fixture.Designer.OnEditorViewChanged(false, true);
        fixture.Trace.Clear();
        return fixture;
    }

    private sealed class DesignerFixture
    {
        public DesignerFixture()
        {
            Backend = new(Trace);
            Designer = new(Backend, () => ComponentId,
                () => { Trace.Add("read:" + EditorText); return EditorText; },
                text => { EditedTexts.Add(text); Designer.MarkDirty(); AfterEditorSet?.Invoke(); },
                new(TextCommand, BoolCommand, SelectCommand),
                (kind, error) => Notices.Add((kind, error)));
        }
        public string ComponentId { get; set; } = "supported";
        public string EditorText { get; set; } = "--known=1 --extra=2";
        public Action? AfterEditorSet { get; set; }
        public List<string> Trace { get; } = [];
        public List<string> EditedTexts { get; } = [];
        public List<(ConfigMakerDesignerNotice Kind, Exception? Error)> Notices { get; } = [];
        public ICommand TextCommand { get; } = new DummyCommand();
        public ICommand BoolCommand { get; } = new DummyCommand();
        public ICommand SelectCommand { get; } = new DummyCommand();
        public DesignerBackend Backend { get; }
        public ConfigMakerSimpleDesigner Designer { get; }
    }

    private sealed class DesignerBackend(List<string> trace) : IConfigMakerSimpleDesignerBackend
    {
        public int SupportChecks { get; private set; }
        public bool LoadSucceeds { get; set; } = true;
        public Exception? LoadError { get; set; }
        public Exception? ParseError { get; set; }
        public Exception? BuildError { get; set; }
        public Action? AfterLoad { get; set; }
        public bool IsSupported(string component) { SupportChecks++; return component == "supported"; }
        public bool TryLoad(string component, ObservableCollection<GraphicDesignerSettingItemModel> items, ObservableCollection<GraphicDesignerExclusiveSettingItemModel> groups)
        {
            trace.Add("load:" + component);
            if (LoadError != null) throw LoadError;
            if (!LoadSucceeds) return false;
            items.Add(new() { Guid = "ordinary", DisplayName = "--known", Type = "string" });
            groups.Add(new() { Guid = "group", Items = [new() { Guid = "child", DisplayName = "--child", Type = "string" }] });
            AfterLoad?.Invoke();
            return true;
        }
        public string Parse(ObservableCollection<GraphicDesignerSettingItemModel> items, ObservableCollection<GraphicDesignerExclusiveSettingItemModel> groups, string command)
        {
            trace.Add("parse:" + command);
            if (ParseError != null) throw ParseError;
            return "--extra=2";
        }
        public string Build(ObservableCollection<GraphicDesignerSettingItemModel> items, ObservableCollection<GraphicDesignerExclusiveSettingItemModel> groups, string additional)
        {
            trace.Add("build:" + additional);
            if (BuildError != null) throw BuildError;
            return "  --first=1 --second=2 ";
        }
    }

    private sealed class DummyCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) { }
    }
}
