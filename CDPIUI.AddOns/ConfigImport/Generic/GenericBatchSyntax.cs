using System.Text;
using System.Text.RegularExpressions;

namespace CDPIUI.AddOns.ConfigImport.Generic;

/// <summary>
/// The batch syntax the generic command-file importer recognizes without running anything.
/// </summary>
internal static partial class GenericBatchSyntax
{
    public static bool TryParseSet(string line, out string name, out string value)
    {
        Match match = SetRegex().Match(line);
        name = match.Success ? match.Groups["name"].Value.Trim() : string.Empty;
        value = match.Success ? match.Groups["value"].Value.Trim() : string.Empty;
        if (match.Success && match.Groups["quoted"].Success)
        {
            int closingQuote = value.LastIndexOf('"');
            if (closingQuote >= 0)
                value = value[..closingQuote].TrimEnd();
        }
        return match.Success && name.Length > 0;
    }

    public static bool TryParseCommentedSet(string line, out string name, out string value)
    {
        Match match = CommentedSetRegex().Match(line);
        return TryParseSet(match.Success ? match.Groups["set"].Value : string.Empty, out name, out value);
    }

    public static bool IsComment(string line)
    {
        // "@rem" and "rem<TAB>" are comments too, as in the Zapret importer.
        string command = line.TrimStart('@');
        return line.StartsWith("::", StringComparison.Ordinal) ||
               (command.StartsWith("rem", StringComparison.OrdinalIgnoreCase) &&
                (command.Length == 3 || char.IsWhiteSpace(command[3]))) ||
               line.StartsWith('#');
    }

    public static string TrimShellTail(string input)
    {
        bool quoted = false;
        for (int index = 0; index < input.Length; index++)
        {
            if (input[index] == '"')
                quoted = !quoted;
            if (!quoted && input[index] is '>' or '<' or '|')
                return input[..index];
            if (!quoted && input[index] == '&')
                return input[..index];
        }
        return input;
    }

    public static List<GenericCommandToken> Tokenize(string input)
    {
        var result = new List<GenericCommandToken>();
        int index = 0;
        while (index < input.Length)
        {
            while (index < input.Length && char.IsWhiteSpace(input[index]))
                index++;
            if (index >= input.Length)
                break;

            int start = index;
            var value = new StringBuilder();
            bool quoted = false;
            while (index < input.Length)
            {
                char current = input[index];
                if (current == '"')
                {
                    quoted = !quoted;
                    index++;
                    continue;
                }
                if (!quoted && char.IsWhiteSpace(current))
                    break;
                value.Append(current);
                index++;
            }
            result.Add(new GenericCommandToken(value.ToString(), start, index));
        }
        return result;
    }

    public static string NormalizeWhitespace(string input) =>
        WhitespaceRegex().Replace(input, " ").Trim();

    [GeneratedRegex("""(?i)^\s*@?set\s+(?<quoted>")?(?<name>[^="]+)=(?<value>.*)$""")]
    private static partial Regex SetRegex();

    [GeneratedRegex(@"(?i)^\s*(?:rem\s+|::\s*)(?<set>set\s+.+)$")]
    private static partial Regex CommentedSetRegex();

    [GeneratedRegex(@"%(?<name>[A-Za-z0-9_~]+)%", RegexOptions.IgnoreCase)]
    public static partial Regex BatchVariableRegex();

    [GeneratedRegex(@"(?im)^\s*#\s*(?:config|preset|activepreset)\s*[:=]\s*(?<value>.+?)\s*$")]
    public static partial Regex ConfigNameRegex();

    [GeneratedRegex(
        """(?ix)(?:["'](?<quotedPath>[^"']+\.(?:bin|txt|lua|dat|conf|cfg|json))['"]|(?<plainPath>[^\s"'=]+\.(?:bin|txt|lua|dat|conf|cfg|json)))""")]
    public static partial Regex ResourcePathRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
