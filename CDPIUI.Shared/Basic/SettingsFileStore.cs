using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

namespace CDPIUI.Shared.Basic;

/// <summary>
/// Reads and writes the settings file that the UI and the tray share. Holds no state: the
/// caller (<see cref="XMLSettingsService"/>) serializes the calls with its lock.
/// </summary>
internal static class SettingsFileStore
{
    private const int LockAttempts = 40;
    private const int LockRetryDelayMs = 50;
    private const int SaveAttempts = 5;
    private const int SaveRetryDelayMs = 50;

    public static bool IsReadFailure(Exception exception) =>
        exception is XmlException or IOException or UnauthorizedAccessException;

    public static XDocument Load(string path)
    {
        // Settings are read by the elevated tray from a file the user can change: no DTD processing,
        // so internal entities cannot blow up the memory of the elevated process.
        var readerSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        XDocument document;
        using (var reader = XmlReader.Create(path, readerSettings))
            document = XDocument.Load(reader);
        if (document.Root?.Name != "Settings")
            throw new XmlException("The settings document must have a Settings root element.");
        return document;
    }

    // A lock file works for both processes, while a named mutex of the elevated tray would not be
    // accessible to the UI.
    public static IDisposable? AcquireLock(string settingsFilePath, out bool timedOut)
    {
        timedOut = false;
        string lockPath = settingsFilePath + ".lock";
        for (int attempt = 1; attempt <= LockAttempts; attempt++)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(settingsFilePath))!);
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None,
                    1, FileOptions.DeleteOnClose);
            }
            catch (IOException)
            {
                Thread.Sleep(LockRetryDelayMs);
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
        timedOut = true;
        return null;
    }

    /// <summary>
    /// Writes <paramref name="document"/> to <paramref name="settingsFilePath"/> through a
    /// temporary file. An I/O error leaves the file as it was.
    /// </summary>
    /// <returns>False if the file cannot be written for lack of rights, so no later save can succeed</returns>
    public static bool Save(XDocument document, string settingsFilePath, bool keepBackup)
    {
        string temporaryPath = settingsFilePath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(settingsFilePath))!);
            document.Save(temporaryPath);

            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    if (File.Exists(settingsFilePath))
                        File.Replace(temporaryPath, settingsFilePath, keepBackup ? settingsFilePath + ".bak" : null);
                    else
                        File.Move(temporaryPath, settingsFilePath);
                    break;
                }
                catch (IOException) when (attempt < SaveAttempts)
                {
                    // The other process (GUI or tray) may be reading the file at this moment.
                    Thread.Sleep(SaveRetryDelayMs * attempt);
                }
            }
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine(ex);
            return false;
        }
        catch (IOException ex)
        {
            Debug.WriteLine(ex);
            return true;
        }
        finally
        {
            try
            { File.Delete(temporaryPath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A left temporary file does no harm; the next save replaces it.
                Trace.WriteLine($"[XMLSettingsService] Cannot delete the temporary settings file: {ex.Message}");
            }
        }
    }

    public static void KeepDamagedCopy(string settingsFilePath)
    {
        try
        {
            File.Copy(settingsFilePath, settingsFilePath + ".corrupt-" + Guid.NewGuid().ToString("N"));
        }
        catch (Exception copyError) when (IsReadFailure(copyError))
        {
            // The copy is kept only for diagnostics; the recovery goes on without it.
            Trace.WriteLine($"[XMLSettingsService] Cannot keep a copy of the damaged settings file: {copyError.Message}");
        }
    }

    public static void WriteRecoveryNotice(string noticePath, string? text)
    {
        try
        { File.WriteAllText(noticePath, text); }
        catch (Exception noticeError) when (IsReadFailure(noticeError))
        {
            Trace.WriteLine($"[XMLSettingsService] Cannot write the recovery notice: {noticeError.Message}");
        }
    }
}
