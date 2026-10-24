using System.Text;

namespace CDPIUI.Core.ComponentServices.Helpers.Configuration.Converters;

/// <summary>
/// Splits an expanded Zapret1 command line into options.
/// </summary>
internal static class Zapret1CommandLineParser
{
    public static List<Zapret1CommandOption> Parse(string commandLine, ZapretConversionContext context)
    {
        List<string> tokens = Tokenize(commandLine, out bool unclosedQuote);
        if (unclosedQuote)
        {
            context.AddError("UNCLOSED_QUOTE", "The Zapret1 startup string contains an unclosed quote.");
        }

        List<Zapret1CommandOption> result = [];
        for (int i = 0; i < tokens.Count; i++)
        {
            string token = tokens[i];
            if (token.StartsWith('@'))
            {
                context.AddError(
                    "CONFIG_FILE_INPUT",
                    "An @config_file command line must be expanded before conversion.");
                continue;
            }

            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                context.AddError(
                    "POSITIONAL_ARGUMENT",
                    $"Unexpected positional argument '{token}'. The converter accepts a ready command line only.");
                continue;
            }

            string body = token[2..];
            int equalsIndex = body.IndexOf('=');
            string name = equalsIndex >= 0 ? body[..equalsIndex] : body;
            string? value = equalsIndex >= 0 ? body[(equalsIndex + 1)..] : null;

            if (equalsIndex < 0 && i + 1 < tokens.Count &&
                !tokens[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = tokens[++i];
            }

            result.Add(new Zapret1CommandOption(name.ToLowerInvariant(), value));
        }

        return result;
    }

    private static List<string> Tokenize(string commandLine, out bool unclosedQuote)
    {
        List<string> result = [];
        StringBuilder current = new();
        bool quoted = false;

        for (int i = 0; i < commandLine.Length; i++)
        {
            char character = commandLine[i];
            if (character == '"')
            {
                quoted = !quoted;
                continue;
            }

            if (char.IsWhiteSpace(character) && !quoted)
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
        {
            result.Add(current.ToString());
        }

        unclosedQuote = quoted;
        return result;
    }
}
