using System.Text;
using System.Text.RegularExpressions;

namespace CDPIUI.AddOns.ConfigImport.Zapret;

/// <summary>
/// Reads the variables that scripts started with "call" set, without running them. Each script
/// and argument set is inspected once per import.
/// </summary>
internal sealed class ZapretCalledScriptInspector(ZapretImportContext context)
{
    private readonly HashSet<string> _inspectedCalls = new(StringComparer.OrdinalIgnoreCase);

    public static bool TryGetCalledScript(
        string line,
        string callerPath,
        IReadOnlyDictionary<string, ZapretBatchVariable> variables,
        out ZapretCalledScript calledScript)
    {
        calledScript = null!;
        Match match = ZapretSyntax.CallScriptRegex().Match(line);
        if (!match.Success)
            return false;

        string token = match.Groups["quoted"].Success
            ? match.Groups["quoted"].Value
            : match.Groups["plain"].Value;
        if (token.StartsWith(':'))
            return false;

        string expanded = ZapretBatchExpander.ExpandBatchValue(token, callerPath, variables);
        string candidate = Path.IsPathRooted(expanded)
            ? expanded
            : Path.Combine(Path.GetDirectoryName(callerPath)!, expanded);

        if (!Path.HasExtension(candidate))
            candidate += ".bat";

        try
        {
            string fullPath = Path.GetFullPath(candidate);
            if (!File.Exists(fullPath) ||
                (!Path.GetExtension(fullPath).Equals(".bat", StringComparison.OrdinalIgnoreCase) &&
                 !Path.GetExtension(fullPath).Equals(".cmd", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            string argumentsText = ZapretSyntax.TrimShellTail(line[(match.Index + match.Length)..]).Trim();
            calledScript = new ZapretCalledScript(fullPath, ZapretSyntax.SplitBatchArguments(argumentsText));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void CollectVariables(
        ZapretCalledScript call,
        IDictionary<string, ZapretBatchVariable> variables,
        int depth)
    {
        string callKey = $"{call.Path}\0{string.Join('\0', call.Arguments)}";
        if (depth > context.Options.MaxIncludeDepth || !_inspectedCalls.Add(callKey))
            return;

        IReadOnlyList<BatchLogicalLine> lines;
        try
        {
            lines = BatchLogicalLineReader.Read(call.Path);
            context.AddSourceFile(call.Path);
        }
        catch (Exception exception)
        {
            context.AddWarning("CALLED_SCRIPT_READ_FAILED", exception.Message, call.Path);
            return;
        }

        var localVariables = ZapretVariableTable.Clone((IReadOnlyDictionary<string, ZapretBatchVariable>)variables);
        string? requestedCommand = call.Arguments.FirstOrDefault();
        IReadOnlyList<string> dispatchedLabels = ZapretBatchLabels.FindDispatchedLabels(lines, call.Arguments.FirstOrDefault());
        if (dispatchedLabels.Count > 0)
        {
            var visitedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string label in dispatchedLabels)
                CollectLabelVariables(call.Path, lines, label, localVariables, depth, visitedLabels);
        }
        else
        {
            CollectTopLevelVariables(call.Path, lines, localVariables, depth);
        }

        if (requestedCommand?.Equals("load_user_lists", StringComparison.OrdinalIgnoreCase) == true)
            CollectGeneratedFiles(call.Path, lines, "load_user_lists", localVariables);

        ZapretVariableTable.Merge(localVariables, variables);
    }

    private void CollectTopLevelVariables(
        string scriptPath,
        IReadOnlyList<BatchLogicalLine> lines,
        IDictionary<string, ZapretBatchVariable> variables,
        int depth)
    {
        foreach (BatchLogicalLine logicalLine in lines)
        {
            string line = logicalLine.Text.Trim();
            if (ZapretSyntax.BatchLabelRegex().IsMatch(line))
                break;

            CollectStaticLineVariables(line, scriptPath, variables, depth);
        }
    }

    private void CollectLabelVariables(
        string scriptPath,
        IReadOnlyList<BatchLogicalLine> lines,
        string label,
        IDictionary<string, ZapretBatchVariable> variables,
        int depth,
        ISet<string> visitedLabels)
    {
        if (!visitedLabels.Add(label) || !ZapretBatchLabels.TryGetLabelRange(lines, label, out int start, out int end))
            return;

        for (int index = start; index < end; index++)
        {
            string line = lines[index].Text.Trim();
            Match localCall = ZapretSyntax.LocalLabelCallRegex().Match(line);
            if (localCall.Success)
            {
                CollectLabelVariables(
                    scriptPath,
                    lines,
                    localCall.Groups["label"].Value,
                    variables,
                    depth,
                    visitedLabels);
                continue;
            }

            CollectStaticLineVariables(line, scriptPath, variables, depth);
        }
    }

    private void CollectStaticLineVariables(
        string line,
        string scriptPath,
        IDictionary<string, ZapretBatchVariable> variables,
        int depth)
    {
        if (ZapretSyntax.TryParseCommentedSet(line, out string alternativeName, out string alternativeValue))
        {
            ZapretVariableTable.Set(variables, alternativeName, alternativeValue, isAlternative: true);
            return;
        }

        if (ZapretSyntax.IsComment(line))
            return;

        if (ZapretSyntax.TryParseSet(line, out string name, out string value))
        {
            ZapretVariableTable.Set(
                variables,
                name,
                value,
                isAlternative: false,
                isConditionalCandidate: true);
            return;
        }

        if (TryGetCalledScript(line, scriptPath, (IReadOnlyDictionary<string, ZapretBatchVariable>)variables, out ZapretCalledScript nestedCall))
            CollectVariables(nestedCall, variables, depth + 1);
    }

    private void CollectGeneratedFiles(
        string scriptPath,
        IReadOnlyList<BatchLogicalLine> lines,
        string label,
        IReadOnlyDictionary<string, ZapretBatchVariable> variables)
    {
        if (!ZapretBatchLabels.TryGetLabelRange(lines, label, out int start, out int end))
            return;

        var contents = new Dictionary<string, StringBuilder>(StringComparer.OrdinalIgnoreCase);
        for (int index = start; index < end; index++)
        {
            Match match = ZapretSyntax.EchoFileWriteRegex().Match(lines[index].Text.Trim());
            if (!match.Success)
                continue;

            string expandedPath = ZapretBatchExpander.ExpandBatchValue(
                match.Groups["path"].Value.Trim(),
                scriptPath,
                variables);
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(expandedPath);
            }
            catch
            {
                continue;
            }

            if (File.Exists(fullPath))
                continue;

            bool append = match.Groups["operator"].Value == ">>";
            if (!append || !contents.TryGetValue(fullPath, out StringBuilder? content))
            {
                content = new StringBuilder();
                contents[fullPath] = content;
            }

            content.Append(match.Groups["content"].Value.TrimEnd()).Append("\r\n");
        }

        string sourceRoot = Path.GetDirectoryName(scriptPath)!;
        foreach (var pair in contents)
            context.AddGeneratedFile(sourceRoot, pair.Key, pair.Value.ToString());
    }
}
