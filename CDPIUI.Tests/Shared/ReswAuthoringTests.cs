using System.Xml.Linq;

namespace CDPIUI.Tests.Shared;

public class ReswAuthoringTests
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
    public void EveryApplicationCatalogUsesEditableResw()
    {
        string root = Path.Combine(RepositoryRoot, "Localization");
        string[] catalogs = ["EditHostFile", "Flashlight", "GoodCheckAdditionalSettings",
            "GraphicDesignerDescriptions", "Help", "Resources", "SettingTiles", "Tray",
            "Troubleshooting", "UIHelper", "Update", "WelcomeWizard", "Widgets"];
        foreach (string language in new[] { "en-us", "ru" })
        {
            string folder = Path.Combine(root, language);
            Assert.Empty(Directory.GetFiles(folder, "*.json"));
            Assert.Equal(catalogs.Order(), Directory.GetFiles(folder, "*.resw")
                .Select(Path.GetFileNameWithoutExtension).Order());
        }
        Assert.False(File.Exists(Path.Combine(RepositoryRoot, "build", "Generate-Localization.ps1")));
        Assert.False(File.Exists(Path.Combine(RepositoryRoot, "build", "Localization.targets")));
    }

    [Fact]
    public void EnglishReswRetainsTranslatorContext()
    {
        var document = XDocument.Load(Path.Combine(RepositoryRoot, "Localization", "en-us", "Resources.resw"));
        foreach (string key in new[] { "AboutButton.Content", "ORSeparatorTextBlock.Text" })
        {
            var entry = Assert.Single(document.Root!.Elements("data"), element => (string?)element.Attribute("name") == key);
            Assert.False(string.IsNullOrWhiteSpace((string?)entry.Element("comment")), key);
        }
    }
}
