using System.Text;
using CDPIUI.Shared.Localization;

namespace CDPIUI.Tests.Shared;

public class LocalizationFailureTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("not JSON")]
    [InlineData("{}")]
    [InlineData("{\"Message\":\"\"}")]
    [InlineData("{\"Message\":\"   \"}")]
    [InlineData("{\"Message\":null}")]
    public void MissingOrDamagedSelectedCatalogUsesEnglish(string? selectedJson)
    {
        var catalog = new JsonCatalog((_, language) => Stream(language == "ru" ? selectedJson : "{\"Message\":\"Error details: {0}\"}"));
        Assert.Equal("Error details: disk", string.Format(catalog.Get("Errors", "Message", "ru"), "disk"));
    }

    [Fact]
    public void ResourceReadFailureStillUsesEnglish()
    {
        var catalog = new JsonCatalog((_, language) => language == "ru"
            ? throw new IOException("Resource unavailable")
            : Stream("{\"Message\":\"Something went wrong\"}"));
        Assert.Equal("Something went wrong", catalog.Get("Errors", "Message", "ru"));
    }

    [Fact]
    public void FailureOfBothCatalogsStillShowsAResourceId()
    {
        var catalog = new JsonCatalog((_, _) => throw new InvalidOperationException("Resource failure"));
        Assert.Equal("CriticalErrorTitle.Text", catalog.Get("Resources", "CriticalErrorTitle.Text", "ru"));
    }

    [Fact]
    public void FailureReadingPreferenceUsesWindowsLanguage()
    {
        Assert.Equal("ru", LocalePreference.Read(() => throw new IOException("Settings unavailable"), "ru-RU"));
        Assert.Equal("en-us", LocalePreference.Read(() => throw new TypeInitializationException("Settings", null), "de-DE"));
    }

    private static MemoryStream? Stream(string? json) => json == null ? null : new MemoryStream(Encoding.UTF8.GetBytes(json));
}
