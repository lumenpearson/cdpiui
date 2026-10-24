namespace CDPIUI.ViewModels.ConfigMaker;

/// <summary>The answer of the save preset dialog.</summary>
public sealed record ConfigMakerSavePresetRequest(string PresetName, bool Overwrite);
