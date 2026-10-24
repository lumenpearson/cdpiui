using System;
using System.IO;

namespace Update;

/// <summary>
/// The log of the updater, update.log next to Update.exe.
/// </summary>
internal static class UpdateLog
{
    public static void Write(string text)
    {
        try
        {
            string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {text}{Environment.NewLine}";
            // In a single-file application, AppContext.BaseDirectory is the folder of Update.exe.
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "update.log"), logLine);
        }
        catch (Exception)
        {
            // The log is optional. A failed write (for example, the file is locked) must not
            // stop the update, and there is no other place to report it.
        }
    }
}
