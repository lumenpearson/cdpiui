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
    /// Checks that a path is inside one of the roots and that no component of its full path,
    /// including the root and its parents, is a symbolic link or a junction
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

            return !HasLinkInPath(fullPath);
        }

        return false;
    }

    private static bool HasLinkInPath(string fullPath)
    {
        string current = Path.GetPathRoot(fullPath)!;
        try
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                return true;
        }
        catch { return true; }

        string[] parts = fullPath.Substring(current.Length)
            .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

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
