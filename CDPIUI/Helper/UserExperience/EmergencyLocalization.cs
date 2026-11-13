using CDPIUI.Shared.Localization;

namespace CDPIUI.Helper.UserExperience;

/// <summary>Exception UI must not depend on WinUI3Localizer, loose files, or readable settings.</summary>
internal static class EmergencyLocalization
{
    internal static string Get(string key) => ReswLocalizer.Get("Resources", key,
        LocalePreference.Read(() => Core.SettingsManager.Instance.GetValueOrDefault<string>(
            "SYSTEM", "language", defaultValue: "")));
}
