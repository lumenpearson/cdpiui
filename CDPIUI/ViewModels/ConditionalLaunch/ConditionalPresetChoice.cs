namespace CDPIUI.ViewModels.ConditionalLaunch;

/// <summary>A preset of a component that the Apply preset action can select.</summary>
internal sealed record ConditionalPresetChoice(
    string Name,
    string PackName,
    string PackId,
    string FileName);
