namespace CDPIUI.AddOns.ConfigImport.Zapret;

/// <summary>
/// A variable a called script sets to one of two values; the Config exposes it as a switch.
/// </summary>
internal sealed record ZapretConditionalVariable(
    string Name,
    string ParameterName,
    bool IsEnabled,
    string EnabledValue,
    string DisabledValue);
