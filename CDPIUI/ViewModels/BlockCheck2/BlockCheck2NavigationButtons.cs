namespace CDPIUI.ViewModels.BlockCheck2;

/// <summary>Which wizard buttons are visible.</summary>
public readonly record struct BlockCheck2NavigationButtons(
    bool Exit,
    bool Back,
    bool Cancel,
    bool ViewResult,
    bool Next);
