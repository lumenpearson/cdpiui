using System;
using System.IO;
using System.Threading.Tasks;
using CDPIUI.Core;
using CDPIUI.Shared.Localization;
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

        string lang = LocalePreference.Resolve(SettingsManager.Instance.GetValueOrDefault<string>(
            "SYSTEM", "language", defaultValue: ""));

        ILocalizer localizer = await new LocalizerBuilder()
            .AddStringResourcesFolderForLanguageDictionaries(stringsFolderPath)
            .SetOptions(options =>
            {
                options.DefaultLanguage = "en-us";
            })
            .Build();
        await localizer.SetLanguage(lang);
    }
}
