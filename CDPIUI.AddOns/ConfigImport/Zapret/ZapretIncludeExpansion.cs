namespace CDPIUI.AddOns.ConfigImport.Zapret;

/// <summary>
/// The command line after a response-file include was replaced with the included config.
/// </summary>
internal sealed record ZapretIncludeExpansion(string Text, string? Name, string? BuiltinVersion);
