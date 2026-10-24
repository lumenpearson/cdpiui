namespace CDPIUI.AddOns.ConfigImport.Generic;

/// <summary>
/// A command-line token without its quotes, with its position in the source line.
/// </summary>
internal sealed record GenericCommandToken(string Value, int Start, int End);
