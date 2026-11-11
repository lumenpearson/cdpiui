using CDPIUI.Helper.CreateConfigHelper;

namespace CDPIUI.Tests.Presentation;

public sealed class ConfigMakerPanelLayoutTests
{
    [Fact]
    public void ConstructorStoresPortWithoutReadingUi()
    {
        var f = new Fixture(); Assert.Empty(f.View.Trace); Assert.False(f.Owner.IsUpdating);
    }

    [Theory]
    [InlineData(0, 800, 1)]
    [InlineData(-1, 800, 1)]
    [InlineData(1200, 0, 2)]
    [InlineData(1200, -1, 2)]
    public void InvalidRootShortcircuitsBeforeMutation(double width, double height, int reads)
    {
        var f = new Fixture(); f.View.Values["RootWidth"] = width; f.View.Values["RootHeight"] = height;
        f.Owner.Update();
        Assert.Equal(reads, f.View.Trace.Count); Assert.All(f.View.Trace, step => Assert.StartsWith("g:Root", step));
        Assert.False(f.Owner.IsUpdating);
    }

    [Theory]
    [InlineData(true, true, 848, 678, 420)]
    [InlineData(false, true, 1200, 678, 420)]
    [InlineData(true, false, 848, 800, 800)]
    [InlineData(false, false, 1200, 800, 800)]
    public void PanelBranchesRetainOriginalEditorBounds(bool commands, bool bottom, double width, double maxHeight, double height)
    {
        var f = new Fixture(); f.View.Values["CommandsVisible"] = commands ? 1 : 0; f.View.Values["BottomVisible"] = bottom ? 1 : 0;
        f.Owner.Update();
        Assert.Equal(width, f.View.Values["EditorMaxWidth"]); Assert.Equal(width, f.View.Values["EditorWidth"]);
        Assert.Equal(maxHeight, f.View.Values["EditorMaxHeight"]); Assert.Equal(height, f.View.Values["EditorHeight"]);
        Assert.DoesNotContain(f.View.Trace, step => step.StartsWith("g:Files", StringComparison.Ordinal) && step != "g:FilesVisible");
        Assert.False(f.Owner.IsUpdating);
    }

    [Fact]
    public void ExactDefaultReadAndAssignmentOrderIsRetained()
    {
        var f = new Fixture(); f.Owner.Update();
        Assert.Equal(["g:RootWidth", "g:RootHeight", "g:FilesVisible", "g:RootWidth", "g:CommandsVisible",
            "g:CommandSizerWidth", "s:EditorMaxWidth", "s:EditorWidth", "g:RootHeight", "g:BottomVisible",
            "g:BottomSizerHeight", "s:EditorMaxHeight", "s:EditorHeight"], f.View.Trace);
    }

    [Theory]
    [InlineData(1200, 150, 100, 476, 150)]
    [InlineData(1200, 150, 250, 476, 250)]
    [InlineData(1200, 150, 999, 476, 476)]
    [InlineData(600, 200, 999, 200, 200)]
    [InlineData(300, 150, 999, 150, 150)]
    [InlineData(2000, 150, 999, 900, 900)]
    [InlineData(1200, 600, 999, 600, 600)]
    public void FilesWidthRetainsSpaceMinimumFortyFivePercentCapAndClamp(double root, double minimum, double current, double maximum, double width)
    {
        var f = new Fixture(); f.View.Values["FilesVisible"] = 1; f.View.Values["RootWidth"] = root;
        f.View.Values["FilesMinWidth"] = minimum; f.View.Values["FilesWidth"] = current;
        f.Owner.Update();
        Assert.Equal(maximum, f.View.Values["FilesMaxWidth"]); Assert.Equal(width, f.View.Values["FilesWidth"]);
        Assert.Equal(Math.Max(360, Math.Max(360, root - width - 12) - 340 - 12), f.View.Values["EditorWidth"]);
    }

    [Fact]
    public void TinyRootRetainsOriginalEditorMinimums()
    {
        var f = new Fixture(); f.View.Values["RootWidth"] = 100; f.View.Values["RootHeight"] = 100;
        f.Owner.Update();
        Assert.Equal(360, f.View.Values["EditorWidth"]); Assert.Equal(180, f.View.Values["EditorMaxHeight"]);
        Assert.Equal(180, f.View.Values["EditorHeight"]);
    }

    [Fact]
    public void InitializationUsesSavedHeightThenCurrentHeightAndInvalidateReturnsToSaved()
    {
        var f = new Fixture(); f.Owner.Update(); Assert.Equal(420, f.View.Values["EditorHeight"]);
        Assert.DoesNotContain("g:EditorHeight", f.View.Trace);
        f.View.Trace.Clear(); f.View.Values["EditorHeight"] = 250; f.Owner.Update();
        Assert.Equal(250, f.View.Values["EditorHeight"]); Assert.Contains("g:EditorHeight", f.View.Trace);
        f.Owner.RememberEditorHeight(300); f.Owner.Invalidate(); f.View.Trace.Clear(); f.Owner.Update();
        Assert.Equal(300, f.View.Values["EditorHeight"]); Assert.DoesNotContain("g:EditorHeight", f.View.Trace);
    }

    [Theory]
    [InlineData(-10, 180)]
    [InlineData(180, 180)]
    [InlineData(420, 420)]
    [InlineData(1000, 678)]
    public void RememberedHeightKeepsOriginalClamp(double remembered, double expected)
    {
        var f = new Fixture(); f.Owner.RememberEditorHeight(remembered); f.Owner.Update();
        Assert.Equal(expected, f.View.Values["EditorHeight"]);
    }

    [Fact]
    public void HiddenBottomUsesFullHeightAndDoesNotReadCurrentEditorHeight()
    {
        var f = new Fixture(); f.Owner.Update(); f.View.Trace.Clear(); f.View.Values["BottomVisible"] = 0;
        f.Owner.Update(); Assert.Equal(800, f.View.Values["EditorHeight"]);
        Assert.DoesNotContain("g:EditorHeight", f.View.Trace); Assert.DoesNotContain("g:BottomSizerHeight", f.View.Trace);
        f.View.Values["BottomVisible"] = 1; f.Owner.Invalidate(); f.Owner.Update();
        Assert.Equal(420, f.View.Values["EditorHeight"]);
    }

    [Fact]
    public void NestedUpdateDuringUiSetterReturnsWithoutAnyPortReads()
    {
        var f = new Fixture(); int entered = 0;
        f.View.OnStep = step =>
        {
            if (step == "s:EditorMaxWidth")
            {
                Assert.True(f.Owner.IsUpdating); int before = f.View.Trace.Count; entered++;
                f.Owner.Update(); Assert.Equal(before, f.View.Trace.Count);
            }
        };
        f.Owner.Update(); Assert.Equal(1, entered); Assert.False(f.Owner.IsUpdating);
        Assert.Equal(420, f.View.Values["EditorHeight"]);
    }

    [Fact]
    public void LaterRootReadSeesMutationFromEarlierFilesAssignment()
    {
        var f = new Fixture(); f.View.Values["FilesVisible"] = 1; f.View.Values["CommandsVisible"] = 0;
        f.View.OnStep = step => { if (step == "s:FilesWidth") f.View.Values["RootWidth"] = 700; };
        f.Owner.Update(); Assert.Equal(438, f.View.Values["EditorWidth"]);
        Assert.Equal(4, f.View.Trace.Count(step => step == "g:RootWidth"));
    }

    [Fact]
    public void LaterCommandsBranchAndClampReadCurrentPropertiesAfterCallbacks()
    {
        var f = new Fixture(); f.View.Values["FilesVisible"] = 1; f.View.Values["FilesWidth"] = 100;
        f.View.OnStep = step =>
        {
            if (step == "s:FilesMaxWidth") f.View.Values["FilesMinWidth"] = 200;
            if (step == "s:FilesWidth") f.View.Values["CommandsVisible"] = 0;
        };
        f.Owner.Update(); Assert.Equal(476, f.View.Values["FilesMaxWidth"]); Assert.Equal(200, f.View.Values["FilesWidth"]);
        Assert.Equal(988, f.View.Values["EditorWidth"]); Assert.Equal(1, f.View.Trace.Count(step => step == "g:CommandSizerWidth"));
    }

    [Theory]
    [InlineData("g:RootWidth")]
    [InlineData("g:FilesVisible")]
    [InlineData("s:EditorMaxWidth")]
    [InlineData("s:EditorWidth")]
    [InlineData("g:RootHeight")]
    [InlineData("g:BottomVisible")]
    [InlineData("s:EditorMaxHeight")]
    [InlineData("s:EditorHeight")]
    public void PortFailureClearsUpdatingAndDoesNotMarkIncompleteLayoutInitialized(string failing)
    {
        var f = new Fixture();
        f.View.OnStep = step => { if (step == failing) throw new IOException("owned port"); };
        Assert.Throws<IOException>(f.Owner.Update); Assert.False(f.Owner.IsUpdating);
        f.View.OnStep = null; f.View.Values["EditorHeight"] = 250; f.View.Trace.Clear(); f.Owner.Update();
        Assert.Equal(420, f.View.Values["EditorHeight"]); Assert.DoesNotContain("g:EditorHeight", f.View.Trace);
    }

    [Fact]
    public void FailureAfterPreviousSuccessKeepsInitializedStateAndAssignedPartialBounds()
    {
        var f = new Fixture(); f.Owner.Update(); f.View.Values["RootWidth"] = 1000; f.View.Values["EditorHeight"] = 250;
        f.View.OnStep = step => { if (step == "s:EditorWidth") throw new IOException("owned setter"); };
        Assert.Throws<IOException>(f.Owner.Update); Assert.Equal(648, f.View.Values["EditorWidth"]); Assert.False(f.Owner.IsUpdating);
        f.View.OnStep = null; f.View.Trace.Clear(); f.Owner.Update();
        Assert.Equal(250, f.View.Values["EditorHeight"]); Assert.Contains("g:EditorHeight", f.View.Trace);
    }

    [Fact]
    public void InvalidateFromCallbackBeforeSuccessfulEndIsOverwrittenByOriginalFinalInitialization()
    {
        var f = new Fixture(); f.View.OnStep = step => { if (step == "s:EditorHeight") f.Owner.Invalidate(); };
        f.Owner.Update(); f.View.OnStep = null; f.View.Values["EditorHeight"] = 250; f.Owner.Update();
        Assert.Equal(250, f.View.Values["EditorHeight"]);
    }

    [Fact]
    public void InconsistentLiveClampBoundsThrowWithoutRollbackAndAllowNextUpdate()
    {
        var f = new Fixture(); f.View.Values["FilesVisible"] = 1;
        f.View.OnStep = step => { if (step == "s:FilesMaxWidth") f.View.Values["FilesMinWidth"] = 1000; };
        Assert.Throws<ArgumentException>(f.Owner.Update);
        Assert.Equal(476, f.View.Values["FilesMaxWidth"]); Assert.Equal(250, f.View.Values["FilesWidth"]); Assert.False(f.Owner.IsUpdating);
        f.View.OnStep = null; f.View.Values["FilesMinWidth"] = 150; f.Owner.Update(); Assert.Equal(420, f.View.Values["EditorHeight"]);
    }

    [Fact]
    public void ControllerDoesNotIntroduceNaNNormalizationBeforeUiPropertyValidation()
    {
        var f = new Fixture(); f.View.Values["RootWidth"] = double.NaN; f.Owner.RememberEditorHeight(double.NaN);
        f.Owner.Update(); Assert.True(double.IsNaN(f.View.Values["EditorWidth"]));
        Assert.True(double.IsNaN(f.View.Values["EditorHeight"])); Assert.False(f.Owner.IsUpdating);
    }

    private sealed class Fixture
    {
        public Fixture() => Owner = new(View);
        public Port View { get; } = new();
        public ConfigMakerPanelLayoutController Owner { get; }
    }

    private sealed class Port : IConfigMakerPanelLayoutView
    {
        public Dictionary<string, double> Values { get; } = new()
        {
            ["RootWidth"] = 1200, ["RootHeight"] = 800, ["FilesVisible"] = 0, ["CommandsVisible"] = 1, ["BottomVisible"] = 1,
            ["FilesMinWidth"] = 150, ["FilesMaxWidth"] = 500, ["FilesWidth"] = 250, ["FilesSizerWidth"] = 12,
            ["CommandSizerWidth"] = 12, ["BottomSizerHeight"] = 12, ["EditorHeight"] = 999,
        };
        public List<string> Trace { get; } = [];
        public Action<string>? OnStep;
        private double Get(string name) { Trace.Add("g:" + name); OnStep?.Invoke("g:" + name); return Values[name]; }
        private void Set(string name, double value) { Values[name] = value; Trace.Add("s:" + name); OnStep?.Invoke("s:" + name); }
        public double RootWidth => Get(nameof(RootWidth));
        public double RootHeight => Get(nameof(RootHeight));
        public bool FilesVisible => Get(nameof(FilesVisible)) == 1;
        public bool CommandsVisible => Get(nameof(CommandsVisible)) == 1;
        public bool BottomVisible => Get(nameof(BottomVisible)) == 1;
        public double FilesMinWidth => Get(nameof(FilesMinWidth));
        public double FilesMaxWidth { get => Get(nameof(FilesMaxWidth)); set => Set(nameof(FilesMaxWidth), value); }
        public double FilesWidth { get => Get(nameof(FilesWidth)); set => Set(nameof(FilesWidth), value); }
        public double FilesSizerWidth => Get(nameof(FilesSizerWidth));
        public double CommandSizerWidth => Get(nameof(CommandSizerWidth));
        public double BottomSizerHeight => Get(nameof(BottomSizerHeight));
        public double EditorMaxWidth { set => Set(nameof(EditorMaxWidth), value); }
        public double EditorWidth { set => Set(nameof(EditorWidth), value); }
        public double EditorMaxHeight { set => Set(nameof(EditorMaxHeight), value); }
        public double EditorHeight { get => Get(nameof(EditorHeight)); set => Set(nameof(EditorHeight), value); }
    }
}
