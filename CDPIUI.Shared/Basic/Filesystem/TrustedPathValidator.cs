using System;
using System.Collections.Generic;
using System.IO;

namespace CDPIUI.Shared.Basic.Filesystem;

/// <summary>
/// Checks paths that an elevated process receives from a less trusted one
/// </summary>
public static class TrustedPathValidator
{
    /// <summary>
    /// Checks that a path is inside one of the roots and that no directory between the root
    /// and the path, and not the path itself, is a symbolic link or a junction
    /// </summary>
    /// <param name="path">Path to check</param>
    /// <param name="roots">Allowed root directories</param>
    public static bool IsInside(string? path, IEnumerable<string?> roots)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        string fullPath;
        try
        { fullPath = Path.GetFullPath(path); }
        catch { return false; }

        foreach (string? root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
                continue;

            string fullRoot;
            try
            { fullRoot = WithSeparator(Path.GetFullPath(root)); }
            catch { continue; }

            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                continue;

            return !HasLinkBelowRoot(fullRoot, fullPath);
        }

        return false;
    }

    private static bool HasLinkBelowRoot(string fullRoot, string fullPath)
    {
        string current = fullRoot.TrimEnd(Path.DirectorySeparatorChar);
        string[] parts = fullPath.Substring(fullRoot.Length)
            .Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string part in parts)
        {
            current = Path.Combine(current, part);
            FileAttributes attributes;
            try
            { attributes = File.GetAttributes(current); }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
            catch { return true; }

            if ((attributes & FileAttributes.ReparsePoint) != 0)
                return true;
        }
        return false;
    }

    private static string WithSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? path
            : path + Path.DirectorySeparatorChar;
}
