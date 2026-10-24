using System.Text;
using System.Text.RegularExpressions;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.JSON;

namespace CDPIUI.AddOns.ConfigImport.Generic;

/// <summary>
/// Imports command files, text configs and JSON Configs for a component that is not zapret.
/// The component supplies the executable name; the source scripts are never executed.
/// </summary>
internal static class GenericCommandImporter
{
    public static ConfigImportResult Import(
        string filePath,
        ConfigImportTarget target,
        string executable)
    {
        var context = new GenericImportContext(target, filePath);
        try
        {
            string extension = Path.GetExtension(filePath);
            ConfigItem? config;
            if (extension.Equals(".bat", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase))
            {
                config = ImportBatch(filePath, executable, context);
            }
            else if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
            {
                config = ImportText(filePath, executable, context);
            }
            else if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                config = ImportJson(filePath, context);
            }
            else
            {
                context.AddError(
                    "UNSUPPORTED_FORMAT",
                    $"The '{extension}' file format is not supported.",
                    filePath);
                config = null;
            }

            return context.CreateResult(config);
        }
        catch (Exception exception)
        {
            context.AddError("IMPORT_FAILED", exception.Message, filePath);
            return context.CreateResult();
        }
    }

    private static ConfigItem? ImportBatch(
        string filePath,
        string executable,
        GenericImportContext context)
    {
        context.AddSourceFile(filePath);
        IReadOnlyList<BatchLogicalLine> lines = BatchLogicalLineReader.Read(filePath);
        List<(string Arguments, int LineNumber, Dictionary<string, GenericBatchVariable> Variables)> launches =
            CollectLaunches(lines, filePath, executable);

        if (launches.Count == 0)
        {
            context.AddError(
                "EXECUTABLE_LAUNCH_NOT_FOUND",
                $"The command file does not contain a supported launch of {executable}.",
                filePath);
            return null;
        }
        if (launches.Count > 1)
        {
            context.AddError(
                "MULTIPLE_EXECUTABLE_LAUNCHES",
                $"The command file contains more than one launch of {executable}; automatic selection is ambiguous.",
                filePath);
            return null;
        }

        var selected = launches[0];
        string arguments = selected.Arguments;
        string argumentsSourcePath = filePath;
        if (TryResolveTextInclude(arguments, filePath, selected.Variables, out string includePath))
        {
            context.AddSourceFile(includePath);
            arguments = ReadTextArguments(includePath, executable, selected.Variables);
            argumentsSourcePath = includePath;
        }

        Dictionary<string, GenericBatchVariable> usedVariables = GetUsedVariables(
            arguments,
            selected.Variables,
            context,
            filePath);
        CollectReferencedFiles(arguments, argumentsSourcePath, selected.Variables, context);
        foreach (GenericBatchVariable variable in usedVariables.Values)
        {
            CollectReferencedFiles(variable.Value, filePath, selected.Variables, context);
            foreach (string alternative in variable.Alternatives)
                CollectReferencedFiles(alternative, filePath, selected.Variables, context);
        }

        return GenericConfigItemFactory.Create(
            Path.GetFileNameWithoutExtension(filePath),
            arguments,
            usedVariables,
            context.Target);
    }

    private static List<(string Arguments, int LineNumber, Dictionary<string, GenericBatchVariable> Variables)> CollectLaunches(
        IReadOnlyList<BatchLogicalLine> lines,
        string filePath,
        string executable)
    {
        Dictionary<string, GenericBatchVariable> variables = GenericVariableTable.Create();
        var launches = new List<(string Arguments, int LineNumber, Dictionary<string, GenericBatchVariable> Variables)>();

        foreach (BatchLogicalLine logicalLine in lines)
        {
            string line = logicalLine.Text.Trim();
            if (line.Length == 0)
                continue;

            if (GenericBatchSyntax.TryParseCommentedSet(line, out string alternativeName, out string alternativeValue))
            {
                GenericVariableTable.Set(variables, alternativeName, alternativeValue, true);
                continue;
            }
            if (GenericBatchSyntax.IsComment(line))
                continue;
            if (GenericBatchSyntax.TryParseSet(line, out string variableName, out string variableValue))
            {
                GenericVariableTable.Set(variables, variableName, variableValue, false);
                continue;
            }
            if (TryExtractArguments(line, executable, filePath, variables, out string launchArguments))
                launches.Add((launchArguments, logicalLine.LineNumber, GenericVariableTable.Clone(variables)));
        }

        return launches;
    }

    private static ConfigItem? ImportText(
        string filePath,
        string executable,
        GenericImportContext context)
    {
        context.AddSourceFile(filePath);
        string arguments = ReadTextArguments(
            filePath,
            executable,
            GenericVariableTable.Create());
        if (string.IsNullOrWhiteSpace(arguments))
        {
            context.AddError("TEXT_CONFIG_EMPTY", "The text config does not contain launch arguments.", filePath);
            return null;
        }

        CollectReferencedFiles(
            arguments,
            filePath,
            GenericVariableTable.Create(),
            context);
        return GenericConfigItemFactory.Create(
            ReadTextConfigName(filePath) ?? Path.GetFileNameWithoutExtension(filePath),
            arguments,
            GenericVariableTable.Create(),
            context.Target);
    }

    private static ConfigItem? ImportJson(string filePath, GenericImportContext context)
    {
        context.AddSourceFile(filePath);
        ConfigItem? config = JSONConvertor.LoadJson<ConfigItem>(filePath);
        if (config == null || string.IsNullOrWhiteSpace(config.startup_string))
        {
            OldConfigItem? oldConfig = JSONConvertor.LoadJson<OldConfigItem>(filePath);
            if (string.IsNullOrWhiteSpace(oldConfig?.custom_parameters))
            {
                context.AddError("JSON_CONFIG_INVALID", "The JSON file is not a supported Config.", filePath);
                return null;
            }

            config = new ConfigItem
            {
                meta = GenericConfigItemFactory.ConfigMeta,
                name = Path.GetFileNameWithoutExtension(filePath),
                startup_string = oldConfig.custom_parameters,
                jparams = [],
                variables = [],
            };
        }

        config.target = [context.Target.ComponentId, ConfigImportTargets.ResolveVersion(context.Target)];
        config.name ??= Path.GetFileNameWithoutExtension(filePath);
        return config;
    }

    private static string ReadTextArguments(
        string filePath,
        string executable,
        IReadOnlyDictionary<string, GenericBatchVariable> variables)
    {
        IReadOnlyList<BatchLogicalLine> lines = BatchLogicalLineReader.Read(filePath);
        var content = new StringBuilder();
        foreach (BatchLogicalLine line in lines)
        {
            string value = line.Text.Trim();
            if (value.Length == 0 || GenericBatchSyntax.IsComment(value))
                continue;

            if (TryExtractArguments(value, executable, filePath, variables, out string extracted))
                value = extracted;
            if (content.Length > 0)
                content.Append(' ');
            content.Append(value);
        }

        return content.ToString();
    }

    private static string? ReadTextConfigName(string filePath)
    {
        foreach (string line in File.ReadLines(filePath))
        {
            Match match = GenericBatchSyntax.ConfigNameRegex().Match(line);
            if (match.Success)
                return match.Groups["value"].Value.Trim();
        }
        return null;
    }

    private static bool TryResolveTextInclude(
        string arguments,
        string sourcePath,
        IReadOnlyDictionary<string, GenericBatchVariable> variables,
        out string includePath)
    {
        includePath = string.Empty;
        List<GenericCommandToken> tokens = GenericBatchSyntax.Tokenize(arguments);
        if (tokens.Count != 1)
            return false;

        string expanded = GenericBatchExpander.ExpandBatchValue(tokens[0].Value.TrimStart('@', '$'), sourcePath, variables);
        string candidate = Path.IsPathRooted(expanded)
            ? expanded
            : Path.Combine(Path.GetDirectoryName(sourcePath)!, expanded);
        try
        {
            candidate = Path.GetFullPath(candidate);
        }
        catch
        {
            return false;
        }

        if (!Path.GetExtension(candidate).Equals(".txt", StringComparison.OrdinalIgnoreCase) || !File.Exists(candidate))
            return false;
        includePath = candidate;
        return true;
    }

    private static Dictionary<string, GenericBatchVariable> GetUsedVariables(
        string input,
        IReadOnlyDictionary<string, GenericBatchVariable> variables,
        GenericImportContext context,
        string sourcePath)
    {
        Dictionary<string, GenericBatchVariable> result = GenericVariableTable.Create();
        var pending = new Queue<string>(GenericBatchExpander.GetVariableNames(input));
        var inspected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (pending.Count > 0)
        {
            string name = pending.Dequeue();
            if (!inspected.Add(name))
                continue;
            if (!variables.TryGetValue(name, out GenericBatchVariable? variable))
            {
                if (!GenericBatchExpander.IsBuiltinBatchVariable(name))
                    context.AddWarning("UNRESOLVED_VARIABLE", $"Batch variable '{name}' could not be resolved.", sourcePath);
                continue;
            }

            result[name] = variable.Clone();
            foreach (string nested in GenericBatchExpander.GetVariableNames(variable.Value))
                pending.Enqueue(nested);
            foreach (string alternative in variable.Alternatives)
            {
                foreach (string nested in GenericBatchExpander.GetVariableNames(alternative))
                    pending.Enqueue(nested);
            }
        }
        return result;
    }

    private static bool TryExtractArguments(
        string line,
        string executable,
        string sourcePath,
        IReadOnlyDictionary<string, GenericBatchVariable> variables,
        out string arguments)
    {
        arguments = string.Empty;
        foreach (GenericCommandToken token in GenericBatchSyntax.Tokenize(line))
        {
            string expanded = GenericBatchExpander.ExpandBatchValue(token.Value, sourcePath, variables).Trim('"');
            string fileName = Path.GetFileName(expanded.Replace('/', Path.DirectorySeparatorChar));
            if (!ConfigImportExecutables.Normalize(fileName).Equals(executable, StringComparison.OrdinalIgnoreCase))
                continue;

            arguments = GenericBatchSyntax.TrimShellTail(line[token.End..]).Trim();
            return arguments.Length > 0;
        }
        return false;
    }

    private static void CollectReferencedFiles(
        string input,
        string sourcePath,
        IReadOnlyDictionary<string, GenericBatchVariable> variables,
        GenericImportContext context)
    {
        string expanded = GenericBatchExpander.ExpandBatchValue(input, sourcePath, variables);
        foreach (Match match in GenericBatchSyntax.ResourcePathRegex().Matches(expanded))
        {
            string rawPath = match.Groups["quotedPath"].Success
                ? match.Groups["quotedPath"].Value
                : match.Groups["plainPath"].Value;
            rawPath = rawPath.TrimStart('@', '$');
            string candidate = Path.IsPathRooted(rawPath)
                ? rawPath
                : Path.Combine(Path.GetDirectoryName(sourcePath)!, rawPath);
            try
            {
                string fullPath = Path.GetFullPath(candidate);
                if (File.Exists(fullPath))
                    context.AddReferencedFile(fullPath);
                else
                    context.AddMissingReferencedFile(fullPath, sourcePath);
            }
            catch
            {
                context.AddWarning("INVALID_RESOURCE_PATH", $"A referenced path could not be resolved: {rawPath}", sourcePath);
            }
        }
    }
}
