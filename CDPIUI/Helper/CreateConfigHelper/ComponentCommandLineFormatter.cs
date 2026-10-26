using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;

namespace CDPIUI.Helper.CreateConfigHelper;

public static partial class ComponentCommandLineFormatter
{
    [GeneratedRegex(@"^-?\d+(?:[.,]\d+)?$")]
    private static partial Regex NumericTokenRegex();

    public static string FormatByFlags(string commandLine)
    {
        List<string> lines = [];
        StringBuilder current = new();

        foreach (string token in Tokenize(commandLine))
        {
            if (IsFlag(token))
            {
                FlushLine(lines, current);
                current.Append(token);
            }
            else
            {
                if (current.Length > 0)
                {
                    current.Append(' ');
                }
                current.Append(token);
            }
        }

        FlushLine(lines, current);
        return string.Join(Environment.NewLine, lines);
    }

    public static string ToSingleLine(string commandLine) =>
        ConfigCommandLine.ToSingleLine(commandLine);

    public static IReadOnlyList<string> ExtractFlags(string commandLine) => Tokenize(commandLine)
        .Where(IsFlag)
        .Select(token => token.Split('=', 2)[0])
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    public static IReadOnlyList<string> Tokenize(string commandLine) =>
        ConfigCommandLine.Tokenize(commandLine);

    private static bool IsFlag(string token) =>
        !NumericTokenRegex().IsMatch(token) &&
        (token.StartsWith("--", StringComparison.Ordinal) ||
         (token.StartsWith("-", StringComparison.Ordinal) && token.Length > 1) ||
         (token.StartsWith("/", StringComparison.Ordinal) && token.Length > 1));

    private static void FlushLine(List<string> lines, StringBuilder current)
    {
        if (current.Length == 0)
        {
            return;
        }
        lines.Add(current.ToString());
        current.Clear();
    }
}
