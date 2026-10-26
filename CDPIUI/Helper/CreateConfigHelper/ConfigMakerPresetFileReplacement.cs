namespace CDPIUI.Helper.CreateConfigHelper;

internal sealed record ConfigMakerPresetFileReplacement(
    string Reference,
    string UpdatedCommandText,
    bool CommandChanged);
