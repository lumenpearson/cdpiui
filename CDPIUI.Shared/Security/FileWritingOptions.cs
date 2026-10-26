using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CDPIUI.Shared.Basic.CommandLine;
using CDPIUI.Shared.Basic.Filesystem;

namespace CDPIUI.Shared.Security;

/// <summary>
/// Finds component options that write a file. The tray starts components with administrator
/// rights, so such an option could otherwise write to any place on the disk.
/// </summary>
public static class FileWritingOptions
{
    // Zapret and Zapret2 (winws, winws2). --debug writes a file only in the form --debug=@file.
    private static readonly string[] Names = { "--debug", "--wf-save", "--hostlist-auto", "--hostlist-auto-debug" };

    // winws parses its options with getopt_long, which also accepts a unique prefix of a long
    // option ("--debu" is "--debug"). These options are prefixes of a name above, but getopt_long
    // takes an exact match first, so they do not mean the option that writes.
    private static readonly HashSet<string> ExactOptionsThatArePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "--hostlist"
    };

    // winws reads more arguments from "@file"; a deeper file is rejected, not skipped.
    private const int MaxArgumentFileDepth = 4;
    private const long MaxArgumentFileBytes = 1 << 20;

    /// <summary>
    /// Returns a description of the first option that writes outside the allowed roots, or null.
    /// </summary>
    /// <param name="commandLine">Arguments of the component</param>
    /// <param name="allowedRoots">Folders in which the component may write</param>
    /// <param name="baseDirectory">Folder for relative paths; null rejects relative paths</param>
    /// <param name="followArgumentFiles">Also check the files given as "@file"</param>
    /// <param name="readArgumentFile">Reads an argument file by its full path; the tray passes a
    /// reader that keeps the file locked until the component exits</param>
    /// <param name="writeTargets">Receives the full path of every file the arguments write</param>
    /// <param name="luaInitValues">Receives the value of every --lua-init option (Lua code, or
    /// @file) with the folder its relative path is resolved against</param>
    public static string? FindOutsideWrite(
        string? commandLine,
        IEnumerable<string?> allowedRoots,
        string? baseDirectory,
        bool followArgumentFiles = false,
        Func<string, string>? readArgumentFile = null,
        ICollection<string>? writeTargets = null,
        ICollection<(string Value, string? BaseDirectory)>? luaInitValues = null)
    {
        var context = new Context(
            new List<string?>(allowedRoots),
            followArgumentFiles,
            readArgumentFile ?? ReadArgumentFile,
            writeTargets,
            luaInitValues);
        return Find(commandLine, context, baseDirectory, MaxArgumentFileDepth);
    }

    private sealed class Context
    {
        public Context(
            List<string?> roots,
            bool followArgumentFiles,
            Func<string, string> readArgumentFile,
            ICollection<string>? writeTargets,
            ICollection<(string Value, string? BaseDirectory)>? luaInitValues)
        {
            Roots = roots;
            FollowArgumentFiles = followArgumentFiles;
            ReadArgumentFile = readArgumentFile;
            WriteTargets = writeTargets;
            LuaInitValues = luaInitValues;
        }

        public List<string?> Roots { get; }
        public bool FollowArgumentFiles { get; }
        public Func<string, string> ReadArgumentFile { get; }
        public ICollection<string>? WriteTargets { get; }
        public ICollection<(string Value, string? BaseDirectory)>? LuaInitValues { get; }
    }

    private static string? Find(string? commandLine, Context context, string? baseDirectory, int depth)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return null;

        // The component splits its command line itself: an MSVC program like CommandLineToArgvW,
        // a Cygwin program (winws) with single quotes as well. Every split is checked, so a
        // quote that one parser reads differently cannot hide an option.
        foreach (var (arguments, isProgramSplit) in Splits(commandLine))
        {
            string? found = FindInArguments(arguments, context, baseDirectory, depth, isProgramSplit);
            if (found != null)
                return found;
        }
        return null;
    }

    // isProgramSplit: a split the way a component program reads its command line; the Lua code
    // it is given is taken from these only, so the code shown for approval is the code it runs.
    private static string? FindInArguments(IReadOnlyList<string> arguments, Context context, string? baseDirectory, int depth, bool isProgramSplit)
    {
        for (int index = 0; index < arguments.Count; index++)
        {
            string argument = arguments[index];
            if (argument.Length > 1 && argument[0] == '@')
            {
                if (!context.FollowArgumentFiles)
                    continue;
                // A file nested deeper than the limit is not read here, so the component would
                // follow it unchecked.
                if (depth == 0)
                    return argument;
                string? nested = CheckArgumentFile(argument.Substring(1).Trim(), context, baseDirectory, depth);
                if (nested != null)
                    return nested;
                continue;
            }

            if (!argument.StartsWith("--", StringComparison.Ordinal))
                continue;

            int equalsIndex = argument.IndexOf('=');
            string name = equalsIndex >= 0 ? argument.Substring(0, equalsIndex) : argument;
            if (isProgramSplit && context.LuaInitValues != null && IsLuaInit(name))
            {
                string code = equalsIndex >= 0
                    ? argument.Substring(equalsIndex + 1)
                    : index + 1 < arguments.Count ? arguments[index + 1] : string.Empty;
                if (code.Trim().Length > 0)
                    context.LuaInitValues.Add((code.Trim(), baseDirectory));
            }
            List<string> meant = MatchingNames(name);
            if (meant.Count == 0)
                continue;

            bool onlyDebug = meant.All(option => option.Equals("--debug", StringComparison.OrdinalIgnoreCase));
            string value;
            if (equalsIndex >= 0)
                value = argument.Substring(equalsIndex + 1);
            else if (onlyDebug)
                value = string.Empty; // --debug takes its value only after "=".
            else
                value = index + 1 < arguments.Count ? arguments[index + 1] : string.Empty; // A required value is the next argument, whatever it starts with.
            value = value.Trim();

            if (onlyDebug)
            {
                // --debug=0|1|syslog|android writes no file; --debug=@file does.
                if (!value.StartsWith("@", StringComparison.Ordinal))
                    continue;
                value = value.Substring(1).Trim();
            }
            if (value.Length == 0)
                continue;

            string? fullPath = Resolve(value, baseDirectory);
            if (fullPath == null || !TrustedPathValidator.IsInside(fullPath, context.Roots))
                return $"{meant[0]}: {value}";
            context.WriteTargets?.Add(fullPath);
        }
        return null;
    }

    // The protected options that getopt_long can read the name as: the exact name, or every
    // protected option the name is a prefix of.
    private static List<string> MatchingNames(string name)
    {
        var meant = new List<string>();
        if (name.Length <= 2)
            return meant;
        foreach (string option in Names)
        {
            if (option.Equals(name, StringComparison.OrdinalIgnoreCase))
                return new List<string> { option };
        }
        if (ExactOptionsThatArePrefixes.Contains(name))
            return meant;
        foreach (string option in Names)
        {
            if (option.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                meant.Add(option);
        }
        return meant;
    }

    private static string? CheckArgumentFile(string path, Context context, string? baseDirectory, int depth)
    {
        string? fullPath = Resolve(path, baseDirectory);
        if (fullPath == null)
            return $"@{path}";
        try
        {
            // A missing file is rejected: it could be created after this check.
            if (!File.Exists(fullPath))
                return $"@{path}";
            return Find(context.ReadArgumentFile(fullPath), context, Path.GetDirectoryName(fullPath), depth - 1);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return $"@{path}";
        }
    }

    private static string ReadArgumentFile(string fullPath)
    {
        var file = new FileInfo(fullPath);
        if (file.Length > MaxArgumentFileBytes)
            throw new InvalidDataException($"The argument file is larger than {MaxArgumentFileBytes} bytes.");
        return File.ReadAllText(fullPath);
    }

    /// <summary>The largest argument file the check reads.</summary>
    public static long MaxArgumentFileSize => MaxArgumentFileBytes;

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

    private static IEnumerable<(IReadOnlyList<string> Arguments, bool IsProgramSplit)> Splits(string commandLine)
    {
        yield return (SplitMsvc(commandLine), true);
        yield return (SplitCygwin(commandLine), true);
        // The splitter the editor and the converters use; its tokens keep their quotes.
        yield return (CommandLineTokens.Tokenize(commandLine).Select(RemoveQuotes).ToList(), false);
    }

    // Zapret2 (winws2) runs the Lua code of --lua-init: inline, or from @file. getopt_long also
    // takes a unique prefix such as --lua-i.
    private static bool IsLuaInit(string name) =>
        name.Length > "--lua-".Length && "--lua-init".StartsWith(name, StringComparison.OrdinalIgnoreCase);

    // The rules of CommandLineToArgvW and the MSVC runtime: 2n backslashes before a quote are n
    // backslashes and a quote that toggles quoting; 2n+1 are n backslashes and a literal quote;
    // "" inside quotes is a literal quote.
    internal static List<string> SplitMsvc(string commandLine)
    {
        var arguments = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;
        bool hasArgument = false;
        int index = 0;
        while (index < commandLine.Length)
        {
            char character = commandLine[index];
            if (!inQuotes && (character == ' ' || character == '\t' || character == '\r' || character == '\n'))
            {
                if (hasArgument)
                {
                    arguments.Add(current.ToString());
                    current.Clear();
                    hasArgument = false;
                }
                index++;
                continue;
            }

            hasArgument = true;
            if (character == '\\')
            {
                int backslashes = 0;
                while (index < commandLine.Length && commandLine[index] == '\\')
                {
                    backslashes++;
                    index++;
                }
                if (index < commandLine.Length && commandLine[index] == '"')
                {
                    current.Append('\\', backslashes / 2);
                    if (backslashes % 2 == 1)
                    {
                        current.Append('"');
                        index++;
                    }
                }
                else
                {
                    current.Append('\\', backslashes);
                }
                continue;
            }

            if (character == '"')
            {
                if (inQuotes && index + 1 < commandLine.Length && commandLine[index + 1] == '"')
                {
                    current.Append('"');
                    index += 2;
                    continue;
                }
                inQuotes = !inQuotes;
                index++;
                continue;
            }

            current.Append(character);
            index++;
        }
        if (hasArgument)
            arguments.Add(current.ToString());
        return arguments;
    }

    // The Cygwin runtime: double and single quotes both group, a backslash escapes a double
    // quote inside double quotes, and quotes are removed.
    internal static List<string> SplitCygwin(string commandLine)
    {
        var arguments = new List<string>();
        var current = new StringBuilder();
        char quote = '\0';
        bool hasArgument = false;
        for (int index = 0; index < commandLine.Length; index++)
        {
            char character = commandLine[index];
            if (quote == '\0' && char.IsWhiteSpace(character))
            {
                if (hasArgument)
                {
                    arguments.Add(current.ToString());
                    current.Clear();
                    hasArgument = false;
                }
                continue;
            }

            hasArgument = true;
            if (quote == '\0' && (character == '"' || character == '\''))
            {
                quote = character;
                continue;
            }
            if (character == quote)
            {
                quote = '\0';
                continue;
            }
            if (quote == '"' && character == '\\' && index + 1 < commandLine.Length && commandLine[index + 1] == '"')
            {
                current.Append('"');
                index++;
                continue;
            }
            current.Append(character);
        }
        if (hasArgument)
            arguments.Add(current.ToString());
        return arguments;
    }

    private static string RemoveQuotes(string token) => token.Replace("\"", string.Empty).Replace("'", string.Empty);
}
