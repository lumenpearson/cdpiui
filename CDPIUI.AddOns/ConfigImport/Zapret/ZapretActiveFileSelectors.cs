using System.Security.Cryptography;

namespace CDPIUI.AddOns.ConfigImport.Zapret;

/// <summary>
/// Replaces references to ACTIVE_*.bin files, which a script copies from one of the BIN files of
/// the folder, with a variable whose alternatives are those BIN files.
/// </summary>
internal static class ZapretActiveFileSelectors
{
    public static string Create(
        string startupString,
        string sourcePath,
        IDictionary<string, ZapretBatchVariable> allVariables,
        IDictionary<string, ZapretBatchVariable> usedVariables)
    {
        var selectors = new Dictionary<string, ZapretBatchVariable>(StringComparer.OrdinalIgnoreCase);
        string result = ZapretSyntax.ActiveBinaryReferenceRegex().Replace(startupString, match =>
        {
            string rootVariableName = match.Groups["root"].Value;
            string fileName = match.Groups["file"].Value;
            string selectorKey = $"{rootVariableName}\0{fileName}";
            if (selectors.TryGetValue(selectorKey, out ZapretBatchVariable? existingSelector))
                return $"%{existingSelector.Name}%";

            if (!allVariables.TryGetValue(rootVariableName, out ZapretBatchVariable? rootVariable))
                return match.Value;

            string sourceDirectory = ZapretBatchExpander.ExpandBatchValue(
                rootVariable.Value,
                sourcePath,
                (IReadOnlyDictionary<string, ZapretBatchVariable>)allVariables);
            if (!Directory.Exists(sourceDirectory))
                return match.Value;

            string[] alternatives = Directory.GetFiles(sourceDirectory, "*.bin")
                .Where(path => !Path.GetFileName(path).StartsWith("ACTIVE_", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (alternatives.Length == 0)
                return match.Value;

            string selectorName = CreateUniqueVariableName(
                Path.GetFileNameWithoutExtension(fileName),
                allVariables.Keys.Concat(usedVariables.Keys));
            string configDirectory = ZapretBatchExpander.TranslateBatchValueToConfig(rootVariable.Value);
            string activePath = Path.Combine(sourceDirectory, fileName);
            string selectedPath = FindEquivalentBinary(alternatives, activePath) ?? activePath;
            var selector = new ZapretBatchVariable(
                selectorName,
                $"{configDirectory}{Path.GetFileName(selectedPath)}");
            foreach (string alternative in alternatives)
                selector.AddAlternative($"{configDirectory}{Path.GetFileName(alternative)}");

            selectors[selectorKey] = selector;
            allVariables[selectorName] = selector;
            usedVariables[selectorName] = selector.Clone();
            return $"%{selectorName}%";
        });

        return result;
    }

    private static string? FindEquivalentBinary(IEnumerable<string> candidates, string activePath)
    {
        if (!File.Exists(activePath))
            return null;

        try
        {
            byte[] activeHash;
            using (FileStream activeStream = File.OpenRead(activePath))
                activeHash = SHA256.HashData(activeStream);

            string? result = null;
            foreach (string candidate in candidates)
            {
                using FileStream candidateStream = File.OpenRead(candidate);
                if (activeHash.AsSpan().SequenceEqual(SHA256.HashData(candidateStream)))
                    result = candidate;
            }
            return result;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string CreateUniqueVariableName(string source, IEnumerable<string> existingNames)
    {
        var existing = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        string baseName = new(source
            .Where(character => char.IsLetterOrDigit(character) || character == '_')
            .ToArray());
        if (baseName.Length == 0)
            baseName = "ImportedFile";

        string result = baseName;
        for (int suffix = 2; existing.Contains(result); suffix++)
            result = $"{baseName}_{suffix}";
        return result;
    }
}
