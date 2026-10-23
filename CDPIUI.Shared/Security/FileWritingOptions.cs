using CDPIUI.Shared.Basic.CommandLine;
using CDPIUI.Shared.Basic.Filesystem;
using System;
using System.Collections.Generic;
using System.IO;

namespace CDPIUI.Shared.Security
{
    /// <summary>
    /// Finds component options that write a file. The tray starts components with administrator
    /// rights, so such an option could otherwise write to any place on the disk.
    /// </summary>
    public static class FileWritingOptions
    {
        // Zapret and Zapret2 (winws, winws2). --debug writes a file only in the form --debug=@file.
        private static readonly HashSet<string> Names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "--debug", "--wf-save", "--hostlist-auto", "--hostlist-auto-debug"
        };

        // winws reads more arguments from "@file"; files nested deeper than this are not followed.
        private const int MaxArgumentFileDepth = 4;
        private const long MaxArgumentFileBytes = 1 << 20;

        /// <summary>
        /// Returns a description of the first option that writes outside the allowed roots, or null.
        /// </summary>
        /// <param name="commandLine">Arguments of the component</param>
        /// <param name="allowedRoots">Folders in which the component may write</param>
        /// <param name="baseDirectory">Folder for relative paths; null rejects relative paths</param>
        /// <param name="followArgumentFiles">Also check the files given as "@file"</param>
        public static string? FindOutsideWrite(
            string? commandLine,
            IEnumerable<string?> allowedRoots,
            string? baseDirectory,
            bool followArgumentFiles = false)
        {
            var roots = new List<string?>(allowedRoots);
            return Find(commandLine, roots, baseDirectory, followArgumentFiles ? MaxArgumentFileDepth : 0);
        }

        private static string? Find(string? commandLine, List<string?> roots, string? baseDirectory, int depth)
        {
            if (string.IsNullOrWhiteSpace(commandLine))
                return null;

            IReadOnlyList<string> tokens = CommandLineTokens.Tokenize(commandLine);
            for (int index = 0; index < tokens.Count; index++)
            {
                string token = CommandLineTokens.Unquote(tokens[index]);
                if (depth > 0 && token.Length > 1 && token[0] == '@')
                {
                    string? nested = CheckArgumentFile(token.Substring(1), roots, baseDirectory, depth);
                    if (nested != null)
                        return nested;
                    continue;
                }

                if (!CommandLineTokens.TryGetOptionName(tokens[index], out string name, out int equalsIndex) ||
                    !Names.Contains(name))
                    continue;

                string value = equalsIndex >= 0
                    ? tokens[index].Substring(equalsIndex + 1)
                    : index + 1 < tokens.Count && !CommandLineTokens.IsOption(tokens[index + 1]) ? tokens[index + 1] : string.Empty;
                value = CommandLineTokens.Unquote(value).Trim();
                if (name.Equals("--debug", StringComparison.OrdinalIgnoreCase))
                {
                    // --debug=0|1|syslog|android writes no file; --debug=@file does.
                    if (!value.StartsWith("@", StringComparison.Ordinal))
                        continue;
                    value = CommandLineTokens.Unquote(value.Substring(1)).Trim();
                }
                if (value.Length == 0)
                    continue;

                if (!IsAllowedPath(value, roots, baseDirectory))
                    return $"{name}: {value}";
            }
            return null;
        }

        private static string? CheckArgumentFile(string path, List<string?> roots, string? baseDirectory, int depth)
        {
            string? fullPath = Resolve(path, baseDirectory);
            if (fullPath == null)
                return $"@{path}";
            try
            {
                var file = new FileInfo(fullPath);
                if (!file.Exists)
                    return null;
                if (file.Length > MaxArgumentFileBytes)
                    return $"@{path}";
                return Find(File.ReadAllText(fullPath), roots, Path.GetDirectoryName(fullPath), depth - 1);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return $"@{path}";
            }
        }

        private static bool IsAllowedPath(string value, List<string?> roots, string? baseDirectory)
        {
            string? fullPath = Resolve(value, baseDirectory);
            return fullPath != null && TrustedPathValidator.IsInside(fullPath, roots);
        }

        private static string? Resolve(string value, string? baseDirectory)
        {
            try
            {
                if (Path.IsPathRooted(value))
                    return Path.GetFullPath(value);
                return baseDirectory == null ? null : Path.GetFullPath(Path.Combine(baseDirectory, value));
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return null;
            }
        }
    }
}
