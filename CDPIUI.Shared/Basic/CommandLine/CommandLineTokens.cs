using System.Collections.Generic;
using System.Text;

namespace CDPIUI.Shared.Basic.CommandLine;

/// <summary>
/// Splits a component command line into arguments as the Windows command line does.
/// Lexical analysis only; does not invoke a shell or execute parameters.
/// </summary>
public static class CommandLineTokens
{
    public static IReadOnlyList<string> Tokenize(string? commandLine)
    {
        List<string> tokens = new List<string>();
        StringBuilder current = new StringBuilder();
        char quote = '\0';
        int backslashes = 0;
        string text = commandLine ?? string.Empty;
        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            // A quoted folder path ends with a backslash ("C:\dir\"): before whitespace or the
            // end of the line that quote closes the argument instead of being escaped.
            bool closesAfterBackslash = quote == '"' &&
                (index + 1 == text.Length || char.IsWhiteSpace(text[index + 1]));
            // Only double quotes group on the Windows command line; an apostrophe inside a value
            // (a Lua argument, a file name) must not join the following arguments.
            if (character == '"' &&
                (quote == '\0' || quote == character) && (backslashes % 2 == 0 || closesAfterBackslash))
            {
                quote = quote == character ? '\0' : character;
            }
            if (char.IsWhiteSpace(character) && quote == '\0')
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(character);
            }
            backslashes = character == '\\' ? backslashes + 1 : 0;
        }
        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }
        return tokens;
    }

    public static bool TryGetOptionName(string token, out string name, out int equalsIndex)
    {
        equalsIndex = token.IndexOf('=');
        name = equalsIndex >= 0 ? token.Substring(0, equalsIndex) : token;
        return IsOption(name);
    }

    // A negative number such as -10000 is a value, not an option.
    public static bool IsOption(string token) => token.Length > 1 && token[0] == '-' && !char.IsDigit(token[1]);

    public static string Unquote(string? value)
    {
        string result = (value ?? string.Empty).Trim();
        if (result.Length >= 2 && ((result[0] == '"' && result[result.Length - 1] == '"') ||
            (result[0] == '\'' && result[result.Length - 1] == '\'')))
        {
            result = result.Substring(1, result.Length - 2);
        }
        return result.Replace("\\\"", "\"");
    }
}
