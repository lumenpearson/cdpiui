using System;
using System.IO;

#if UPDATE_TOOL
namespace Update;
#else
namespace CDPIUI.Shared.Security;
#endif

/// <summary>Rejects names whose Win32 interpretation differs from the checked path.</summary>
internal static class WindowsPathNames
{
    public static bool IsOrdinaryName(string name)
    {
        if (name.Length == 0 || name != name.TrimEnd(' ', '.')) return false;
        foreach (char c in name)
            if (c < 32 || "<>:\"|?*\\/".IndexOf(c) >= 0) return false;
        string stem = name.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
        if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL") return false;
        if (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
            "0123456789\u00b9\u00b2\u00b3".IndexOf(stem[3]) >= 0) return false;
        return true;
    }

    public static void RequireOrdinaryAbsolutePath(string path)
    {
        if (path.StartsWith(@"\\.\", StringComparison.Ordinal) || path.StartsWith(@"\\?\", StringComparison.Ordinal))
            throw new ArgumentException("Device paths are not supported.", nameof(path));
        string root = Path.GetPathRoot(path) ?? throw new ArgumentException("An absolute path is required.", nameof(path));
        foreach (string name in path.Substring(root.Length).Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries))
            if (!IsOrdinaryName(name)) throw new ArgumentException("An ordinary Windows path is required.", nameof(path));
    }
}
