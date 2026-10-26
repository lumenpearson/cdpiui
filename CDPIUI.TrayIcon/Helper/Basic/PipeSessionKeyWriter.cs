using CDPIUI.Shared.Pipe;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace CDPIUI.TrayIcon.Helper.Basic
{
    /// <summary>
    /// Makes the pipe key of this tray start. A build-wide secret was the same in every copy of the
    /// application and could be read from its files; this key is new at each start and only the
    /// desktop user can read it.
    /// </summary>
    public static class PipeSessionKeyWriter
    {
        public static string Key { get; private set; } = string.Empty;

        public static void Create()
        {
            string path = PipeSessionKey.GetPath(Utils.GetDataDirectory());
            string directory = Path.GetDirectoryName(path)!;
            string key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            try
            {
                Directory.CreateDirectory(directory);
                // A link or a file of another owner is not reused: the key is written to a new file.
                if (File.Exists(path))
                    File.Delete(path);

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

                using (FileStream stream = new FileInfo(path).Create(
                    FileMode.CreateNew, FileSystemRights.Write, FileShare.None, 4096, FileOptions.None, security))
                {
                    byte[] bytes = Encoding.ASCII.GetBytes(key);
                    stream.Write(bytes, 0, bytes.Length);
                }
                Key = key;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Without the key no client is authorized; an empty key never matches.
                Key = string.Empty;
                Logger.Instance.CreateErrorLog(nameof(PipeSessionKeyWriter), $"Cannot write the pipe key: {exception.Message}");
            }
        }
    }
}
