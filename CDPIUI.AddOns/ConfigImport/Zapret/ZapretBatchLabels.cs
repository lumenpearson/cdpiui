using System.Text.RegularExpressions;

namespace CDPIUI.AddOns.ConfigImport.Zapret;

/// <summary>
/// Finds batch labels, their line ranges and the labels a script dispatches to for an argument.
/// </summary>
internal static class ZapretBatchLabels
{
    public static IReadOnlyList<string> FindDispatchedLabels(
        IReadOnlyList<BatchLogicalLine> lines,
        string? requestedArgument)
    {
        if (string.IsNullOrWhiteSpace(requestedArgument))
            return [];

        for (int index = 0; index < lines.Count; index++)
        {
            string line = lines[index].Text.Trim();
            Match dispatch = ZapretSyntax.ExternalDispatchRegex().Match(line);
            if (!dispatch.Success ||
                !dispatch.Groups["argument"].Value.Equals(
                    requestedArgument,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var labels = new List<string>();
            int depth = CountParentheses(line);
            for (index++; index < lines.Count && depth > 0; index++)
            {
                string blockLine = lines[index].Text.Trim();
                Match localCall = ZapretSyntax.LocalLabelCallRegex().Match(blockLine);
                if (localCall.Success)
                    labels.Add(localCall.Groups["label"].Value);
                depth += CountParentheses(blockLine);
            }

            return labels;
        }

        return [];
    }

    public static bool TryGetLabelRange(
        IReadOnlyList<BatchLogicalLine> lines,
        string label,
        out int start,
        out int end)
    {
        for (int index = 0; index < lines.Count; index++)
        {
            Match match = ZapretSyntax.BatchLabelRegex().Match(lines[index].Text.Trim());
            if (!match.Success ||
                !match.Groups["label"].Value.Equals(label, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            start = index + 1;
            end = lines.Count;
            for (int next = start; next < lines.Count; next++)
            {
                if (ZapretSyntax.BatchLabelRegex().IsMatch(lines[next].Text.Trim()))
                {
                    end = next;
                    break;
                }
            }
            return true;
        }

        start = 0;
        end = 0;
        return false;
    }

    private static int CountParentheses(string line)
    {
        bool quoted = false;
        int result = 0;
        foreach (char current in line)
        {
            if (current == '"')
            {
                quoted = !quoted;
                continue;
            }

            if (quoted)
                continue;
            if (current == '(')
                result++;
            else if (current == ')')
                result--;
        }

        return result;
    }
}
