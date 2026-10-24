using System;
using System.IO;

namespace CDPIUI.Shared.Pipe;

/// <summary>
/// Location of the pipe key. The tray makes a new random key at each start and writes it to a
/// file that only the desktop user, administrators and SYSTEM can read. The GUI reads the key
/// and sends it in Service/RequestAuth.
/// </summary>
public static class PipeSessionKey
{
    public const string DirectoryName = ".session";
    public const string FileName = "pipe.key";

    public static string GetPath(string applicationDirectory) =>
        Path.Combine(applicationDirectory, DirectoryName, FileName);

    public static string? TryRead(string applicationDirectory)
    {
        try
        {
            string path = GetPath(applicationDirectory);
            if (!File.Exists(path))
                return null;
            string key = File.ReadAllText(path).Trim();
            return key.Length > 0 ? key : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
