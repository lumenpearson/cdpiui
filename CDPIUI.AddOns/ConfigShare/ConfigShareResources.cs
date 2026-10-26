using System.Security.Cryptography;
using System.Text;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;

namespace CDPIUI.AddOns.ConfigShare;

internal sealed class ConfigShareResources(ConfigItem config, string packDirectory, string? componentDirectory)
{
    internal sealed record Resource(string ArchivePath);
    internal Dictionary<string, Resource> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _replacements = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _emptyResources = new(StringComparer.OrdinalIgnoreCase);

    // A list that the component creates while it runs and that does not exist yet.
    internal bool IsEmptyResource(string source) => _emptyResources.Contains(source);

    internal void Collect()
    {
        foreach (ConfigUsedFile file in config.UsedFiles)
        {
            string source;
            try
            { source = config.ResolveFilePath(file.Path, componentDirectory ?? "", packDirectory); }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
            {
                throw new ConfigShareException("SHARE_FILE_MISSING", file.Path, exception);
            }
            // winws creates the --hostlist-auto list while it runs, so before the first run there is
            // no file and the preset could not be shared. It is shared as an empty list;
            // the user's kit is not changed.
            if (!File.Exists(source) && IsRuntimeCreated(file.OptionName))
                _emptyResources.Add(source);
            if (!File.Exists(source) && !_emptyResources.Contains(source))
                throw new ConfigShareException("SHARE_FILE_MISSING", file.Path);
            if (!Files.TryGetValue(source, out var resource))
            {
                if (Files.Count >= 4095)
                    throw new ConfigShareException("SHARE_TOO_LARGE", "Too many resource files.");
                string archivePath = GetArchivePath(source);
                ConfigShareService.SafeDestination(packDirectory, archivePath);
                resource = new Resource(archivePath);
                Files.Add(source, resource);
            }
            string portable = ConfigShareService.ReferencePrefix + resource.ArchivePath;
            AddReplacement(file.Path, portable);
            AddReplacement(file.ExpandedPath, portable);
            AddReplacement(source, portable);
        }
        foreach (var metadata in config.configMaker?.resources ?? [])
        {
            string expanded = config.ExpandFileReference(metadata.path);
            if (!_replacements.TryGetValue(expanded, out string? portable))
                continue;
            if (!string.IsNullOrWhiteSpace(metadata.alias))
                AddReplacement("preset://" + metadata.alias, portable);
            metadata.isBuiltIn = false;
        }
        MapConfig(config, text => ConfigShareService.ReplaceConfigReferences(text, _replacements));
    }

    private static bool IsRuntimeCreated(string? optionName) =>
        string.Equals(optionName, "--hostlist-auto", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(optionName, "--hostlist-auto-debug", StringComparison.OrdinalIgnoreCase);

    private void AddReplacement(string reference, string portable)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return;
        if (reference.StartsWith('%') && reference.EndsWith('%') && reference.IndexOf('%', 1) == reference.Length - 1)
            return;
        _replacements[reference] = portable;
        _replacements[reference.Replace('\\', '/')] = portable;
        _replacements[reference.Replace('/', '\\')] = portable;
    }

    private string GetArchivePath(string path)
    {
        foreach (var (root, category) in new[] { (packDirectory, "preset"), (componentDirectory, "component") })
        {
            if (string.IsNullOrWhiteSpace(root))
                continue;
            string prefix = Path.GetFullPath(root).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            if (path.TrimEnd('\\', '/').Equals(prefix.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                return "resources/" + category;
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return "resources/" + category + "/" + Path.GetRelativePath(root, path).Replace('\\', '/');
        }
        string volume = Path.GetPathRoot(path)!;
        string volumeId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(volume.ToUpperInvariant())))[..12];
        string relative = Path.GetRelativePath(volume, path).Replace('\\', '/');
        return "resources/external/" + volumeId + (relative == "." ? "" : "/" + relative);
    }

    internal static void MapConfig(ConfigItem item, Func<string, string> map)
    {
        string? Map(string? text) => text == null ? null : map(text);
        void MapList(List<string>? list)
        {
            if (list != null)
                for (int index = 0; index < list.Count; index++)
                    list[index] = Map(list[index])!;
        }
        item.startup_string = Map(item.startup_string);
        MapList(item.variables);
        if (item.commaVars != null)
            foreach (string key in item.commaVars.Keys.ToArray())
                item.commaVars[key] = Map(item.commaVars[key])!;
        foreach (var choice in item.availableCommaVarsValues ?? [])
            MapList(choice.Values);
        foreach (var variable in item.configMaker?.variables ?? [])
        {
            variable.value = Map(variable.value);
            variable.onValue = Map(variable.onValue);
            variable.offValue = Map(variable.offValue);
            MapList(variable.values);
        }
        foreach (var resource in item.configMaker?.resources ?? [])
            resource.path = Map(resource.path);
    }
}
