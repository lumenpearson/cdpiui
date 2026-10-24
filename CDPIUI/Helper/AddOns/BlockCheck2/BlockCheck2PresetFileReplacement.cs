using CDPIUI.ViewModels;

namespace CDPIUI.Helper.AddOns.BlockCheck2;

/// <summary>
/// A file reference of a BlockCheck2 preset and the stored reference that replaces it.
/// </summary>
internal sealed record BlockCheck2PresetFileReplacement(
    BlockCheck2PresetFileKind Kind,
    string OriginalReference,
    string ReplacementReference);
