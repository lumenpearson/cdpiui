using System;
using System.Linq;

namespace CDPIUI.Helper.CreateConfigHelper;

// Text operations of the Config Maker command editor.
internal static class ConfigMakerCommandText
{
    // Returns the option with its argument, or null when a required argument is empty.
    public static string BuildOptionCommand(ComponentCommandHelpOption option, string argument)
    {
        string command = option.DisplayName;
        if (string.IsNullOrWhiteSpace(option.ArgumentPlaceholder))
        {
            return command;
        }

        argument = argument.Trim();
        if (string.IsNullOrWhiteSpace(argument))
        {
            return option.IsArgumentRequired ? null : command;
        }

        bool usesEquals = option.Syntax.Contains($"{command}=", StringComparison.OrdinalIgnoreCase);
        return usesEquals ? $"{command}={argument}" : $"{command} {argument}";
    }

    // Inserts the command at the cursor on its own line. The result uses "\n" line breaks;
    // newCursorIndex is the index right after the inserted command.
    public static string InsertOnOwnLine(
        string text,
        int cursorLine,
        int cursorColumn,
        string command,
        out int newCursorIndex)
    {
        text = (text ?? string.Empty)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');
        string[] lines = text.Split('\n');
        int lineIndex = Math.Clamp(cursorLine, 0, Math.Max(0, lines.Length - 1));
        int characterIndex = Math.Clamp(cursorColumn, 0, lines[lineIndex].Length);
        int absoluteIndex = lines.Take(lineIndex).Sum(line => line.Length + 1) + characterIndex;

        string before = text[..absoluteIndex];
        string after = text[absoluteIndex..];
        string prefix = before.Length > 0 && !char.IsWhiteSpace(before[^1]) ? "\n" : string.Empty;
        string suffix = after.Length > 0 && !char.IsWhiteSpace(after[0]) ? "\n" : string.Empty;
        newCursorIndex = before.Length + prefix.Length + command.Length;
        return before + prefix + command + suffix + after;
    }

    public static (int Line, int Column) GetLineAndColumn(string normalizedText, int absoluteIndex)
    {
        string before = normalizedText[..Math.Clamp(absoluteIndex, 0, normalizedText.Length)];
        string[] lines = before.Split('\n');
        return (lines.Length - 1, lines[^1].Length);
    }
}
