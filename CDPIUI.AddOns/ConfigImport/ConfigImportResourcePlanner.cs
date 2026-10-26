using System.Security.Cryptography;
using System.Text;
using CDPIUI.Shared;

namespace CDPIUI.AddOns.ConfigImport;

/// <summary>
/// Decides where each file of an imported Config is saved: a category folder by file type and a
/// per-import folder, so imports cannot overwrite each other's files.
/// </summary>
internal static class ConfigImportResourcePlanner
{
    public const string ConvertedDirectoryName = "Converted";
    private const string LuaDirectoryName = "Lua";

    public static List<ConfigImportResource> Build(
        ConfigImportResult result,
        string sourceDirectory,
        string importId)
    {
        var resolutions = result.MissingFileResolutions.ToDictionary(
            resolution => Path.GetFullPath(resolution.MissingPath),
            resolution => resolution,
            StringComparer.OrdinalIgnoreCase);
        var resources = new List<ConfigImportResource>();
        var expectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var savedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddResource(string expectedPath, string? sourcePath, string? generatedContent, string? relativePath = null)
        {
            string fullExpectedPath = Path.GetFullPath(expectedPath);
            if (!expectedPaths.Add(fullExpectedPath))
                return;

            string sourceRelativePath = relativePath ?? GetSourceRelativePath(sourceDirectory, fullExpectedPath);
            string category = GetResourceCategory(fullExpectedPath);
            string organizedRelativePath = GetOrganizedRelativePath(sourceRelativePath, category);
            string savedRelativePath = $"{category}/{ConvertedDirectoryName}/{importId}/{organizedRelativePath.Replace('\\', '/')}";
            if (!savedPaths.Add(savedRelativePath))
            {
                organizedRelativePath = AddPathHash(organizedRelativePath, fullExpectedPath);
                savedRelativePath = $"{category}/{ConvertedDirectoryName}/{importId}/{organizedRelativePath.Replace('\\', '/')}";
                savedPaths.Add(savedRelativePath);
            }

            resources.Add(new ConfigImportResource(
                fullExpectedPath,
                sourceRelativePath,
                category,
                organizedRelativePath,
                savedRelativePath,
                sourcePath,
                generatedContent));
        }

        // A config can name any file the user can read, and the preset may be shared later. Only
        // the files of the config folder are copied. A file outside keeps its path in the preset,
        // and the REFERENCED_FILE_OUTSIDE_SOURCE warning shows it in the review.
        foreach (string referencedFile in result.ReferencedFiles)
        {
            string fullPath = Path.GetFullPath(referencedFile);
            if (IsInsideDirectory(sourceDirectory, fullPath))
                AddResource(fullPath, fullPath, null);
        }

        foreach (ConfigImportGeneratedFile generatedFile in result.GeneratedFiles)
        {
            AddResource(
                Path.Combine(sourceDirectory, generatedFile.RelativePath),
                null,
                generatedFile.Content,
                generatedFile.RelativePath);
        }

        foreach (string missingFile in result.MissingReferencedFiles)
        {
            string fullMissingPath = Path.GetFullPath(missingFile);
            string? replacementPath = resolutions.TryGetValue(fullMissingPath, out ConfigImportMissingFileResolution? resolution)
                ? resolution.ReplacementPath
                : null;
            AddResource(fullMissingPath, replacementPath, null);
        }

        return resources;
    }

    private static string GetSourceRelativePath(string sourceDirectory, string path)
    {
        try
        {
            return GetSafeRelativePath(sourceDirectory, path);
        }
        catch (InvalidOperationException)
        {
            return Path.GetFileName(path);
        }
    }

    private static string GetResourceCategory(string path)
    {
        string extension = Path.GetExtension(path);
        if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
            return SharedConstants.LocalUserItemSiteListsFolder;
        if (extension.Equals(".lua", StringComparison.OrdinalIgnoreCase))
            return LuaDirectoryName;
        return SharedConstants.LocalUserItemBinsFolder;
    }

    private static string GetOrganizedRelativePath(string relativePath, string category)
    {
        string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        string[] parts = normalized.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return "empty.bin";

        bool stripFirst = category.Equals(SharedConstants.LocalUserItemSiteListsFolder, StringComparison.OrdinalIgnoreCase)
            ? parts[0].Equals("list", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("lists", StringComparison.OrdinalIgnoreCase)
            : category.Equals(LuaDirectoryName, StringComparison.OrdinalIgnoreCase)
                ? parts[0].Equals("lua", StringComparison.OrdinalIgnoreCase)
                : parts[0].Equals("bin", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("fake", StringComparison.OrdinalIgnoreCase);
        string result = string.Join(Path.DirectorySeparatorChar, stripFirst ? parts.Skip(1) : parts);
        return string.IsNullOrWhiteSpace(result) ? Path.GetFileName(normalized) : result;
    }

    private static string AddPathHash(string relativePath, string expectedPath)
    {
        string directory = Path.GetDirectoryName(relativePath) ?? string.Empty;
        string fileName = Path.GetFileNameWithoutExtension(relativePath);
        string extension = Path.GetExtension(relativePath);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(expectedPath)))[..8];
        return Path.Combine(directory, $"{fileName}_{hash}{extension}");
    }

    private static string GetSafeRelativePath(string sourceRoot, string sourcePath)
    {
        if (!IsInsideDirectory(sourceRoot, sourcePath))
            throw new InvalidOperationException($"Referenced file is outside the imported Config directory: {sourcePath}");
        return Path.GetRelativePath(sourceRoot, sourcePath);
    }

    private static bool IsInsideDirectory(string directory, string path)
    {
        string relativePath = Path.GetRelativePath(directory, path);
        return !(Path.IsPathRooted(relativePath) ||
            relativePath.Equals("..", StringComparison.Ordinal) ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal));
    }
}
