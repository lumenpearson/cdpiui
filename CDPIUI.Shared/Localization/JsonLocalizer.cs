#nullable enable
using System;

#if UPDATE_TOOL
namespace Update.Localization;
#elif HOSTS_TOOL
namespace EditHostFile.Localization;
#else
namespace CDPIUI.Shared.Localization;
#endif

/// <summary>Looks up embedded JSON catalogs, with English fallback for an absent translation.</summary>
public static class JsonLocalizer
{
    // Standalone tools link this source and embed their own catalogs. They never load
    // translations or assemblies from the installation directory while running elevated.
    private static readonly JsonCatalog Catalogs = new((catalog, language) =>
        typeof(JsonLocalizer).Assembly.GetManifestResourceStream("Localization." + language + "." + catalog + ".json"));

    public static string Get(string catalog, string key, string? language = null)
    {
        return Catalogs.Get(catalog, key, LocalePreference.Resolve(language));
    }
}
