using CDPIUI.Core.ComponentServices.Helpers.Configuration;

namespace CDPIUI.AddOns.ConfigImport.Zapret;

/// <summary>
/// Collects the issues and the files of one zapret import and builds its result.
/// </summary>
internal sealed class ZapretImportContext(ZapretConfigImportOptions options)
{
    private readonly List<ZapretConfigImportIssue> _issues = [];
    private readonly List<string> _sourceFiles = [];
    private readonly HashSet<string> _sourceFileSet = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _referencedFiles = [];
    private readonly HashSet<string> _referencedFileSet = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ZapretConfigImportGeneratedFile> _generatedFiles = [];
    private readonly HashSet<string> _generatedFilePaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _missingReferencedFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _activeIncludes = new(StringComparer.OrdinalIgnoreCase);

    public ZapretConfigImportOptions Options { get; } = options;

    public void AddError(string code, string message, string? path, int? line = null) =>
        _issues.Add(new ZapretConfigImportIssue(
            ZapretConfigImportIssueSeverity.Error,
            code,
            message,
            path,
            line));

    public void AddWarning(string code, string message, string? path, int? line = null) =>
        _issues.Add(new ZapretConfigImportIssue(
            ZapretConfigImportIssueSeverity.Warning,
            code,
            message,
            path,
            line));

    // The folder of the first source file, which is the imported config.
    private string? _root;

    public void AddSourceFile(string path)
    {
        string fullPath = Path.GetFullPath(path);
        _root ??= Path.GetDirectoryName(fullPath);
        if (!_sourceFileSet.Add(fullPath))
            return;
        _sourceFiles.Add(fullPath);
        if (IsOutsideRoot(_root, fullPath))
            AddWarning(
                "INCLUDED_FILE_OUTSIDE_SOURCE",
                $"The config reads a file outside its folder: {fullPath}",
                _sourceFiles[0]);
    }

    public bool EnterInclude(string path) => _activeIncludes.Add(path);

    public void LeaveInclude(string path) => _activeIncludes.Remove(path);

    public void AddReferencedFile(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (!_referencedFileSet.Add(fullPath))
            return;
        _referencedFiles.Add(fullPath);
        if (IsOutsideRoot(_root, fullPath))
            AddWarning(
                "REFERENCED_FILE_OUTSIDE_SOURCE",
                $"The config refers to a file outside its folder. The import does not copy this file: {fullPath}",
                _sourceFiles.Count > 0 ? _sourceFiles[0] : null);
    }

    // A config may name any file on the disk. The installer does not copy a file outside the
    // folder of the imported config into the preset, which the user can later share; the
    // reference stays, and the warning shows it in the review, so the user can change it.
    private static bool IsOutsideRoot(string? root, string fullPath)
    {
        if (string.IsNullOrEmpty(root))
            return false;
        string relativePath = Path.GetRelativePath(root, fullPath);
        return Path.IsPathRooted(relativePath) ||
            relativePath.Equals("..", StringComparison.Ordinal) ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }

    public void AddGeneratedFile(string sourceRoot, string path, string content)
    {
        string fullRoot = Path.GetFullPath(sourceRoot);
        string fullPath = Path.GetFullPath(path);
        string relativePath = Path.GetRelativePath(fullRoot, fullPath);
        if (Path.IsPathRooted(relativePath) ||
            relativePath.Equals("..", StringComparison.Ordinal) ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            AddWarning(
                "GENERATED_FILE_OUTSIDE_SOURCE",
                "A batch-generated file outside the source config directory was ignored.",
                fullPath);
            return;
        }

        if (_generatedFilePaths.Add(fullPath))
            _generatedFiles.Add(new ZapretConfigImportGeneratedFile(relativePath, content));
    }

    public bool IsGeneratedFile(string path) =>
        _generatedFilePaths.Contains(Path.GetFullPath(path));

    public void AddMissingReferencedFile(string path, string sourcePath)
    {
        string fullPath = Path.GetFullPath(path);
        if (_missingReferencedFiles.Add(fullPath))
        {
            AddWarning(
                "REFERENCED_FILE_NOT_FOUND",
                $"A referenced BIN/TXT/LUA file was not found: {fullPath}",
                sourcePath);
        }
    }

    public ZapretConfigImportResult CreateResult(
        ConfigItem? config = null,
        ZapretConfigGeneration? generation = null,
        IEnumerable<ZapretBatchVariable>? variables = null) =>
        new()
        {
            Config = config,
            Generation = generation,
            Issues = _issues.AsReadOnly(),
            SourceFiles = _sourceFiles.AsReadOnly(),
            ReferencedFiles = _referencedFiles.AsReadOnly(),
            MissingReferencedFiles = _missingReferencedFiles.ToList().AsReadOnly(),
            GeneratedFiles = _generatedFiles.AsReadOnly(),
            Variables = (variables ?? [])
                .Select(variable => new ZapretConfigImportVariable(
                    variable.Name,
                    variable.Value,
                    variable.Alternatives.AsReadOnly()))
                .ToList()
                .AsReadOnly(),
        };
}
