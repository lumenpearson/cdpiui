using System;
using System.Collections.Generic;
using System.Linq;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.ViewModels;

namespace CDPIUI.Helper.AddOns.BlockCheck2;

/// <summary>
/// Rewrites the file options of a BlockCheck2 preset command line (--hostlist,
/// --hostlist-exclude, --lua-init, --blob) so they point to the stored copies.
/// </summary>
internal static class BlockCheck2PresetReferenceRewriter
{
    public static string RewriteFileReferences(
        string arguments,
        IReadOnlyList<BlockCheck2PresetFileReplacement> replacements)
    {
        IReadOnlyList<string> tokens = ComponentCommandLineFormatter.Tokenize(arguments);
        List<string> output = [];
        for (int index = 0; index < tokens.Count; index++)
        {
            string token = tokens[index];
            int equalsIndex = token.IndexOf('=');
            string option = equalsIndex >= 0 ? token[..equalsIndex] : token;
            string value = equalsIndex >= 0 ? token[(equalsIndex + 1)..] : string.Empty;
            bool usesSeparateValue = false;

            BlockCheck2PresetFileKind? kind = GetFileKind(option);
            if (kind == null)
            {
                output.Add(token);
                continue;
            }

            if (equalsIndex < 0 && index + 1 < tokens.Count && !tokens[index + 1].StartsWith("-", StringComparison.Ordinal))
            {
                value = tokens[++index];
                usesSeparateValue = true;
            }
            if (string.IsNullOrWhiteSpace(value))
            {
                output.Add(token);
                continue;
            }

            string rewritten = RewriteValue(option, value, kind.Value, replacements);
            if (rewritten == null)
            {
                output.Add(token);
                if (usesSeparateValue)
                {
                    output.Add(value);
                }
                continue;
            }
            output.Add(rewritten);
        }
        return string.Join(' ', output);
    }

    public static string NormalizeReference(string reference)
    {
        string value = (reference ?? string.Empty).Trim().Trim('"', '\'');
        if (value.StartsWith('@'))
        {
            value = value[1..].Trim().Trim('"', '\'');
        }
        if (value.StartsWith("$GETCURRENTDIR()", StringComparison.OrdinalIgnoreCase))
        {
            value = value[1..];
        }
        else if (value.StartsWith('$'))
        {
            value = value[1..].Trim().Trim('"', '\'');
        }
        return value.Replace('\\', '/');
    }

    private static BlockCheck2PresetFileKind? GetFileKind(string option)
    {
        return option.Equals("--hostlist", StringComparison.OrdinalIgnoreCase) ||
               option.Equals("--hostlist-exclude", StringComparison.OrdinalIgnoreCase)
            ? BlockCheck2PresetFileKind.SiteList
            : option.Equals("--lua-init", StringComparison.OrdinalIgnoreCase)
                ? BlockCheck2PresetFileKind.Library
                : option.Equals("--blob", StringComparison.OrdinalIgnoreCase)
                    ? BlockCheck2PresetFileKind.Payload
                    : null;
    }

    /// <summary>
    /// Returns the rewritten "option=value" token, or null when the value is kept as it is.
    /// </summary>
    private static string RewriteValue(
        string option,
        string value,
        BlockCheck2PresetFileKind kind,
        IReadOnlyList<BlockCheck2PresetFileReplacement> replacements)
    {
        string prefix = string.Empty;
        string reference = value;
        if (kind == BlockCheck2PresetFileKind.Payload)
        {
            string unquoted = value.Trim().Trim('"', '\'');
            int separatorIndex = unquoted.IndexOf(':');
            if (separatorIndex < 0)
            {
                // "--blob <value>" lost its value here.
                return null;
            }
            prefix = unquoted[..(separatorIndex + 1)];
            reference = unquoted[(separatorIndex + 1)..];
        }

        char sourceMarker = GetSourceMarker(reference);
        string normalizedReference = NormalizeReference(reference);
        BlockCheck2PresetFileReplacement replacement = replacements.FirstOrDefault(candidate =>
            candidate.Kind == kind &&
            string.Equals(
                NormalizeReference(candidate.OriginalReference),
                normalizedReference,
                StringComparison.OrdinalIgnoreCase));
        if (replacement == null)
        {
            return null;
        }

        string marker = kind == BlockCheck2PresetFileKind.SiteList
            ? string.Empty
            : sourceMarker == '\0'
                ? kind == BlockCheck2PresetFileKind.Library ? "@" : string.Empty
                : sourceMarker.ToString();
        string rewrittenValue = prefix + marker + replacement.ReplacementReference;
        return $"{option}=\"{rewrittenValue.Replace("\"", "\\\"")}\"";
    }

    private static char GetSourceMarker(string reference)
    {
        string value = reference.Trim().Trim('"', '\'');
        if (value.StartsWith("$GETCURRENTDIR()", StringComparison.OrdinalIgnoreCase))
        {
            return '\0';
        }
        return value.Length > 0 && (value[0] == '@' || value[0] == '$')
            ? value[0]
            : '\0';
    }
}
