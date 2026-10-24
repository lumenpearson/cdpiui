using System.Text.RegularExpressions;

namespace CDPIUI.AddOns.ConfigImport.Zapret;

/// <summary>
/// Finds the single winws launch of a batch file and resolves the variables it uses.
/// </summary>
internal static class ZapretBatchImporter
{
    public static ZapretImportedPayload? Import(string filePath, ZapretImportContext context)
    {
        context.AddSourceFile(filePath);
        IReadOnlyList<BatchLogicalLine> lines;

        try
        {
            lines = BatchLogicalLineReader.Read(filePath);
        }
        catch (Exception exception)
        {
            context.AddError("BATCH_READ_FAILED", exception.Message, filePath);
            return null;
        }

        List<(ZapretBatchLaunch Launch, Dictionary<string, ZapretBatchVariable> Variables)> launches =
            CollectLaunches(lines, filePath, context);

        if (launches.Count == 0)
        {
            context.AddError(
                "WINWS_LAUNCH_NOT_FOUND",
                "The batch file does not contain a supported winws or winws2 launch.",
                filePath);
            return null;
        }

        if (launches.Count > 1)
        {
            context.AddError(
                "MULTIPLE_WINWS_LAUNCHES",
                "The batch file contains more than one winws launch. Automatic selection would be ambiguous.",
                filePath);
            return null;
        }

        return CreatePayload(filePath, launches[0].Launch, launches[0].Variables, context);
    }

    private static List<(ZapretBatchLaunch Launch, Dictionary<string, ZapretBatchVariable> Variables)> CollectLaunches(
        IReadOnlyList<BatchLogicalLine> lines,
        string filePath,
        ZapretImportContext context)
    {
        Dictionary<string, ZapretBatchVariable> variables = ZapretVariableTable.Create();
        foreach (var predefined in context.Options.PredefinedVariables)
            ZapretVariableTable.Set(variables, predefined.Key, predefined.Value, isAlternative: false);

        var calledScripts = new ZapretCalledScriptInspector(context);
        var launches = new List<(ZapretBatchLaunch Launch, Dictionary<string, ZapretBatchVariable> Variables)>();

        foreach (BatchLogicalLine logicalLine in lines)
        {
            string line = logicalLine.Text.Trim();
            if (line.Length == 0)
                continue;

            if (ZapretSyntax.TryParseCommentedSet(line, out string alternativeName, out string alternativeValue))
            {
                ZapretVariableTable.Set(variables, alternativeName, alternativeValue, isAlternative: true);
                continue;
            }

            if (ZapretSyntax.IsComment(line))
                continue;

            if (ZapretSyntax.TryParseSet(line, out string variableName, out string variableValue))
            {
                ZapretVariableTable.Set(variables, variableName, variableValue, isAlternative: false);
                continue;
            }

            if (ZapretCalledScriptInspector.TryGetCalledScript(line, filePath, variables, out ZapretCalledScript calledScript))
                calledScripts.CollectVariables(calledScript, variables, depth: 0);

            if (TryExtractLaunch(line, logicalLine.LineNumber, out ZapretBatchLaunch launch))
                launches.Add((launch, ZapretVariableTable.Clone(variables)));
        }

        return launches;
    }

    private static ZapretImportedPayload? CreatePayload(
        string filePath,
        ZapretBatchLaunch selected,
        Dictionary<string, ZapretBatchVariable> snapshot,
        ZapretImportContext context)
    {
        ZapretIncludeExpansion expansion = ZapretTextConfigImporter.ExpandConfigIncludes(
            selected.Arguments,
            filePath,
            selected.Generation,
            snapshot,
            context,
            depth: 0);

        ZapretConfigGeneration? detected = ZapretSyntax.DetectGeneration(expansion.Text, expansion.BuiltinVersion);
        if (detected.HasValue && detected.Value != selected.Generation)
        {
            context.AddError(
                "GENERATION_MISMATCH",
                $"The batch file starts {ZapretSyntax.GetExecutableName(selected.Generation)}, but its arguments look like a {ZapretSyntax.GetExecutableName(detected.Value)} config.",
                filePath,
                selected.LineNumber);
            return null;
        }

        string startupString = ZapretBatchExpander.NormalizeDelayedVariableSyntax(expansion.Text);
        Dictionary<string, ZapretBatchVariable> usedVariables = GetUsedVariables(startupString, snapshot, context, filePath);
        ZapretBatchExpander.NormalizeDelayedVariableSyntax(usedVariables.Values);
        startupString = ZapretActiveFileSelectors.Create(startupString, filePath, snapshot, usedVariables);
        IReadOnlyList<ZapretConditionalVariable> conditionalVariables = CreateConditionalVariables(usedVariables);
        ZapretReferencedFileCollector.Collect(startupString, filePath, snapshot, context);
        foreach (ZapretBatchVariable variable in usedVariables.Values)
        {
            ZapretReferencedFileCollector.Collect(variable.Value, filePath, snapshot, context);
            foreach (string alternative in variable.Alternatives)
                ZapretReferencedFileCollector.Collect(alternative, filePath, snapshot, context);
        }

        return new ZapretImportedPayload(
            selected.Generation,
            startupString,
            expansion.Name ?? Path.GetFileNameWithoutExtension(filePath),
            selected.Generation == ZapretConfigGeneration.Winws2 ? expansion.BuiltinVersion : null,
            usedVariables,
            conditionalVariables);
    }

    private static bool TryExtractLaunch(string line, int lineNumber, out ZapretBatchLaunch launch)
    {
        launch = null!;
        Match match = ZapretSyntax.WinwsExecutableRegex().Match(line);
        if (!match.Success)
            return false;

        int argumentsStart = match.Index + match.Length;
        if (argumentsStart < line.Length && line[argumentsStart] == '"')
            argumentsStart++;

        string arguments = ZapretSyntax.TrimShellTail(line[argumentsStart..]).Trim();
        if (arguments.Length == 0 ||
            (!arguments.Contains("--", StringComparison.Ordinal) &&
             !ZapretSyntax.StandaloneAtFileRegex().IsMatch(arguments) &&
             !ZapretSyntax.BatchVariableRegex().IsMatch(arguments)))
        {
            return false;
        }

        string executable = match.Groups["exe"].Value;
        ZapretConfigGeneration generation = executable.StartsWith("winws2", StringComparison.OrdinalIgnoreCase)
            ? ZapretConfigGeneration.Winws2
            : ZapretConfigGeneration.Winws1;

        launch = new ZapretBatchLaunch(generation, arguments, lineNumber);
        return true;
    }

    private static Dictionary<string, ZapretBatchVariable> GetUsedVariables(
        string startupString,
        IReadOnlyDictionary<string, ZapretBatchVariable> allVariables,
        ZapretImportContext context,
        string sourcePath)
    {
        Dictionary<string, ZapretBatchVariable> result = ZapretVariableTable.Create();
        var queue = new Queue<string>(ZapretBatchExpander.GetReferencedVariableNames(startupString));
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (queue.Count > 0)
        {
            string name = queue.Dequeue();
            if (!visited.Add(name))
                continue;

            if (!allVariables.TryGetValue(name, out ZapretBatchVariable? variable))
            {
                context.AddWarning(
                    "UNRESOLVED_BATCH_VARIABLE",
                    $"Batch variable '%{name}%' could not be resolved statically.",
                    sourcePath);
                continue;
            }

            result[name] = variable.Clone();
            foreach (string nestedName in ZapretBatchExpander.GetReferencedVariableNames(variable.Value))
                queue.Enqueue(nestedName);
        }

        return result;
    }

    private static IReadOnlyList<ZapretConditionalVariable> CreateConditionalVariables(
        IReadOnlyDictionary<string, ZapretBatchVariable> usedVariables)
    {
        var result = new List<ZapretConditionalVariable>();
        var parameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ZapretBatchVariable variable in usedVariables.Values)
        {
            if (!variable.IsConditionalCandidate)
                continue;

            List<string> values = variable.ObservedValues
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (values.Count != 2)
                continue;

            string parameterName = CreateConditionalParameterName(variable.Name, parameterNames);
            result.Add(new ZapretConditionalVariable(
                variable.Name,
                parameterName,
                variable.Value.Equals(values[1], StringComparison.Ordinal),
                values[1],
                values[0]));
        }

        return result;
    }

    private static string CreateConditionalParameterName(string variableName, ISet<string> usedNames)
    {
        string identifier = new(variableName
            .Where(character => char.IsLetterOrDigit(character) || character == '_')
            .ToArray());
        if (identifier.Length == 0)
            identifier = "Variable";

        string baseName = $"use{char.ToUpperInvariant(identifier[0])}{identifier[1..]}";
        string result = baseName;
        for (int suffix = 2; !usedNames.Add(result); suffix++)
            result = $"{baseName}{suffix}";
        return result;
    }
}
