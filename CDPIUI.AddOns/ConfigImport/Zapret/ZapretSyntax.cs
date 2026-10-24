using System.Text;
using System.Text.RegularExpressions;

namespace CDPIUI.AddOns.ConfigImport.Zapret;

/// <summary>
/// The batch and winws command-line syntax the zapret importer recognizes without running anything.
/// </summary>
internal static partial class ZapretSyntax
{
    public static ZapretConfigGeneration? DetectGeneration(string startupString, string? builtinVersion)
    {
        if (!string.IsNullOrWhiteSpace(builtinVersion) || Winws2OptionRegex().IsMatch(startupString))
            return ZapretConfigGeneration.Winws2;

        if (Winws1OptionRegex().IsMatch(startupString))
            return ZapretConfigGeneration.Winws1;

        return null;
    }

    public static string GetExecutableName(ZapretConfigGeneration generation) =>
        generation == ZapretConfigGeneration.Winws2 ? "winws2" : "winws1";

    /// <summary>
    /// Cuts the command at the first unescaped redirection, pipe or command separator and removes
    /// the caret escapes outside quotes, as cmd does ("^&" becomes "&", "^^" becomes "^").
    /// </summary>
    public static string TrimShellTail(string input)
    {
        var result = new StringBuilder(input.Length);
        bool quoted = false;
        for (int index = 0; index < input.Length; index++)
        {
            char current = input[index];
            if (current == '"')
                quoted = !quoted;

            if (!quoted)
            {
                if (current == '^' && index + 1 < input.Length)
                {
                    result.Append(input[++index]);
                    continue;
                }
                if (current is '>' or '<' or '|' or '&')
                    break;
            }

            result.Append(current);
        }

        return result.ToString();
    }

    public static bool TryParseSet(string line, out string name, out string value)
    {
        Match match = SetRegex().Match(line);
        if (!match.Success)
        {
            name = string.Empty;
            value = string.Empty;
            return false;
        }

        name = match.Groups["name"].Value.Trim();
        value = match.Groups["value"].Value;
        if (match.Groups["quoted"].Success && value.EndsWith('"'))
            value = value[..^1];
        return name.Length > 0;
    }

    public static bool TryParseCommentedSet(string line, out string name, out string value)
    {
        Match match = CommentedSetRegex().Match(line);
        if (match.Success)
            return TryParseSet(match.Groups["set"].Value, out name, out value);

        name = string.Empty;
        value = string.Empty;
        return false;
    }

    public static bool IsComment(string line)
    {
        // "@rem" and "rem<TAB>" are comments too.
        string command = line.TrimStart('@');
        return (command.StartsWith("rem", StringComparison.OrdinalIgnoreCase) &&
                (command.Length == 3 || char.IsWhiteSpace(command[3]))) ||
               line.StartsWith("::", StringComparison.Ordinal);
    }

    public static IReadOnlyList<string> SplitBatchArguments(string input)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool quoted = false;

        foreach (char character in input)
        {
            if (character == '"')
            {
                quoted = !quoted;
                continue;
            }

            if (!quoted && char.IsWhiteSpace(character))
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }

            current.Append(character);
        }

        if (current.Length > 0)
            result.Add(current.ToString());
        return result;
    }

    public static string NormalizeWhitespace(string input)
    {
        var result = new StringBuilder(input.Length);
        bool quoted = false;
        bool whitespacePending = false;

        foreach (char current in input.Trim())
        {
            if (current == '"')
            {
                if (whitespacePending && result.Length > 0)
                    result.Append(' ');
                whitespacePending = false;
                quoted = !quoted;
                result.Append(current);
                continue;
            }

            if (!quoted && char.IsWhiteSpace(current))
            {
                whitespacePending = true;
                continue;
            }

            if (whitespacePending && result.Length > 0)
                result.Append(' ');
            whitespacePending = false;
            result.Append(current);
        }

        return result.ToString();
    }

    [GeneratedRegex(
        """(?ix)(?:^|\s)(?<token>[@$](?:"(?<quoted>[^"]+)"|(?<plain>[^\s"]+)))""")]
    public static partial Regex StandaloneAtFileRegex();

    [GeneratedRegex(
        @"(?im)^\s*#\s*(?:built(?:-|\s*)in\s*version|builtinversion)\s*[:=]\s*(?<value>[^\s#;]+)")]
    public static partial Regex BuiltinVersionRegex();

    [GeneratedRegex(@"(?im)^\s*#\s*(?:preset|activepreset)\s*[:=]\s*(?<value>.+?)\s*$")]
    public static partial Regex ConfigNameRegex();

    [GeneratedRegex(
        @"(?ix)(?:^|\s)(?:--lua-init(?:=|\s)|--lua-desync(?:=|\s)|--blob(?:=|\s)|--wf-(?:tcp|udp)-(?:out|in)(?:=|\s)|--out-range(?:=|\s)|--ctrack-[a-z0-9-]+(?:=|\s))")]
    private static partial Regex Winws2OptionRegex();

    [GeneratedRegex(
        @"(?ix)(?:^|\s)(?:--dpi-desync(?:=|\s)|--wf-(?:tcp|udp)(?:=|\s))")]
    private static partial Regex Winws1OptionRegex();

    [GeneratedRegex(
        // A left boundary: "mywinws.exe" is not a winws launch.
        """(?ix)(?<![\w.-])(?<exe>winws2?)(?:\.exe)?(?=["\s]|$)""")]
    public static partial Regex WinwsExecutableRegex();

    [GeneratedRegex("""(?i)^\s*@?set\s+(?<quoted>")?(?<name>[^="]+)=(?<value>.*)$""")]
    private static partial Regex SetRegex();

    [GeneratedRegex(@"(?i)^\s*(?:rem\s+|::\s*)(?<set>set\s+.+)$")]
    private static partial Regex CommentedSetRegex();

    [GeneratedRegex(@"%(?<name>[A-Za-z0-9_~]+)%", RegexOptions.IgnoreCase)]
    public static partial Regex BatchVariableRegex();

    [GeneratedRegex(@"%~(?<modifiers>[fdpnxs]+)0", RegexOptions.IgnoreCase)]
    public static partial Regex BatchFileModifierRegex();

    [GeneratedRegex(@"!(?<name>[A-Za-z0-9_]+)!", RegexOptions.IgnoreCase)]
    public static partial Regex DelayedVariableRegex();

    [GeneratedRegex(@"%(?<root>[A-Za-z0-9_]+)%(?<file>ACTIVE_[^\s\""'\\/]+\.bin)", RegexOptions.IgnoreCase)]
    public static partial Regex ActiveBinaryReferenceRegex();

    [GeneratedRegex(
        """(?ix)^\s*(?:@?call)\s+(?:"(?<quoted>[^"]+\.(?:bat|cmd))"|(?<plain>[^\s"]+\.(?:bat|cmd)))""")]
    public static partial Regex CallScriptRegex();

    [GeneratedRegex(
        """(?ix)^\s*if(?:\s+/i)?\s+"?%~?1"?\s*==\s*"(?<argument>[^"]*)"\s*\(""")]
    public static partial Regex ExternalDispatchRegex();

    [GeneratedRegex(@"(?i)^\s*(?:@?call)\s+:(?<label>[A-Za-z0-9_.-]+)")]
    public static partial Regex LocalLabelCallRegex();

    [GeneratedRegex(@"^\s*:(?!:)(?<label>[A-Za-z0-9_.-]+)\s*$", RegexOptions.IgnoreCase)]
    public static partial Regex BatchLabelRegex();

    [GeneratedRegex(
        """(?ix)^\s*@?echo(?:\s+|\.)?(?<content>.*?)\s*(?<operator>>>|>)\s*"?(?<path>[^">]+)"?\s*$""")]
    public static partial Regex EchoFileWriteRegex();

    [GeneratedRegex(
        """(?ix)(?:["'](?<quotedPath>[^"']+\.(?:bin|txt|lua))["']|(?<plainPath>(?:[A-Za-z]:[\\/][^\s"'=]+|[^\s"'=:]+)\.(?:bin|txt|lua)))""")]
    public static partial Regex ResourcePathRegex();
}
