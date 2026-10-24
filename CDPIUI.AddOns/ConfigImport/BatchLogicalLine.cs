namespace CDPIUI.AddOns.ConfigImport;

/// <summary>
/// A command of a batch file after the caret line continuations were joined.
/// </summary>
internal sealed record BatchLogicalLine(int LineNumber, string Text);
