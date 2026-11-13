using System.Xml;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CDPIUI.Shared.Localization;

namespace CDPIUI.Tests.Shared;

public class LocalizationCatalogTests
{
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
    public void CatalogsHaveMatchingKeysAndFormattingArguments()
    {
        string root = Path.Combine(RepositoryRoot, "Localization");
        Assert.True(Directory.Exists(Path.Combine(root, "en-us")), "RESW localization catalogs must exist.");
        foreach (string englishPath in Directory.GetFiles(Path.Combine(root, "en-us"), "*.resw"))
        {
            var english = Read(englishPath);
            var russian = Read(Path.Combine(root, "ru", Path.GetFileName(englishPath)));
            Assert.Equal(english.Keys.Order(), russian.Keys.Order());
            foreach ((string key, string value) in english)
            {
                Assert.False(string.IsNullOrWhiteSpace(value), $"Empty English resource: {englishPath}/{key}");
                Assert.False(string.IsNullOrWhiteSpace(russian[key]), $"Empty Russian resource: {englishPath}/{key}");
                Assert.Equal(Arguments(value), Arguments(russian[key]));
                Assert.Equal(value, ReswLocalizer.Get(Path.GetFileNameWithoutExtension(englishPath), key, "en-us"));
                Assert.Equal(russian[key], ReswLocalizer.Get(Path.GetFileNameWithoutExtension(englishPath), key, "ru"));
            }
        }
    }

    [Fact]
    public void XamlUsesResourcesForVisibleCopy()
    {
        var visibleProperties = new HashSet<string>
        {
            "Text", "Content", "Header", "Title", "Description", "PlaceholderText",
            "PrimaryButtonText", "SecondaryButtonText", "CloseButtonText", "CardTitle",
            "ToolTipService.ToolTip", "AutomationProperties.Name"
        };
        // Product/author names and physical key names are intentionally invariant.
        var invariantNames = new HashSet<string>
        {
            "CDPI UI", "Storik4", "Zapret", "ByeDPI", "GoodbyeDPI", "SpoofDPI",
            "Ctrl", "Alt", "Shift", "Win"
        };
        foreach (string folder in new[] { "Controls", "Views", "Windows" })
        foreach (string path in Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "CDPIUI", folder), "*.xaml", SearchOption.AllDirectories))
        {
            var document = XDocument.Load(path);
            foreach (var setter in document.Descendants().Where(element => element.Name.LocalName == "Setter"))
            {
                string property = (string?)setter.Attribute("Property") ?? "";
                string value = (string?)setter.Attribute("Value") ?? "";
                if (visibleProperties.Contains(property) && !value.StartsWith('{') && Regex.IsMatch(value, @"\p{L}") && !invariantNames.Contains(value))
                    Assert.Fail($"Visible style text must come from localization: {path}: {setter}");
            }
            foreach (var attribute in document.Descendants().Attributes())
            {
                if (visibleProperties.Contains(attribute.Name.LocalName) && !attribute.Value.StartsWith('{') &&
                    Regex.IsMatch(attribute.Value, @"\p{L}") && !invariantNames.Contains(attribute.Value))
                    Assert.Fail($"Visible text must come from localization: {path}: {attribute}");
            }
        }
    }

    [Fact]
    public void VisibleToggleStateLabelsUseTheApplicationLocale()
    {
        var english = Read(Path.Combine(RepositoryRoot, "Localization", "en-us", "Resources.resw"));
        var russian = Read(Path.Combine(RepositoryRoot, "Localization", "ru", "Resources.resw"));
        XNamespace localizer = "using:WinUI3Localizer";
        foreach (string folder in new[] { "Controls", "Views", "Windows" })
        foreach (string path in Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "CDPIUI", folder), "*.xaml", SearchOption.AllDirectories))
        foreach (var toggle in XDocument.Load(path).Descendants().Where(element => element.Name.LocalName == "ToggleSwitch"))
        {
            if ((string?)toggle.Attribute("OnContent") == "" && (string?)toggle.Attribute("OffContent") == "")
                continue; // The compact config editor intentionally hides both state labels.
            string? uid = (string?)toggle.Attribute(localizer + "Uids.Uid");
            Assert.True(uid != null, $"Toggle state labels must not inherit the Windows language: {path}");
            Assert.Equal("On", english[uid + ".OnContent"]);
            Assert.Equal("Off", english[uid + ".OffContent"]);
            Assert.Equal("Вкл.", russian[uid + ".OnContent"]);
            Assert.Equal("Откл.", russian[uid + ".OffContent"]);
        }
    }

    private static Dictionary<string, string> Read(string path)
    {
        using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        Assert.Equal("root", document.Root!.Name.LocalName);
        // ToDictionary rejects duplicate names instead of silently overwriting a translation.
        return document.Root.Elements("data").ToDictionary(
            element => (string?)element.Attribute("name") ?? throw new InvalidDataException("Missing resource name."),
            element => element.Element("value")?.Value ?? throw new InvalidDataException("Missing resource value."));
    }

    private static string[] Arguments(string text) => Regex.Matches(text, @"(?<!\{)\{(\d+)(?:[^{}]*)\}(?!\})")
        .Select(match => match.Groups[1].Value).Distinct().Order().ToArray();
}
