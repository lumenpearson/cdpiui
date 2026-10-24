using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using CDPIUI.Core;
using Windows.Storage;
using WinUI3Localizer;

namespace CDPIUI.Helper.Basic;

/// <summary>
/// Builds the application localizer from the Strings folder in the language chosen in the settings.
/// </summary>
internal static class AppLocalizerInitializer
{
    internal static async Task InitializeAsync()
    {
        string stringsFolderPath = Path.Combine(AppContext.BaseDirectory, "Strings");
        StorageFolder stringsFolder = await StorageFolder.GetFolderFromPathAsync(stringsFolderPath);

        string lang = SettingsManager.Instance.GetValue<string>("SYSTEM", "language");
        var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        // An unknown or damaged value would leave every text empty (issue #8).
        if (string.IsNullOrWhiteSpace(lang) || lang == "NaN" ||
            lang.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            !Directory.Exists(Path.Combine(stringsFolderPath, lang)))
        {
            lang = culture switch
            {
                "en" => "en-us",
                "ru" => "ru",
                _ => "en-us",
            };
        }

        ILocalizer localizer = await new LocalizerBuilder()
            .AddStringResourcesFolderForLanguageDictionaries(stringsFolderPath)
            .SetOptions(options =>
            {
                options.DefaultLanguage = lang;
            })
            .Build();
    }
}
