using System.Text;
using CDPIUI.Shared.Localization;

namespace CDPIUI.Tests.Shared;

public class LocalizationFailureTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("not XML")]
    [InlineData("<root />")]
    [InlineData("<root><data name=\"Message\"><value /></data></root>")]
    [InlineData("<root><data name=\"Message\"><value>   </value></data></root>")]
    [InlineData("<root><data name=\"Message\" /></root>")]
    [InlineData("<root><data name=\"Message\"><value>incomplete")]
    [InlineData("<translations><data name=\"Message\"><value>Wrong root</value></data></translations>")]
    [InlineData("<root><data name=\"Message\"><value>first</value></data><data name=\"Message\"><value>second</value></data></root>")]
    [InlineData("<!DOCTYPE root [<!ENTITY text 'untrusted'>]><root><data name=\"Message\"><value>&text;</value></data></root>")]
    public void MissingOrDamagedSelectedCatalogUsesEnglish(string? selectedXml)
    {
        var catalog = new ReswCatalog((_, language) => Stream(language == "ru" ? selectedXml : Xml("Error details: {0}")));
        Assert.Equal("Error details: disk", string.Format(catalog.Get("Errors", "Message", "ru"), "disk"));
    }

    [Fact]
    public void ResourceReadFailureStillUsesEnglish()
    {
        var catalog = new ReswCatalog((_, language) => language == "ru"
            ? throw new IOException("Resource unavailable")
            : Stream(Xml("Something went wrong")));
        Assert.Equal("Something went wrong", catalog.Get("Errors", "Message", "ru"));
    }

    [Fact]
    public void FailureOfBothCatalogsStillShowsAResourceId()
    {
        var catalog = new ReswCatalog((_, _) => throw new InvalidOperationException("Resource failure"));
        Assert.Equal("CriticalErrorTitle.Text", catalog.Get("Resources", "CriticalErrorTitle.Text", "ru"));
    }

    [Fact]
    public void FailureReadingPreferenceUsesWindowsLanguage()
    {
        Assert.Equal("ru", LocalePreference.Read(() => throw new IOException("Settings unavailable"), "ru-RU"));
        Assert.Equal("en-us", LocalePreference.Read(() => throw new TypeInitializationException("Settings", null), "de-DE"));
    }

    [Fact]
    public void OversizedSelectedCatalogUsesEnglish()
    {
        string oversized = Xml(new string('x', 4 * 1024 * 1024));
        var catalog = new ReswCatalog((_, language) => Stream(language == "ru" ? oversized : Xml("English")));
        Assert.Equal("English", catalog.Get("Errors", "Message", "ru"));
    }

    [Fact]
    public void CatalogCacheKeepsLanguagesAndCatalogNamesSeparate()
    {
        var reads = new Dictionary<(string, string), int>();
        var catalog = new ReswCatalog((name, language) =>
        {
            reads[(name, language)] = reads.GetValueOrDefault((name, language)) + 1;
            return Stream(Xml(name + ":" + language));
        });
        Assert.Equal("Errors:ru", catalog.Get("Errors", "Message", "ru"));
        Assert.Equal("Errors:ru", catalog.Get("Errors", "Message", "ru"));
        Assert.Equal("Errors:en-us", catalog.Get("Errors", "Message", "en-us"));
        Assert.Equal("Tray:ru", catalog.Get("Tray", "Message", "ru"));
        Assert.Equal(3, reads.Count);
        Assert.All(reads.Values, count => Assert.Equal(1, count));
    }

    private static string Xml(string value) => "<root><data name=\"Message\"><value>" + value + "</value></data></root>";

    private static MemoryStream? Stream(string? xml) => xml == null ? null : new MemoryStream(Encoding.UTF8.GetBytes(xml));
}
