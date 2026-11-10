using System.Globalization;
using CDPIUI.Shared.Localization;

namespace CDPIUI.Tests.Shared;

public class JsonLocalizerTests
{
    [Theory]
    [InlineData("ru", "en-US", "ru")]
    [InlineData("RU-ru", "en-US", "ru")]
    [InlineData(" en_GB ", "ru-RU", "en-us")]
    [InlineData("en-us", "ru-RU", "en-us")]
    [InlineData(null, "ru-RU", "ru")]
    [InlineData("NaN", "ru", "ru")]
    [InlineData("../../ru", "en", "en-us")]
    [InlineData("russian", "de-DE", "en-us")]
    [InlineData("enough", "ru", "ru")]
    [InlineData("fr-FR", "fr-FR", "en-us")]
    public void UsesSelectedLanguageBeforeWindowsAndEnglishFallback(string? selected, string windows, string expected) =>
        Assert.Equal(expected, LocalePreference.Resolve(selected, windows));

    [Fact]
    public void EmbeddedCatalogsContainBothLanguagesAndMultilineFormats()
    {
        Assert.Equal("Exit", JsonLocalizer.Get("Tray", "Exit", "en-US"));
        Assert.Equal("Выход", JsonLocalizer.Get("Tray", "Exit", "ru-RU"));
        string template = JsonLocalizer.Get("Tray", "ExecutableTrustQuestion", "ru");
        string text = string.Format(template, "component.exe", "hash", "publisher");
        Assert.Contains("component.exe", text);
        Assert.Contains("\n\n", text);
        Assert.Contains("hash", text);
        Assert.Contains("publisher", text);
        Assert.Equal("Critical error", JsonLocalizer.Get("Resources", "CriticalErrorWindowTitle", "en"));
        Assert.Equal("Критическая ошибка", JsonLocalizer.Get("Resources", "CriticalErrorWindowTitle", "ru"));
        Assert.Equal("MissingKey", JsonLocalizer.Get("Tray", "MissingKey", "ru"));
    }

    [Fact]
    public void StandaloneToolsUseSelectedLanguageEvenWithDifferentWindowsLanguage()
    {
        CultureInfo previousCulture = CultureInfo.CurrentUICulture;
        string previousUpdateLanguage = Update.Strings.Language;
        string previousHostsLanguage = EditHostFile.Strings.Language;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            Update.Strings.Language = Update.Localization.LocalePreference.FromArguments(["--language", "ru-RU"]);
            EditHostFile.Strings.Language = EditHostFile.Localization.LocalePreference.FromArguments(["/add", "--language", "ru"]);
            Assert.Equal("Помощник обновления", Update.Strings.WindowTitle);
            Assert.Equal("CDPI UI: редактор файла hosts", EditHostFile.Strings.WindowTitle);
            Assert.Contains("123", string.Format(Update.Strings.ErrorMessage, 123));

            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
            Update.Strings.Language = Update.Localization.LocalePreference.FromArguments(["--language", "en-us"]);
            EditHostFile.Strings.Language = EditHostFile.Localization.LocalePreference.FromArguments(["--language", "en"]);
            Assert.Equal("Update Assistant", Update.Strings.WindowTitle);
            Assert.Equal("CDPI UI: hosts file editor", EditHostFile.Strings.WindowTitle);
            Assert.Equal("ru", EditHostFile.Localization.LocalePreference.FromArguments(["--language"]));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
            Update.Strings.Language = previousUpdateLanguage;
            EditHostFile.Strings.Language = previousHostsLanguage;
        }
    }
}
