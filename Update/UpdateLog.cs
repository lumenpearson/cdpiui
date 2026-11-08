using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Update;

/// <summary>
/// The log of the updater, update.log next to Update.exe.
/// </summary>
internal static class UpdateLog
{
    public static void Write(string text)
        => WriteTo(Path.Combine(AppContext.BaseDirectory, "update.log"), text);

    internal static void WriteTo(string path, string text)
    {
        try
        {
            string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {text}{Environment.NewLine}";
            // In a single-file application, AppContext.BaseDirectory is the folder of Update.exe.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var lease = WritablePathLease.Open(path);
                lease.Stream.Seek(0, SeekOrigin.End);
                using var writer = new StreamWriter(lease.Stream, new UTF8Encoding(false), 1024, leaveOpen: true);
                writer.Write(logLine);
            }
            else File.AppendAllText(path, logLine);
        }
        catch (Exception)
        {
            // The log is optional. A failed write (for example, the file is locked) must not
            // stop the update, and there is no other place to report it.
        }
    }
}
