using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using CDPIUI.Shared.Security;

namespace CDPIUI.Shared.Basic;

/// <summary>Windows settings I/O. The caller holds the shared settings lock for writes.</summary>
internal static class WindowsSettingsFiles
{
    public static void Save(XDocument document, string path, bool keepBackup, int attempts, int retryDelayMs)
    {
        path = Path.GetFullPath(path);
        string backupPath = path + ".bak";
        WritablePathLease? temporary = null;
        WritablePathLease? backup = null;
        try
        {
            using (var original = OpenExisting(path))
            {
                byte[]? access = original == null ? null : FileAccessDescriptor.Read(original.Stream!.SafeFileHandle);
                temporary = WritablePathLease.CreateForMove(path + ".tmp-" + Guid.NewGuid().ToString("N"), access);
                document.Save(temporary.Stream);
                temporary.Stream.Flush(flushToDisk: true);
                if (original != null && keepBackup)
                {
                    backup = WritablePathLease.CreateForMove(backupPath + ".tmp-" + Guid.NewGuid().ToString("N"), access);
                    original.Stream!.CopyTo(backup.Stream);
                }
            }
            // Publish after closing our read-only lease. The new temporary and the
            // settings lock still pin every parent. A target alias is replaced as a name,
            // never opened for writes; the settings name never has an absent window.
            if (backup != null) Publish(backup, backupPath, attempts, retryDelayMs);
            Publish(temporary, path, attempts, retryDelayMs);
        }
        finally
        {
            ReleaseTemporary(backup, backupPath);
            ReleaseTemporary(temporary, path);
        }
    }

    private static ReadOnlyPathLease? OpenExisting(string path)
    {
        try { return ReadOnlyPathLease.OpenFile(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    private static void Publish(WritablePathLease file, string destination, int attempts, int retryDelayMs)
    {
        for (int attempt = 1; ; attempt++)
        {
            try { file.MoveTo(destination, replace: true); return; }
            catch (IOException) when (attempt < attempts) { Thread.Sleep(retryDelayMs * attempt); }
        }
    }

    private static void ReleaseTemporary(WritablePathLease? file, string publishedPath)
    {
        if (file == null) return;
        try
        {
            if (!string.Equals(file.CurrentPath, publishedPath, StringComparison.OrdinalIgnoreCase)) file.DeleteOnClose();
        }
        catch (Exception ex) when (SettingsFileStore.IsReadFailure(ex)) { Trace.WriteLine(ex); }
        finally
        {
            try { file.Dispose(); }
            catch (Exception ex) when (SettingsFileStore.IsReadFailure(ex)) { Trace.WriteLine(ex); }
        }
    }

    public static void KeepDamagedCopy(string path)
    {
        using var source = ReadOnlyPathLease.OpenFile(path);
        using var copy = WritablePathLease.CreateForMove(path + ".corrupt-" + Guid.NewGuid().ToString("N"), FileAccessDescriptor.Read(source.Stream!.SafeFileHandle));
        try { source.Stream!.CopyTo(copy.Stream); copy.Stream.Flush(flushToDisk: true); }
        catch { copy.DeleteOnClose(); throw; }
    }

    public static void WriteNotice(string path, string? text)
    {
        using var target = WritablePathLease.Open(path);
        target.Stream.SetLength(0);
        using var writer = new StreamWriter(target.Stream, new UTF8Encoding(false), 1024, leaveOpen: true);
        writer.Write(text);
    }
}
