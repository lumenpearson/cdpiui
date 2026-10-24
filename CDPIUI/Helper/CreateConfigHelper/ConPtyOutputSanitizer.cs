using System;
using System.Text;
using System.Text.RegularExpressions;

namespace CDPIUI.Helper.CreateConfigHelper;

internal static partial class ConPtyOutputSanitizer
{
    [GeneratedRegex(@"\x1B\][^\x07]*(?:\x07|\x1B\\)")]
    private static partial Regex OscSequenceRegex();

    [GeneratedRegex(@"\x1B\[[0-?]*[ -/]*[@-~]")]
    private static partial Regex CsiSequenceRegex();

    [GeneratedRegex(@"\x1B\[[0-?]*[ -/]*E")]
    private static partial Regex CsiNextLineSequenceRegex();

    [GeneratedRegex(@"\x1B[DE]")]
    private static partial Regex EscLineSequenceRegex();

    public static string Sanitize(string output)
    {
        string sanitized = OscSequenceRegex().Replace(output ?? string.Empty, string.Empty);
        sanitized = CsiNextLineSequenceRegex().Replace(sanitized, "\n");
        sanitized = EscLineSequenceRegex().Replace(sanitized, "\n");
        sanitized = CsiSequenceRegex().Replace(sanitized, string.Empty);
        sanitized = sanitized.Replace("\0", string.Empty, StringComparison.Ordinal);
        return RemoveInvisibleCharacters(NormalizeLineSeparators(sanitized));
    }

    public static string NormalizeLineSeparators(string text) =>
        (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace('\v', '\n')
            .Replace('\f', '\n')
            .Replace('\u0085', '\n')
            .Replace('\u2028', '\n')
            .Replace('\u2029', '\n');

    private static string RemoveInvisibleCharacters(string text)
    {
        StringBuilder result = new(text.Length);
        foreach (char character in text)
        {
            if (character is '\n' or '\t')
            {
                result.Append(character);
                continue;
            }

            System.Globalization.UnicodeCategory category = char.GetUnicodeCategory(character);
            if (category is System.Globalization.UnicodeCategory.Control or
                System.Globalization.UnicodeCategory.Format)
            {
                continue;
            }

            result.Append(character);
        }

        return result.ToString();
    }
}
