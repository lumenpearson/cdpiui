using CDPIUI.Core.ComponentServices.Helpers.Configuration;

namespace CDPIUI.AddOns.ConfigImport.Generic;

/// <summary>
/// Collects the issues and the files of one generic command-file import and builds its result.
/// </summary>
internal sealed class GenericImportContext(ConfigImportTarget target, string sourcePath)
{
    private readonly List<ConfigImportIssue> _issues = [];
    private readonly List<string> _sourceFiles = [];
    private readonly HashSet<string> _sourceFileSet = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _referencedFiles = [];
    private readonly HashSet<string> _referencedFileSet = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _missingReferencedFiles = [];
    private readonly HashSet<string> _missingReferencedFileSet = new(StringComparer.OrdinalIgnoreCase);

    public ConfigImportTarget Target { get; } = target;

    public void AddError(string code, string message, string? path, int? line = null) =>
        _issues.Add(new(ConfigImportIssueSeverity.Error, code, message, path, line));

    public void AddWarning(string code, string message, string? path, int? line = null) =>
        _issues.Add(new(ConfigImportIssueSeverity.Warning, code, message, path, line));

    public void AddSourceFile(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (!_sourceFileSet.Add(fullPath))
            return;
        _sourceFiles.Add(fullPath);
        if (IsOutsideRoot(_root, fullPath))
            AddWarning(
                "REFERENCED_FILE_OUTSIDE_SOURCE",
                $"The config uses a file outside its folder: {fullPath}",
                sourcePath);
    }

    private readonly string? _root = Path.GetDirectoryName(Path.GetFullPath(sourcePath));

    public void AddReferencedFile(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (!_referencedFileSet.Add(fullPath))
            return;
        _referencedFiles.Add(fullPath);
        if (IsOutsideRoot(_root, fullPath))
            AddWarning(
                "REFERENCED_FILE_OUTSIDE_SOURCE",
                $"The config uses a file outside its folder: {fullPath}",
                sourcePath);
    }

    // A config may name any file on the disk, and the import copies it into the preset, which the
    // user can later share. A file outside the folder of the imported config is reported, so the
    // user sees it in the review before saving.
    private static bool IsOutsideRoot(string? root, string fullPath)
    {
        if (string.IsNullOrEmpty(root))
            return false;
        string relativePath = Path.GetRelativePath(root, fullPath);
        return Path.IsPathRooted(relativePath) ||
            relativePath.Equals("..", StringComparison.Ordinal) ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }

    public void AddMissingReferencedFile(string path, string pathSource)
    {
        string fullPath = Path.GetFullPath(path);
        if (!_missingReferencedFileSet.Add(fullPath))
            return;
        _missingReferencedFiles.Add(fullPath);
        AddWarning(
            "REFERENCED_FILE_NOT_FOUND",
            $"A referenced file was not found: {fullPath}",
            pathSource);
    }

    public ConfigImportResult CreateResult(ConfigItem? config = null) => new()
    {
        Config = config,
        Target = Target,
        SourcePath = sourcePath,
        Issues = _issues.AsReadOnly(),
        SourceFiles = _sourceFiles.AsReadOnly(),
        ReferencedFiles = _referencedFiles.AsReadOnly(),
        MissingReferencedFiles = _missingReferencedFiles.AsReadOnly(),
        MissingFileResolutions = [],
        GeneratedFiles = [],
    };
}
