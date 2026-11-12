#nullable enable
using System;

#if UPDATE_TOOL
namespace Update.Localization;
#elif HOSTS_TOOL
namespace EditHostFile.Localization;
#else
namespace CDPIUI.Shared.Localization;
#endif

/// <summary>Looks up embedded RESW catalogs, with English fallback for an absent translation.</summary>
public static class ReswLocalizer
{
    // Standalone tools link this source and embed their own catalogs. They never load
    // translations or assemblies from the installation directory while running elevated.
    private static readonly ReswCatalog Catalogs = new((catalog, language) =>
        typeof(ReswLocalizer).Assembly.GetManifestResourceStream("Localization." + language + "." + catalog + ".resw"));

    public static string Get(string catalog, string key, string? language = null)
    {
        return Catalogs.Get(catalog, key, LocalePreference.Resolve(language));
    }
}
