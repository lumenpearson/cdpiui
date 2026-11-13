using System.Xml.Linq;

namespace CDPIUI.Tests.Shared;

public class ReswProjectLayoutTests
{
    private static readonly string[] Languages = ["en-us", "ru"];
    private static readonly string[] Catalogs = ["EditHostFile", "Flashlight", "GoodCheckAdditionalSettings",
        "GraphicDesignerDescriptions", "Help", "Resources", "SettingTiles", "Tray",
        "Troubleshooting", "UIHelper", "Update", "WelcomeWizard", "Widgets"];

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CDPI.sln")))
                directory = directory.Parent;
            return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
        }
    }

    [Fact]
    public void GuiResourcesHaveAllRuntimeAndCompiledDestinations()
    {
        var project = ReadProject("CDPIUI");
        var content = Expand(project, "Content", "CDPIUI").Where(item => item.Source.EndsWith(".resw", StringComparison.Ordinal)).ToArray();
        string[] expected = [.. Languages.SelectMany(language => Catalogs
            .Where(catalog => catalog is not "Tray" and not "Update" and not "EditHostFile")
            .Select(catalog => "Strings/" + language + "/" + catalog + ".resw"))];
        Assert.Equal(expected.Order(), content.Select(item => Metadata(item, "Link")).Order());
        Assert.Equal(expected.Order(), content.Select(item => Metadata(item, "TargetPath")).Order());
        foreach (var item in content)
        {
            Assert.Equal("PreserveNewest", Metadata(item, "CopyToOutputDirectory"));
            Assert.Equal("PreserveNewest", Metadata(item, "CopyToPublishDirectory"));
            Assert.Equal("Localization/" + Metadata(item, "Link")["Strings/".Length..], item.Source);
        }

        var pri = Expand(project, "PRIResource", "CDPIUI").ToArray();
        string[] expectedPri = [.. Languages.Select(language => "Strings/" + language + "/Resources.resw")];
        Assert.Equal(expectedPri.Order(), pri.Select(item => Metadata(item, "Link")).Order());
        foreach (var item in pri)
            Assert.Equal("Localization/" + Metadata(item, "Link")["Strings/".Length..], item.Source);
    }

    [Fact]
    public void SharedAndStandaloneManifestNamesMatchCatalogContract()
    {
        foreach (string projectName in new[] { "CDPIUI.Shared", "Update", "EditHostFile" })
        {
            var items = Expand(ReadProject(projectName), "EmbeddedResource", projectName)
                .Where(item => item.Source.StartsWith("Localization/", StringComparison.Ordinal)).ToArray();
            string[] catalogs = projectName == "CDPIUI.Shared" ? Catalogs : [projectName];
            string[] expected = [.. Languages.SelectMany(language => catalogs
                .Select(catalog => "Localization." + language + "." + catalog + ".resw"))];
            Assert.Equal(expected.Order(), items.Select(item => Metadata(item, "LogicalName")).Order());
            foreach (var item in items)
                Assert.Equal(item.Source.Replace('/', '.'), Metadata(item, "LogicalName"));
        }
    }

    private static XDocument ReadProject(string name) =>
        XDocument.Load(Path.Combine(RepositoryRoot, name, name + ".csproj"));

    // These declarations use literal local paths and Filename metadata. Keep this source-level
    // contract explicit; actual MSBuild outputs and PRI maps retain their separate artifact check.
    private static IEnumerable<(XElement Declaration, string Source)> Expand(XDocument project, string kind, string projectName)
    {
        string directory = Path.Combine(RepositoryRoot, projectName);
        foreach (var declaration in project.Descendants(kind).Where(element => element.Attribute("Include") != null))
        {
            string include = ((string)declaration.Attribute("Include")!).Replace('\\', '/');
            string absolute = Path.GetFullPath(Path.Combine(directory, include));
            // Non-localization assets are outside this contract and can use other MSBuild metadata.
            if (!absolute.StartsWith(Path.Combine(RepositoryRoot, "Localization") + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                continue;
            Assert.Null(declaration.Attribute("Condition"));
            string[] excluded = ((string?)declaration.Attribute("Exclude") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(path => Path.GetFullPath(Path.Combine(directory, path.Replace('\\', '/')))).ToArray();
            string[] files = include.Contains('*')
                ? Directory.GetFiles(Path.GetDirectoryName(absolute)!, Path.GetFileName(absolute))
                : [absolute];
            foreach (string file in files.Except(excluded, StringComparer.Ordinal))
                yield return (declaration, Path.GetRelativePath(RepositoryRoot, file).Replace('\\', '/'));
        }
    }

    private static string Metadata((XElement Declaration, string Source) item, string name) =>
        (((string?)item.Declaration.Attribute(name) ?? (string?)item.Declaration.Element(name)) ?? "")
        .Replace("%(Filename)", Path.GetFileNameWithoutExtension(item.Source)).Replace('\\', '/');
}
