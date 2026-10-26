namespace CDPIUI.ViewModels.ConfigMaker;

/// <summary>The variable confirmed in the variable dialog and the name it had before the edit.</summary>
public sealed record ConfigMakerVariableEditResult(ConfigMakerVariableDefinition Variable, string OriginalName);
