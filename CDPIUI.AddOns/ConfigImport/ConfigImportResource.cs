namespace CDPIUI.AddOns.ConfigImport;

/// <summary>
/// A file an imported Config needs: where the Config expects it, where the import saves it and
/// where its content comes from (a source file, generated text, or nothing for an empty file).
/// </summary>
internal sealed record ConfigImportResource(
    string ExpectedPath,
    string SourceRelativePath,
    string Category,
    string RelativePath,
    string SavedRelativePath,
    string? SourcePath,
    string? GeneratedContent);
