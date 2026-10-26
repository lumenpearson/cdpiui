using System.Text.RegularExpressions;

namespace CDPIUI.AddOns.ConfigImport.Zapret;

/// <summary>
/// Records the BIN, TXT and LUA files a command line names, and the ones that are missing.
/// </summary>
internal static class ZapretReferencedFileCollector
{
    public static void Collect(
        string startupString,
        string sourcePath,
        IReadOnlyDictionary<string, ZapretBatchVariable> variables,
        ZapretImportContext context)
    {
        string expanded = ZapretBatchExpander.ExpandBatchValue(startupString, sourcePath, variables);
        foreach (Match match in ZapretSyntax.ResourcePathRegex().Matches(expanded))
        {
            string rawPath = match.Groups["quotedPath"].Success
                ? match.Groups["quotedPath"].Value
                : match.Groups["plainPath"].Value;
            rawPath = rawPath.TrimStart('@', '$');

            string candidate = Path.IsPathRooted(rawPath)
                ? rawPath
                : Path.Combine(Path.GetDirectoryName(sourcePath)!, rawPath);
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(candidate);
            }
            catch
            {
                continue;
            }

            if (File.Exists(fullPath))
            {
                context.AddReferencedFile(fullPath);
            }
            else if (!context.IsGeneratedFile(fullPath))
            {
                context.AddMissingReferencedFile(fullPath, sourcePath);
            }
        }
    }
}
