using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CDPIUI.Helper.CreateConfigHelper;

internal static partial class ComponentCommandHelpParser
{
    [GeneratedRegex(@"(?<!\S)(-{1,2}[A-Za-z0-9][A-Za-z0-9_.-]*|/[A-Za-z?][A-Za-z0-9?_.-]*)")]
    private static partial Regex OptionNameRegex();

    [GeneratedRegex(@"-{1,2}[A-Za-z0-9][A-Za-z0-9_.-]*|/[A-Za-z?][A-Za-z0-9?_.-]*")]
    private static partial Regex EmbeddedOptionNameRegex();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex ColumnSeparatorRegex();

    [GeneratedRegex(@"(?<![A-Z0-9])(?<header>[A-Z][A-Z0-9 /()&+_-]{2,}:)(?=\s+(?:-{1,2}[A-Za-z0-9]|/[A-Za-z?]))")]
    private static partial Regex GroupHeaderBeforeOptionRegex();

    public static ComponentCommandHelpDocument Parse(
        string componentId,
        string executablePath,
        string rawText,
        string error = "")
    {
        string usage = string.Empty;
        string currentGroupName = string.Empty;
        List<MutableHelpOption> parsed = [];
        MutableHelpOption current = null;
        string[] lines = GetLogicalHelpLines(rawText);

        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            string originalLine = lines[lineIndex];
            string trimmed = originalLine.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                current = null;
                continue;
            }

            if (string.IsNullOrEmpty(usage) &&
                (trimmed.StartsWith("usage", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.StartsWith("syntax", StringComparison.OrdinalIgnoreCase)))
            {
                usage = trimmed;
            }

            string nextNonEmptyLine = GetNextNonEmptyLine(lines, lineIndex + 1);
            if (TryGetGroupName(
                originalLine,
                trimmed,
                current == null,
                nextNonEmptyLine,
                out string groupName))
            {
                currentGroupName = groupName;
                current = null;
                continue;
            }

            Match firstName = OptionNameRegex().Match(trimmed);
            if (!firstName.Success || firstName.Index > 2)
            {
                if (current != null)
                {
                    current.Description = MergeDescriptionContinuation(
                        current.Description,
                        NormalizeDescription(trimmed),
                        originalLine.Length > 0 && char.IsWhiteSpace(originalLine[0]));
                }
                continue;
            }

            if (TryCreateOption(trimmed, currentGroupName, out MutableHelpOption option))
            {
                current = option;
                parsed.Add(current);
            }
        }

        IReadOnlyList<ComponentCommandHelpOption> options = parsed
            .GroupBy(item => item.Names.First(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First().ToImmutable())
            .ToList();

        return new ComponentCommandHelpDocument
        {
            ComponentId = componentId,
            ExecutablePath = executablePath,
            Usage = usage,
            RawText = rawText,
            Error = error,
            Options = options,
        };
    }

    private static bool TryCreateOption(
        string trimmed,
        string groupName,
        out MutableHelpOption option)
    {
        option = null;
        (string syntax, string description) = SplitOptionLine(trimmed);
        List<string> names = OptionNameRegex().Matches(syntax)
            .Select(match => match.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (names.Count == 0)
        {
            return false;
        }

        (string placeholder, bool required) = FindArgument(syntax, names);
        option = new MutableHelpOption
        {
            Names = names,
            GroupName = groupName,
            Syntax = syntax,
            Description = description,
            ArgumentPlaceholder = placeholder,
            IsArgumentRequired = required,
        };
        return true;
    }

    private static (string Placeholder, bool Required) FindArgument(
        string syntax,
        IReadOnlyList<string> names)
    {
        int lastNameEnd = names
            .Select(name => syntax.LastIndexOf(name, StringComparison.OrdinalIgnoreCase) + name.Length)
            .DefaultIfEmpty(0)
            .Max();
        if (lastNameEnd <= 0 || lastNameEnd >= syntax.Length)
        {
            return (string.Empty, false);
        }

        string remainder = syntax[lastNameEnd..].Trim().TrimStart('=').Trim();
        if (string.IsNullOrWhiteSpace(remainder) || remainder.StartsWith(','))
        {
            return (string.Empty, false);
        }

        string placeholder = remainder.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;
        bool required = !IsEntirelyOptional(placeholder);
        return (placeholder, required);
    }

    private static bool TryGetGroupName(
        string originalLine,
        string trimmed,
        bool isBetweenOptions,
        string nextNonEmptyLine,
        out string groupName)
    {
        groupName = string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed) ||
            OptionNameRegex().IsMatch(trimmed) ||
            trimmed.StartsWith("usage", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("syntax", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int leadingWhitespace = originalLine.Length - originalLine.TrimStart().Length;
        Match nextOption = OptionNameRegex().Match(nextNonEmptyLine ?? string.Empty);
        bool followedByOption = nextOption.Success && nextOption.Index <= 2;
        bool hasHeaderPunctuation = trimmed.EndsWith(':') ||
            (trimmed.StartsWith("==", StringComparison.Ordinal) &&
             trimmed.EndsWith("==", StringComparison.Ordinal));
        bool looksLikePlainHeader = trimmed.Length <= 60 &&
            trimmed.IndexOfAny([';', '=', '<', '>', '|', '@', '$']) < 0;
        if (!hasHeaderPunctuation &&
            !(isBetweenOptions && followedByOption && looksLikePlainHeader))
        {
            return false;
        }
        if (leadingWhitespace > 8 && !followedByOption)
        {
            return false;
        }

        groupName = trimmed
            .TrimEnd(':')
            .Trim('=', '-', '*', '#', ' ');
        return !string.IsNullOrWhiteSpace(groupName) && groupName.Length <= 100;
    }

    private static string GetNextNonEmptyLine(IReadOnlyList<string> lines, int startIndex)
    {
        for (int index = startIndex; index < lines.Count; index++)
        {
            string candidate = lines[index].Trim();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }
        return string.Empty;
    }

    private static string[] GetLogicalHelpLines(string rawText)
    {
        string normalized = ConPtyOutputSanitizer.NormalizeLineSeparators(
            ConPtyOutputSanitizer.Sanitize(rawText));

        // ConPTY can express a new row by moving the cursor instead of writing a
        // conventional CR/LF. If such a transition was already stripped before
        // the text reached us, a section title can remain glued to the preceding
        // description. A title is safe to separate here when an option follows it.
        normalized = GroupHeaderBeforeOptionRegex().Replace(
            normalized,
            match =>
            {
                bool startsAtLineBeginning = match.Index == 0 || normalized[match.Index - 1] == '\n';
                string prefix = startsAtLineBeginning ? string.Empty : "\n";
                return $"{prefix}{match.Groups["header"].Value}\n";
            });

        List<string> logicalLines = [];
        foreach (string line in normalized.Split('\n'))
        {
            if (TrySplitAttachedTrailingOption(line, out string prefix, out string option))
            {
                logicalLines.Add(prefix);
                logicalLines.Add(option);
            }
            else
            {
                logicalLines.Add(line);
            }
        }

        return logicalLines.ToArray();
    }

    private static bool TrySplitAttachedTrailingOption(
        string line,
        out string prefix,
        out string option)
    {
        prefix = string.Empty;
        option = string.Empty;

        MatchCollection matches = EmbeddedOptionNameRegex().Matches(line);
        for (int index = matches.Count - 1; index >= 0; index--)
        {
            Match match = matches[index];
            if (match.Index <= 2)
            {
                continue;
            }

            string candidatePrefix = line[..match.Index].TrimEnd();
            string candidateOption = line[match.Index..].Trim();
            if (candidateOption.Any(char.IsWhiteSpace) ||
                candidateOption.EndsWith('.') ||
                candidateOption.EndsWith(',') ||
                candidateOption.EndsWith(';') ||
                candidateOption.EndsWith(':') ||
                candidateOption.EndsWith('\'') ||
                candidateOption.EndsWith('"'))
            {
                continue;
            }

            bool followsCompletedDescription = candidatePrefix.Contains(';') &&
                (candidatePrefix.EndsWith('.') ||
                 candidatePrefix.EndsWith('!') ||
                 candidatePrefix.EndsWith('?'));
            bool followsConfigFileSyntax = candidatePrefix.TrimStart().StartsWith("@<", StringComparison.Ordinal) ||
                candidatePrefix.TrimStart().StartsWith("$<", StringComparison.Ordinal);
            if (!followsCompletedDescription && !followsConfigFileSyntax)
            {
                continue;
            }

            prefix = candidatePrefix;
            option = candidateOption;
            return true;
        }

        return false;
    }

    private static (string Syntax, string Description) SplitOptionLine(string line)
    {
        int descriptionDelimiter = line.IndexOf(';');
        if (descriptionDelimiter >= 0)
        {
            return (
                line[..descriptionDelimiter].TrimEnd(),
                NormalizeDescription(line[(descriptionDelimiter + 1)..]));
        }

        string[] columns = ColumnSeparatorRegex().Split(line, 2);
        return (
            columns[0].Trim(),
            columns.Length > 1
                ? NormalizeDescription(columns[1])
                : string.Empty);
    }

    private static string NormalizeDescription(string description) =>
        (description ?? string.Empty).Trim().TrimStart(';').TrimStart();

    private static string MergeDescriptionContinuation(
        string description,
        string continuation,
        bool startsIndented)
    {
        if (string.IsNullOrWhiteSpace(continuation))
        {
            return description;
        }
        if (string.IsNullOrWhiteSpace(description))
        {
            return continuation;
        }

        // A terminal soft-wrap starts in column zero. When it cuts a word at
        // the right edge, ConPTY may repeat the edge character on the next row
        // ("prefi" + "ix"). Rejoin that case without introducing a space.
        bool likelyMidWordWrap = !startsIndented &&
            description.Length >= 80 &&
            char.IsLetterOrDigit(description[^1]) &&
            char.IsLower(continuation[0]);
        if (likelyMidWordWrap)
        {
            int continuationStart = description[^1] == continuation[0] ? 1 : 0;
            return description + continuation[continuationStart..];
        }

        return $"{description} {continuation}".Trim();
    }

    private static bool IsEntirelyOptional(string placeholder)
    {
        if (placeholder.Length < 2 || placeholder[0] != '[')
        {
            return false;
        }

        int depth = 0;
        for (int index = 0; index < placeholder.Length; index++)
        {
            depth += placeholder[index] switch
            {
                '[' => 1,
                ']' => -1,
                _ => 0,
            };

            if (depth == 0)
            {
                return index == placeholder.Length - 1;
            }
            if (depth < 0)
            {
                return false;
            }
        }

        return false;
    }

    private sealed class MutableHelpOption
    {
        public List<string> Names { get; init; } = [];
        public string GroupName { get; init; } = string.Empty;
        public string Syntax { get; init; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ArgumentPlaceholder { get; init; } = string.Empty;
        public bool IsArgumentRequired { get; init; }

        public ComponentCommandHelpOption ToImmutable() => new()
        {
            Names = Names,
            GroupName = GroupName,
            Syntax = Syntax,
            Description = Description,
            ArgumentPlaceholder = ArgumentPlaceholder,
            IsArgumentRequired = IsArgumentRequired,
        };
    }
}
