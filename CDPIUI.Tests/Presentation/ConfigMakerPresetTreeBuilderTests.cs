using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Helper.CreateConfigHelper;

namespace CDPIUI.Tests.Presentation;

public class ConfigMakerPresetTreeBuilderTests
{
    [Fact]
    public void FilesAreGroupedByKindAndSortedWithinEachCategory()
    {
        var builder = CreateBuilder();
        ConfigMakerPresetFileInfo[] files =
        [
            new("z.txt", "z.txt", ""),
            new("payload.bin", "payload.bin", "", ConfigMakerPresetFileKind.Payload),
            new("a.txt", "a.txt", ""),
            new("init.lua", "init.lua", "", ConfigMakerPresetFileKind.Library),
        ];

        var nodes = builder.BuildFiles(files);

        Assert.Equal(["Lists", "Libraries", "Payloads"], nodes.Select(node => node.DisplayName));
        Assert.Equal(["a.txt", "z.txt"], nodes[0].Children.Select(node => node.DisplayName));
        Assert.All(nodes, node => Assert.True(node.IsExpanded));
        Assert.All(nodes, node => Assert.Null(node.File));
        Assert.Same(files[2], nodes[0].Children[0].File);
    }

    [Fact]
    public void MissingFileTooltipUsesTheResolvedPath()
    {
        var file = new ConfigMakerPresetFileInfo("missing.txt", "missing.txt", "");

        var leaf = Assert.Single(Assert.Single(CreateBuilder().BuildFiles([file])).Children);

        Assert.True(leaf.IsMissing);
        Assert.Equal("Missing: /resolved/missing.txt", leaf.ToolTip);
        Assert.False(leaf.IsExpanded);
    }

    [Fact]
    public void AttachedFileTooltipRetainsReferenceAndResolvedPath()
    {
        var file = new ConfigMakerPresetFileInfo("hosts", "preset://hosts", "", IsAttachedResource: true);

        var leaf = Assert.Single(Assert.Single(CreateBuilder().BuildFiles([file])).Children);

        Assert.False(leaf.IsMissing);
        Assert.Equal($"preset://hosts{Environment.NewLine}/resolved/preset://hosts", leaf.ToolTip);
    }

    [Fact]
    public void UnresolvableFileIsMissingWithoutCheckingTheFilesystem()
    {
        var builder = new ConfigMakerPresetTreeBuilder(Localize, _ => "", _ => throw new Exception("Unexpected I/O"), _ => "");

        var leaf = Assert.Single(Assert.Single(builder.BuildFiles([new("bad", "bad", "")])).Children);

        Assert.True(leaf.IsMissing);
        Assert.Equal("Missing: ", leaf.ToolTip);
    }

    [Fact]
    public void EmptyFilesProduceNoCategories() => Assert.Empty(CreateBuilder().BuildFiles([]));

    [Fact]
    public void CommonOptionsAndProfilesRetainTheirOrderAndExplicitNames()
    {
        var options = ConfigCommandLine.ParseOptions(
            "--wf-tcp=80 --debug=1 --filter-tcp=80 --name=parsed --dpi-desync=fake --new --filter-udp=443 --dpi-desync=split");

        var nodes = CreateBuilder().BuildGroups(options, [new("Explicit", [])], isZapret2: true);

        Assert.Equal(["Common filters", "Common options", "Explicit", "Profile 2"], nodes.Select(node => node.Title));
        Assert.Equal("--wf-tcp=80", Assert.Single(nodes[0].Children).Title);
        Assert.Equal("--debug=1", Assert.Single(nodes[1].Children).Title);
        Assert.Equal(["Profile filters", "Profile actions"], nodes[2].Children.Select(node => node.Title));
        Assert.Equal(["--filter-tcp=80", "--name=parsed"], nodes[2].Children[0].Children.Select(node => node.Title));
        Assert.Equal("--dpi-desync=fake", Assert.Single(nodes[2].Children[1].Children).Title);
    }

    [Fact]
    public void ParsedProfileNameIsUnquotedWhenThereIsNoExplicitName()
    {
        var nodes = CreateBuilder().BuildGroups(
            ConfigCommandLine.ParseOptions("--filter-tcp=80 --name=\"My profile\" --dpi-desync=fake"), [], true);

        Assert.Equal("My profile", Assert.Single(nodes).Title);
    }

    [Fact]
    public void SourceSpansAreRetainedForNavigationToTheEditor()
    {
        var options = ConfigCommandLine.ParseOptions("\n  --filter-tcp=80\n  --dpi-desync=fake");

        var groups = CreateBuilder().BuildGroups(options, [], true);
        var leaf = Assert.Single(groups[0].Children[1].Children);

        Assert.True(leaf.IsCode);
        Assert.Same(options[1], leaf.Source);
        Assert.Equal(21, leaf.Source!.SourceIndex);
        Assert.Equal(2, leaf.Source.SourceLine);
        Assert.Equal(2, leaf.Source.SourceColumn);
        Assert.Equal("help:--dpi-desync", leaf.Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData("--new --new")]
    public void EmptyProfilesUseExplicitFallbackDetails(string command)
    {
        var nodes = CreateBuilder().BuildGroups(
            ConfigCommandLine.ParseOptions(command), [new("Fallback", ["first", " ", "second"])], true);

        var group = Assert.Single(nodes);
        Assert.Equal("Fallback", group.Title);
        Assert.Equal(["first", "second"], group.Children.Select(node => node.Title));
        Assert.All(group.Children, node => Assert.True(node.IsCode));
        Assert.All(group.Children, node => Assert.Null(node.Source));
        Assert.False(group.IsExpanded);
    }

    [Fact]
    public void NonZapretComponentsDoNotGetZapretDescriptions()
    {
        var builder = new ConfigMakerPresetTreeBuilder(Localize, path => path, _ => true,
            _ => throw new Exception("Zapret describer must not run"));

        var nodes = builder.BuildGroups(ConfigCommandLine.ParseOptions("--filter-tcp=80 --dpi-desync=fake"), [], false);

        Assert.Empty(nodes[0].Description);
        Assert.All(nodes[0].Children, node => Assert.Empty(node.Description));
        Assert.All(nodes[0].Children.SelectMany(node => node.Children), node => Assert.Empty(node.Description));
    }

    [Fact]
    public void EmptyCommandAndNoExplicitGroupsProduceNoGroups() =>
        Assert.Empty(CreateBuilder().BuildGroups([], [], true));

    [Fact]
    public void EmptyTreeProjectionChangesIdentityForPropertyNotifications()
    {
        // Empty explicit structure and cleared structure have different tab visibility.
        // Both still publish an empty tree; SetProperty must see a new collection.
        var explicitTree = ConfigMakerPresetTreeProjection.Map(Array.Empty<string>(), value => value);
        var clearedTree = ConfigMakerPresetTreeProjection.Map(Array.Empty<string>(), value => value);

        Assert.Empty(explicitTree);
        Assert.Empty(clearedTree);
        Assert.NotSame(explicitTree, clearedTree);
    }

    [Theory]
    [InlineData("en-US", "ä.txt", "z.txt")]
    [InlineData("sv-SE", "z.txt", "ä.txt")]
    public void FileSortingUsesTheCurrentCulture(string culture, string first, string second)
    {
        var previousCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new(culture);
            var nodes = CreateBuilder().BuildFiles([new("ä.txt", "a.txt", ""), new("z.txt", "z.txt", "")]);

            Assert.Equal([first, second], Assert.Single(nodes).Children.Select(node => node.DisplayName));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previousCulture;
        }
    }

    private static ConfigMakerPresetTreeBuilder CreateBuilder() => new(
        Localize, path => "/resolved/" + path,
        path => !path.EndsWith("missing.txt", StringComparison.Ordinal), option => "help:" + option.Name);

    private static string Localize(string key) => key switch
    {
        "ConfigMakerPresetFileCategoryLibraries" => "Libraries",
        "ConfigMakerPresetFileCategoryPayloads" => "Payloads",
        "ConfigMakerPresetFileCategorySiteLists" => "Lists",
        "ConfigMakerPresetFileMissingMessage" => "Missing: {0}",
        "ConfigMakerPresetCommonFiltersGroup" => "Common filters",
        "ConfigMakerPresetCommonOptionsGroup" => "Common options",
        "ConfigMakerPresetProfileFiltersGroup" => "Profile filters",
        "ConfigMakerPresetProfileActionsGroup" => "Profile actions",
        "ConfigMakerPresetProfileFallbackName" => "Profile {0}",
        _ => key,
    };
}
