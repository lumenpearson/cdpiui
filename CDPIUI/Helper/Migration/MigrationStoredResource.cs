namespace CDPIUI.Helper.Migration;

/// <param name="Path">The full path of the imported resource file.</param>
/// <param name="Reference">The reference that the imported presets use for the file.</param>
internal sealed record MigrationStoredResource(string Path, string Reference);
