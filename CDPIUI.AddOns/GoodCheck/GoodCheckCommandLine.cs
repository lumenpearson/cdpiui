using CDPIUI.Core;

namespace CDPIUI.AddOns.GoodCheck;

/// <summary>
/// Builds the GoodCheck arguments for one site list from the add-on settings.
/// </summary>
internal static class GoodCheckCommandLine
{
    public static string Build(string componentName, GoodCheckSiteListModel siteList)
    {
        string addonId = GoodCheckInstallation.AddonId;
        string resolver = SettingsManager.Instance.GetValue<bool>(["ADDONS", addonId], "useCurl") ? "curl" : "native";
        // A number from a text box: anything else would add arguments of its own.
        string passes = SettingsManager.Instance.GetValue<string>(["ADDONS", addonId], "passesValue") ?? string.Empty;
        if (!int.TryParse(passes.Trim(), out int passCount) || passCount < 1)
            passCount = 1;
        return
            $"-q " +
            $"-p {passCount} " +
            $"-f {componentName} " +
            $"-m {resolver} " +
            $"-c \"{siteList.SiteListPath}\" " +
            $"-s \"{siteList.StrategyListPath}\" ";
    }
}
