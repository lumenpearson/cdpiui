#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

#if UPDATE_TOOL
namespace Update.Localization;
#elif HOSTS_TOOL
namespace EditHostFile.Localization;
#else
namespace CDPIUI.Shared.Localization;
#endif

/// <summary>Reads embedded string-only RESW catalogs without external XML dependencies.</summary>
internal sealed class ReswCatalog(Func<string, string, Stream?> openResource)
{
    // The largest current catalog is below 0.5 MiB. This bounds damaged input with ample translation margin.
    private const long MaximumDocumentCharacters = 4L * 1024 * 1024;
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
                    using var reader = XmlReader.Create(stream, new XmlReaderSettings
                    {
                        DtdProcessing = DtdProcessing.Prohibit,
                        XmlResolver = null,
                        MaxCharactersInDocument = MaximumDocumentCharacters
                    });
                    var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
                    var root = document.Root;
                    if (root == null || root.Name != "root")
                        throw new XmlException("A RESW catalog requires a root element.");

                    var values = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var entry in root.Elements("data"))
                    {
                        string? name = (string?)entry.Attribute("name");
                        var value = entry.Element("value");
                        if (string.IsNullOrWhiteSpace(name) || value == null || value.HasElements || entry.Elements("value").Count() != 1)
                            throw new XmlException("A RESW string requires a name and one text value.");
                        // Add rejects duplicate names. A malformed catalog never publishes a partial dictionary.
                        values.Add(name!, value.Value);
                    }
                    return values;
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
            {
                // Do not call the logger, settings or UI here: localization also reports their failures.
            }
            return new Dictionary<string, string>();
        });
}
