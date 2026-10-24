namespace CDPIUI.Core.ComponentServices.Helpers.Configuration.Converters;

/// <summary>
/// One option of a Zapret1 command line: the name without the leading dashes, in lower case,
/// and the value, or null for a flag.
/// </summary>
internal sealed record Zapret1CommandOption(string Name, string? Value);
