namespace CDPIUI.ViewModels.ConfigImport;

/// <summary>How many of the imported presets were converted; <see cref="None"/> before the first import.</summary>
public enum ConfigImportCompletionState
{
    None,
    Success,
    Partial,
    Failed
}
