#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;

#if UPDATE_TOOL
namespace Update.Localization;
#elif HOSTS_TOOL
namespace EditHostFile.Localization;
#else
namespace CDPIUI.Shared.Localization;
#endif

internal sealed class JsonCatalog(Func<string, string, Stream?> openResource)
{
    private readonly ConcurrentDictionary<(string Catalog, string Language), Dictionary<string, string>> _catalogs = new();

    internal string Get(string catalog, string key, string language)
    {
        if (Read(catalog, language).TryGetValue(key, out string? text) && !string.IsNullOrWhiteSpace(text))
            return text;
        if (Read(catalog, "en-us").TryGetValue(key, out text) && !string.IsNullOrWhiteSpace(text))
            return text;
        // Even a broken deployment must not turn an error dialog's labels into empty strings.
        return key;
    }

    private Dictionary<string, string> Read(string catalog, string language) =>
        _catalogs.GetOrAdd((catalog, language), resource =>
        {
            try
            {
                using var stream = openResource(resource.Catalog, resource.Language);
                if (stream != null)
                {
                    var serializer = new DataContractJsonSerializer(typeof(Dictionary<string, string>),
                        new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
                    return (Dictionary<string, string>?)serializer.ReadObject(stream) ?? new Dictionary<string, string>();
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
            {
                // Do not call the logger or UI here: localization is also used to report their failures.
            }
            return new Dictionary<string, string>();
        });
}
