using System;
using System.Text.RegularExpressions;

namespace CDPIUI.Shared.ComponentsTask;

/// <summary>
/// Turns pseudo-console output into plain text. Every pattern starts with ESC: patterns
/// without it also cut ordinary text, "[HOSTLIST]" became "OSTLIST]".
/// </summary>
public static class OutputCleanupHelper
{
    private const string Esc = "\u001b";

    // Control sequence (CSI), operating system command (OSC) or a two-character sequence.
    private static readonly Regex CompleteSequenceRegex = new(
        @"^\u001b(?:\[[0-?]*[ -/]*[@-~]|\][^\u0007\u001b]*(?:\u0007|\u001b\\)|[@-Z\\-_])",
        RegexOptions.Compiled);

    private static readonly Regex CursorForwardRegex = new(@"\u001b\[(\d{0,3})C", RegexOptions.Compiled);
    private static readonly Regex OscRegex = new(@"\u001b\][^\u0007\u001b]*(?:\u0007|\u001b\\)", RegexOptions.Compiled);
    private static readonly Regex CsiRegex = new(@"\u001b\[[0-?]*[ -/]*[@-~]", RegexOptions.Compiled);
    private static readonly Regex OtherEscapeRegex = new(@"\u001b[@-Z\\-_]?", RegexOptions.Compiled);

    public static string ReplaceSymbols(string str)
    {
        if (string.IsNullOrEmpty(str) || str.IndexOf('\u001b') < 0)
            return str;

        str = str.Replace(Esc + "[?25l" + Esc + "[2J" + Esc + "[m" + Esc + "[H", "");
        // Rows of the winws status screen start a new line.
        str = str.Replace(Esc + "[4;1H", "\n");
        str = str.Replace(Esc + "[12;1H", "\n");
        str = str.Replace(Esc + "[23;1H", "\n\t");
        str = str.Replace(Esc + "[32m", "\n");
        str = str.Replace(Esc + "[90m", "\n");
        str = CursorForwardRegex.Replace(str, match =>
            new string(' ', int.TryParse(match.Groups[1].Value, out int count) ? Math.Clamp(count, 1, 200) : 1));
        str = OscRegex.Replace(str, "");
        str = CsiRegex.Replace(str, "");
        str = OtherEscapeRegex.Replace(str, "");
        return str;
    }

    /// <summary>
    /// Splits off an escape sequence cut at the end of a chunk, so that it is cleaned together
    /// with the rest of it in the next chunk.
    /// </summary>
    public static string TakeIncompleteTail(ref string chunk)
    {
        int index = chunk.LastIndexOf('\u001b');
        if (index < 0 || chunk.Length - index > 64)
            return string.Empty;

        string tail = chunk.Substring(index);
        if (CompleteSequenceRegex.IsMatch(tail))
            return string.Empty;

        chunk = chunk.Substring(0, index);
        return tail;
    }
}
