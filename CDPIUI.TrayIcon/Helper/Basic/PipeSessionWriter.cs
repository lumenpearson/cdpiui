using System.Security.AccessControl;
using System.Security.Principal;
using CDPIUI.Shared.Pipe;

namespace CDPIUI.TrayIcon.Helper.Basic;

/// <summary>
/// Writes the pipe name and the pipe key of this tray start to the session file. A build-wide
/// secret was the same in every copy of the application and could be read from its files; this
/// key is new at each start and only the desktop user can read it.
/// </summary>
public static class PipeSessionWriter
{
    public static string Key { get; } = PipeSession.CreateKey();

    /// <summary>
    /// Removes the session file of an earlier start, so that the GUI does not use the pipe name of
    /// a tray that no longer runs while this start creates its pipe.
    /// </summary>
    public static void RemoveEarlierSession()
    {
        string path = PipeSessionFile.GetPath(Utils.GetDataDirectory());
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Logger.Instance.CreateWarningLog(nameof(PipeSessionWriter), $"Cannot remove the pipe session of an earlier start: {exception.Message}");
        }
    }

    /// <summary>
    /// Publishes <paramref name="pipeName"/> with the key of this start. Throws
    /// <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/> if the file cannot
    /// be written; then no client can find the pipe.
    /// </summary>
    public static void Publish(string pipeName) =>
        PipeSessionFile.Write(Utils.GetDataDirectory(), new PipeSession(pipeName, Key), CreateProtectedFile);

    private static Stream CreateProtectedFile(string path)
    {
        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        var trayUser = WindowsIdentity.GetCurrent().User!;
        security.SetOwner(trayUser);
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(trayUser, FileSystemRights.FullControl, AccessControlType.Allow));
        SecurityIdentifier? desktopUser = PipeAccessPolicy.GetDesktopUserSid();
        if (desktopUser != null && desktopUser != trayUser)
            security.AddAccessRule(new FileSystemAccessRule(desktopUser, FileSystemRights.Read | FileSystemRights.Synchronize, AccessControlType.Allow));

        return new FileInfo(path).Create(
            FileMode.CreateNew, FileSystemRights.Write, FileShare.None, 4096, FileOptions.None, security);
    }
}
