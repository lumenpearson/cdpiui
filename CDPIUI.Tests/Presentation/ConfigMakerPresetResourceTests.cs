using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.ViewModels;

namespace CDPIUI.Tests.Presentation;

public class ConfigMakerPresetResourceTests
{
    [Theory]
    [InlineData("hosts.txt", "hosts.txt")]
    [InlineData("  list ?.txt", "list__.txt")]
    [InlineData("список.txt", "spisok.txt")]
    [InlineData("???", "resource.bin")]
    public void AliasIsSafeAndRetainsTheFileExtension(string fileName, string expected)
    {
        using var fixture = new ConfigMakerResourceFixture();

        Assert.Equal(expected, fixture.Registry.CreateUniqueAlias(fileName));
    }

    [Fact]
    public void AliasesAreUniqueIgnoringCase()
    {
        using var fixture = new ConfigMakerResourceFixture();
        fixture.Resources.Add(new() { Alias = "HOSTS.txt" });
        fixture.Resources.Add(new() { Alias = "hosts_2.TXT" });

        Assert.Equal("hosts_3.txt", fixture.Registry.CreateUniqueAlias("hosts.txt"));
    }

    [Theory]
    [InlineData("hosts.txt")]
    [InlineData("PRESET://HOSTS.TXT")]
    [InlineData("  'preset://hosts.txt' ")]
    public void AttachedResourceLookupAcceptsQuotedReferencesAndAliases(string reference)
    {
        using var fixture = new ConfigMakerResourceFixture();
        var resource = new ConfigMakerPresetResource { Alias = "hosts.txt" };
        fixture.Resources.Add(resource);

        Assert.Same(resource, fixture.Registry.FindAttached(reference));
    }

    [Fact]
    public void AddingFilesSkipsMissingAndRepeatedPaths()
    {
        using var fixture = new ConfigMakerResourceFixture();
        string file = fixture.CreateFile("hosts.txt");

        int added = fixture.Registry.AddFiles(["", fixture.PathFor("missing.txt"), file, file.ToUpperInvariant()]);

        Assert.Equal(1, added);
        var resource = Assert.Single(fixture.Resources);
        Assert.Equal("preset://hosts.txt", resource.Reference);
        Assert.Equal(file, resource.Path);
        Assert.Equal(ConfigMakerResourceKind.SiteList, resource.Kind);
        Assert.False(resource.IsBuiltIn);
    }

    [Fact]
    public void GetOrCreateReusesResolvedPathsAndKeepsTheOriginalKind()
    {
        using var fixture = new ConfigMakerResourceFixture();
        string file = fixture.CreateFile("data.bin");
        var first = fixture.Registry.GetOrCreate(file, ConfigMakerPresetFileKind.Payload, out bool added);

        var second = fixture.Registry.GetOrCreate(file.ToUpperInvariant(), ConfigMakerPresetFileKind.Library, out bool addedAgain);

        Assert.True(added);
        Assert.False(addedAgain);
        Assert.Same(first, second);
        Assert.Single(fixture.Resources);
        Assert.Equal(ConfigMakerResourceKind.Payload, second.Kind);
    }

    [Fact]
    public void ExistingPresetReferencesAndMissingFilesAreNotAttachedAgain()
    {
        using var fixture = new ConfigMakerResourceFixture();

        Assert.Null(fixture.Registry.GetOrCreate(new("hosts", "preset://hosts", ""), out bool aliasAdded));
        Assert.Null(fixture.Registry.GetOrCreate(new("missing", fixture.PathFor("missing.txt"), ""), out bool missingAdded));
        Assert.False(aliasAdded);
        Assert.False(missingAdded);
        Assert.Empty(fixture.Resources);
    }

    [Fact]
    public void ReplacingAnAttachedFileRetainsItsAliasAndUpdatesBuiltInState()
    {
        using var fixture = new ConfigMakerResourceFixture();
        string componentFile = fixture.CreateFile("component/init.lua");
        var resource = new ConfigMakerPresetResource { Alias = "custom.lua", Path = fixture.CreateFile("external.lua") };
        fixture.Resources.Add(resource);

        bool changed = fixture.Registry.TryUpdateAttachedResource(new("custom", resource.Reference, ""), componentFile);

        Assert.True(changed);
        Assert.Equal("custom.lua", resource.Alias);
        Assert.Equal(componentFile, resource.Path);
        Assert.True(resource.IsBuiltIn);
        Assert.False(fixture.Registry.TryUpdateAttachedResource(new("other", "preset://other", ""), componentFile));
    }
}

internal sealed class ConfigMakerResourceFixture : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cdpiui-preset-tests", Guid.NewGuid().ToString("N"));

    public ConfigMakerResourceFixture()
    {
        Directory.CreateDirectory(_root);
        Registry = new(Resources, ResolvePath, path => path.StartsWith(PathFor("component") + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase));
    }

    public List<ConfigMakerPresetResource> Resources { get; } = [];
    public List<ConfigMakerVariableDefinition> Variables { get; } = [];
    public ConfigMakerPresetResourceRegistry Registry { get; }

    public string PathFor(string relative) => Path.Combine(_root, relative);

    public string CreateFile(string relative)
    {
        string path = Path.GetFullPath(PathFor(relative));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "fixture");
        return path;
    }

    public ConfigMakerPresetReferenceEditor CreateEditor() => new(
        Variables, Registry,
        text => CDPIUI.Core.ComponentServices.Helpers.Configuration.ConfigFileReferences.ExtractFromText(text)
            .Select(file => ConfigMakerPresetFileKinds.ToEditorFile(file)!),
        value => ConfigMakerPresetFileKinds.ToEditorFile(
            CDPIUI.Core.ComponentServices.Helpers.Configuration.ConfigFileReferences.ExtractDirectFile(value)));

    private string ResolvePath(string reference)
    {
        var resource = Registry.FindAttached(reference);
        return Path.GetFullPath(resource?.Path ?? reference);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
