namespace CDPIUI.Controls.MainPage;

/// <summary>
/// How a component tile shows a component state: the status brush resource, the glyph and
/// its size, and the localization key of the tooltip.
/// </summary>
internal sealed record ComponentStateVisual(
    string BackgroundBrushKey,
    string Glyph,
    double FontSize,
    string ToolTipKey)
{
    public static ComponentStateVisual? For(ComponentState componentState) => componentState switch
    {
        ComponentState.SetupRequired => new ComponentStateVisual("SystemFillColorSolidNeutralBrush", "\uE713", 22, "ComponentStatusSetupRequired"),
        ComponentState.Stopped => new ComponentStateVisual("SystemFillColorCriticalBrush", "\uE7E8", 20, "ComponentStatusStopped"),
        ComponentState.Running => new ComponentStateVisual("SystemFillColorSuccessBrush", "\uF13E", 28, "ComponentStatusStarted"),
        ComponentState.ExitedWithException => new ComponentStateVisual("SystemFillColorCautionBrush", "\uE7BA", 18, "ComponentStatusExceptionHappens"),
        _ => null
    };
}
