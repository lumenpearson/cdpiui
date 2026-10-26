using System.Text;
using System.Text.RegularExpressions;

namespace CDPIUI.AddOns.ConfigImport.Zapret;

/// <summary>
/// Imports winws text configs and expands the response-file includes (@file) of a command line.
/// </summary>
internal static class ZapretTextConfigImporter
{
    public static ZapretImportedPayload? Import(
        string filePath,
        ZapretConfigGeneration? expectedGeneration,
        ZapretImportContext context,
        int depth)
    {
        if (depth > context.Options.MaxIncludeDepth)
        {
            context.AddError(
                "INCLUDE_DEPTH_EXCEEDED",
                $"Config include depth exceeds the allowed limit of {context.Options.MaxIncludeDepth}.",
                filePath);
            return null;
        }

        string fullPath = Path.GetFullPath(filePath);
        if (!context.EnterInclude(fullPath))
        {
            context.AddError("INCLUDE_CYCLE", "A cycle was detected in config file includes.", fullPath);
            return null;
        }

        try
        {
            context.AddSourceFile(fullPath);
            ZapretTextConfig textConfig;
            try
            {
                textConfig = ParseTextConfig(fullPath);
            }
            catch (Exception exception)
            {
                context.AddError("TEXT_CONFIG_READ_FAILED", exception.Message, fullPath);
                return null;
            }

            if (string.IsNullOrWhiteSpace(textConfig.StartupString) ||
                (!textConfig.StartupString.Contains("--", StringComparison.Ordinal) &&
                 !ZapretSyntax.StandaloneAtFileRegex().IsMatch(textConfig.StartupString)))
            {
                context.AddError(
                    "TEXT_CONFIG_HAS_NO_OPTIONS",
                    "The text file does not contain winws command-line options.",
                    fullPath);
                return null;
            }

            if (!TryResolveGeneration(textConfig, expectedGeneration, context, fullPath, out ZapretConfigGeneration generation))
                return null;

            ZapretIncludeExpansion expansion = ExpandConfigIncludes(
                textConfig.StartupString,
                fullPath,
                generation,
                ZapretVariableTable.Create(),
                context,
                depth);
            ZapretReferencedFileCollector.Collect(
                expansion.Text,
                fullPath,
                ZapretVariableTable.Create(),
                context);

            return new ZapretImportedPayload(
                generation,
                expansion.Text,
                textConfig.Name ?? expansion.Name ?? Path.GetFileNameWithoutExtension(fullPath),
                textConfig.BuiltinVersion ?? expansion.BuiltinVersion,
                ZapretVariableTable.Create(),
                []);
        }
        finally
        {
            context.LeaveInclude(fullPath);
        }
    }

    private static bool TryResolveGeneration(
        ZapretTextConfig textConfig,
        ZapretConfigGeneration? expectedGeneration,
        ZapretImportContext context,
        string fullPath,
        out ZapretConfigGeneration generation)
    {
        ZapretConfigGeneration? detected = ZapretSyntax.DetectGeneration(textConfig.StartupString, textConfig.BuiltinVersion);

        if (expectedGeneration.HasValue)
        {
            generation = expectedGeneration.Value;
            if (detected.HasValue && detected.Value != generation)
            {
                context.AddError(
                    "GENERATION_MISMATCH",
                    $"The referenced text config looks like {ZapretSyntax.GetExecutableName(detected.Value)}, but {ZapretSyntax.GetExecutableName(generation)} was expected.",
                    fullPath);
                return false;
            }

            return true;
        }

        if (detected.HasValue)
        {
            generation = detected.Value;
            return true;
        }

        if (!context.Options.DefaultTextConfigGeneration.HasValue)
        {
            context.AddError(
                "CONFIG_GENERATION_UNKNOWN",
                "The text file does not contain enough information to distinguish winws1 from winws2.",
                fullPath);
            generation = default;
            return false;
        }

        generation = context.Options.DefaultTextConfigGeneration.Value;
        return true;
    }

    public static ZapretIncludeExpansion ExpandConfigIncludes(
        string input,
        string referringFile,
        ZapretConfigGeneration expectedGeneration,
        IReadOnlyDictionary<string, ZapretBatchVariable> variables,
        ZapretImportContext context,
        int depth)
    {
        if (depth > context.Options.MaxIncludeDepth)
        {
            context.AddError(
                "INCLUDE_DEPTH_EXCEEDED",
                $"Config include depth exceeds the allowed limit of {context.Options.MaxIncludeDepth}.",
                referringFile);
            return new ZapretIncludeExpansion(input, null, null);
        }

        MatchCollection matches = ZapretSyntax.StandaloneAtFileRegex().Matches(input);
        if (matches.Count == 0)
            return new ZapretIncludeExpansion(input, null, null);

        foreach (Match match in matches)
        {
            string? fullCandidatePath = ResolveIncludedConfig(match, referringFile, variables, context);
            if (fullCandidatePath == null)
                continue;

            ZapretImportedPayload? nested = Import(
                fullCandidatePath,
                expectedGeneration,
                context,
                depth + 1);

            if (nested != null)
            {
                Group tokenGroup = match.Groups["token"];
                string remainingArguments = string.Concat(
                    input.AsSpan(0, tokenGroup.Index),
                    input.AsSpan(tokenGroup.Index + tokenGroup.Length)).Trim();
                if (remainingArguments.Length > 0)
                {
                    context.AddWarning(
                        "CONFIG_INCLUDE_IGNORES_OTHER_ARGUMENTS",
                        "A response-file config replaces the command line; other arguments are ignored by winws.",
                        referringFile);
                }

                return new ZapretIncludeExpansion(
                    nested.StartupString,
                    nested.Name,
                    nested.BuiltinVersion);
            }
        }

        return new ZapretIncludeExpansion(input, null, null);
    }

    /// <returns>The full path of the included text config, or null when the token names none.</returns>
    private static string? ResolveIncludedConfig(
        Match match,
        string referringFile,
        IReadOnlyDictionary<string, ZapretBatchVariable> variables,
        ZapretImportContext context)
    {
        string rawPath = match.Groups["quoted"].Success
            ? match.Groups["quoted"].Value
            : match.Groups["plain"].Value;

        string expandedPath = ZapretBatchExpander.ExpandBatchValue(rawPath, referringFile, variables);
        string candidatePath = Path.IsPathRooted(expandedPath)
            ? expandedPath
            : Path.Combine(Path.GetDirectoryName(referringFile)!, expandedPath);

        string fullCandidatePath;
        try
        {
            fullCandidatePath = Path.GetFullPath(candidatePath);
        }
        catch
        {
            return null;
        }

        if (!File.Exists(fullCandidatePath))
        {
            context.AddError(
                "INCLUDED_CONFIG_NOT_FOUND",
                $"Referenced config file '{rawPath}' was not found.",
                referringFile);
            return null;
        }

        return LooksLikeTextConfig(fullCandidatePath) ? fullCandidatePath : null;
    }

    private static ZapretTextConfig ParseTextConfig(string filePath)
    {
        string[] lines = CDPIUI.Core.System.BatchFileEncoding.ReadAllLines(filePath);
        var command = new StringBuilder();
        string? name = null;
        string? builtinVersion = null;

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            if (line.StartsWith('#'))
            {
                Match nameMatch = ZapretSyntax.ConfigNameRegex().Match(line);
                if (nameMatch.Success && string.IsNullOrWhiteSpace(name))
                    name = nameMatch.Groups["value"].Value.Trim();

                Match versionMatch = ZapretSyntax.BuiltinVersionRegex().Match(line);
                if (versionMatch.Success && string.IsNullOrWhiteSpace(builtinVersion))
                    builtinVersion = versionMatch.Groups["value"].Value.Trim();

                continue;
            }

            if (command.Length > 0)
                command.Append(' ');
            command.Append(line);
        }

        return new ZapretTextConfig(command.ToString(), name, builtinVersion);
    }

    private static bool LooksLikeTextConfig(string filePath)
    {
        if (!Path.GetExtension(filePath).Equals(".txt", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            foreach (string rawLine in CDPIUI.Core.System.BatchFileEncoding.ReadAllLines(filePath).Take(64))
            {
                string line = rawLine.Trim();
                if (line.Length == 0)
                    continue;
                if (ZapretSyntax.BuiltinVersionRegex().IsMatch(line) || line.StartsWith("--", StringComparison.Ordinal))
                    return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }
}
