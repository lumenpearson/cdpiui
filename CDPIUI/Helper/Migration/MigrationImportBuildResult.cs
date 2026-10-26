using System.Collections.Generic;

namespace CDPIUI.Helper.Migration;

/// <summary>
/// What the staged preset collection contains before it is committed.
/// </summary>
internal sealed record MigrationImportBuildResult(
    int PresetCount,
    int ResourceCount,
    int ReviewRequiredCount,
    IReadOnlyDictionary<string, string> PresetFilesBySource);
