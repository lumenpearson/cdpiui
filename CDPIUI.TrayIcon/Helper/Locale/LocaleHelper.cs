using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI.TrayIcon.Helper;

public class LocaleHelper
{
    public static readonly Dictionary<string, string> ComponentNames = new()
    {
        { "CSZTBN012", "Zapret Legacy" },
        { "CSZTBN062", "Zapret2" },
        { "CSGIVS036", "GoodbyeDPI" },
        { "CSBIHA024", "ByeDPI" },
        { "CSSIXC048", "SpoofDPI" },
        { "CSNIG9025", "NoDPI" },
        { "CSTYFL050", "TG WS Proxy" },
    };

    public static string GetLocalizedComponentName(string id)
    {
        if (ComponentNames.TryGetValue(id, out var name))
            return name;
        return id;
    }

    public static string GetLocaleString(string key) =>
        CDPIUI.Shared.Localization.JsonLocalizer.Get("Tray", key, GetLanguage());

    /// <summary>
    /// Returns the language that the user selected in the app ("SYSTEM/language" in Settings.xml).
    /// Without a usable value, the Windows display language is used, as in the app.
    /// </summary>
    internal static string GetLanguage()
    {
        string? language = null;
        try
        {
            // The setting is only read: a missing key must not write a default into the user's file.
            language = SettingsManager.Instance.GetValue<string>("SYSTEM", "language", raiseExceptionIfNotExits: true);
        }
        catch (Exception)
        {
            language = null;
        }
        return ToTrayLanguage(language, System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    }

    internal static string ToTrayLanguage(string? appLanguage, string windowsLanguage)
    {
        return CDPIUI.Shared.Localization.LocalePreference.Resolve(appLanguage, windowsLanguage) == "ru" ? "ru" : "en";
    }
}
