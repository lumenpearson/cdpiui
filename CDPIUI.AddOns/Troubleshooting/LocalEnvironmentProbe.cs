using System.Diagnostics;

namespace CDPIUI.AddOns.Troubleshooting;

/// <summary>
/// Checks of the local computer: tools on PATH, running processes and writable folders.
/// </summary>
internal static class LocalEnvironmentProbe
{
    public static bool ExistsOnPath(string fileName)
    {
        try
        {
            return GetFullPath(fileName) != null;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsProcessRunning(string procName)
    {
        Process[] processes =
            Process.GetProcessesByName(procName);
        // Each Process holds a handle until it is disposed.
        foreach (var process in processes)
            process.Dispose();
        return processes.Length > 0;
    }

    public static bool IsDirectoryWritable(string dirPath, bool throwIfFails = false)
    {
        try
        {
            using FileStream fs = File.Create(
                Path.Combine(
                    dirPath,
                    Path.GetRandomFileName()
                ),
                1,
                FileOptions.DeleteOnClose);
            return true;
        }
        catch
        {
            if (throwIfFails)
                throw;
            else
                return false;
        }
    }

    private static string? GetFullPath(string fileName)
    {
        if (File.Exists(fileName))
            return Path.GetFullPath(fileName);

        var values = Environment.GetEnvironmentVariable("PATH");
        foreach (var path in values.Split(Path.PathSeparator))
        {
            var fullPath = Path.Combine(path, fileName);
            if (File.Exists(fullPath))
                return fullPath;
        }
        return null;
    }
}
