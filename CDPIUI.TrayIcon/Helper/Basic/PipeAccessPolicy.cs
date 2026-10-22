using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace CDPIUI.TrayIcon.Helper.Basic
{
    public static class PipeAccessPolicy
    {
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const uint TOKEN_QUERY = 0x0008;

        public static string ExpectedClientPath => Path.Combine(Utils.GetDataDirectory(), "CDPIUI.exe");

        public static PipeSecurity CreatePipeSecurity()
        {
            var security = new PipeSecurity();
            var trayUser = WindowsIdentity.GetCurrent().User
                ?? throw new InvalidOperationException("Cannot determine the tray process user.");

            security.SetOwner(trayUser);
            security.AddAccessRule(new PipeAccessRule(trayUser, PipeAccessRights.FullControl, AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));

            // With over-the-shoulder elevation the tray runs as an administrator account while the UI runs as the desktop user.
            var desktopUser = GetDesktopUserSid();
            if (desktopUser != null && desktopUser != trayUser)
                security.AddAccessRule(new PipeAccessRule(desktopUser, PipeAccessRights.ReadWrite, AccessControlType.Allow));

            security.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));

            return security;
        }

        public static SecurityIdentifier? GetDesktopUserSid()
        {
            IntPtr shellWindow = GetShellWindow();
            if (shellWindow == IntPtr.Zero || GetWindowThreadProcessId(shellWindow, out uint shellProcessId) == 0)
                return null;

            IntPtr process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, shellProcessId);
            if (process == IntPtr.Zero)
                return null;

            IntPtr token = IntPtr.Zero;
            try
            {
                if (!OpenProcessToken(process, TOKEN_QUERY, out token))
                    return null;

                using var identity = new WindowsIdentity(token);
                return identity.User;
            }
            finally
            {
                if (token != IntPtr.Zero) CloseHandle(token);
                CloseHandle(process);
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
