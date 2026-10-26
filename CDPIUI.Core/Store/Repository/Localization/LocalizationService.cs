using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Core.JSON;

namespace CDPIUI.Core.Store.Repository.Localization;

public class StoreLocalizationHelper()
{
    /// <summary>
    /// Get store-like localization key from default localization key
    /// </summary>
    /// <returns>Store-like localization key</returns>
    public static string GetStoreLikeLocale()
    {
        return ApplicationInfo.Instance.Localization switch
        {
            "ru" => "RU",
            "en-us" or "en-US" => "EN",
            _ => "EN",
        };
    }
}

internal class LocalizationService : ILocalizationService
{
    public string? LocalizationName;
    public Dictionary<string, string>? LocalizationDict;

    private Dictionary<string, string> _storeLocalizationPaths = [];
    private readonly object _cacheLock = new();

    // Set after every repository sync: the cached strings were kept from the previous one.
    public Dictionary<string, string> StoreLocalizationPaths
    {
        get => _storeLocalizationPaths;
        set
        {
            lock (_cacheLock)
            {
                _storeLocalizationPaths = value ?? [];
                LocalizationName = null;
                LocalizationDict = null;
            }
        }
    }

    public string GetLocalizedStoreItemName(string? name, string langCode)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        string localizedString = $"slocale:{name}";

        string localRepoFolder = Directories.StoreRepoCacheDirectory;

        if (name.Contains(' '))
            return name;

        try
        {
            lock (_cacheLock)
            {
                if (LocalizationName != langCode || LocalizationDict == null)
                {
                    string locFilePath;
                    if (!StoreLocalizationPaths.TryGetValue(langCode, out string value))
                    {
                        locFilePath = Path.Combine(localRepoFolder, StoreLocalizationPaths["EN"]);
                    }
                    else
                    {
                        locFilePath = Path.Combine(localRepoFolder, value);
                    }

                    using (StreamReader r = new StreamReader(locFilePath))
                    {
                        string json = r.ReadToEnd();
                        Dictionary<string, string> localizationDict = JSONConvertor.DeserializeObject<Dictionary<string, string>>(json);

                        LocalizationName = langCode;
                        LocalizationDict = localizationDict;
                    }

                }
                if (LocalizationDict != null && LocalizationDict.TryGetValue(name, out string? localized))
                    localizedString = localized;
            }
        }
        catch (Exception)
        {
            Logger.Instance.CreateWarningLog(nameof(LocalizationService), $"Cannot get localization for {name} [{langCode}]");
        }

        return localizedString;
    }
}
