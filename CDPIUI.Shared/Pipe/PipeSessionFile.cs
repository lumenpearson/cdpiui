using System;
using System.IO;
using System.Text;

namespace CDPIUI.Shared.Pipe;

/// <summary>
/// The session file of the tray: the pipe name and the pipe key of the running tray start. The
/// tray writes it with an access list that lets only the desktop user, administrators and SYSTEM
/// read it. The GUI reads it before each connection.
/// </summary>
public static class PipeSessionFile
{
    public const string DirectoryName = ".session";
    public const string FileName = "pipe.key";

    // Two short lines. A larger file is not a file of the tray.
    private const int MaximumLength = 1024;

    public static string GetPath(string applicationDirectory) =>
        Path.Combine(applicationDirectory, DirectoryName, FileName);

    /// <summary>
    /// Line 1 is the pipe name, line 2 is the key. One file holds both, so a reader never gets
    /// the name of one tray start with the key of another.
    /// </summary>
    public static string Format(PipeSession session) => session.PipeName + "\n" + session.Key + "\n";

    /// <summary>
    /// Returns null for any text that the tray does not write. The one-line format of earlier
    /// versions (the key only) is also rejected: a tray of that version uses the fixed pipe name.
    /// </summary>
    public static PipeSession? Parse(string text)
    {
        string[] lines = text.Trim().Split('\n');
        if (lines.Length != 2)
            return null;

        string pipeName = lines[0].Trim();
        string key = lines[1].Trim();
        return PipeSession.IsValidPipeName(pipeName) && PipeSession.IsValidKey(key)
            ? new PipeSession(pipeName, key)
            : null;
    }

    public static PipeSession? TryRead(string applicationDirectory)
    {
        try
        {
            // Delete sharing lets the tray replace the file while the GUI reads it.
            using var stream = new FileStream(
                GetPath(applicationDirectory), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > MaximumLength)
                return null;
            using var reader = new StreamReader(stream, Encoding.ASCII);
            return Parse(reader.ReadToEnd());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Replaces the session file. <paramref name="createNewFile"/> must create a new file at the
    /// given path (<see cref="FileMode.CreateNew"/>) with the access list of the session file.
    /// </summary>
    public static void Write(string applicationDirectory, PipeSession session, Func<string, Stream> createNewFile)
    {
        string path = GetPath(applicationDirectory);
        string directory = Path.GetDirectoryName(path)!;
        // A random name, so that a file put there earlier cannot block or replace the new file.
        string temporary = Path.Combine(directory, $"{FileName}.{Path.GetRandomFileName()}.tmp");
        Directory.CreateDirectory(directory);
        try
        {
            byte[] bytes = Encoding.ASCII.GetBytes(Format(session));
            using (Stream stream = createNewFile(temporary))
            {
                stream.Write(bytes, 0, bytes.Length);
            }

            // The complete file gets its name in one step, so a reader never sees a part of it.
            // A link or a file of another owner at the path is removed, not written through.
            if (File.Exists(path))
                File.Delete(path);
            File.Move(temporary, path);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Only a temporary file with a random name is left; the next write does not use it.
        }
    }
}
