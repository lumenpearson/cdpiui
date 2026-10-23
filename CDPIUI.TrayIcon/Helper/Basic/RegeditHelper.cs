using CDPIUI.TrayIcon.Helper.Basic;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI.TrayIcon.Helper
{
    public static class RegeditHelper
    {
        public const string InternetSettingsKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
        private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
        private const int INTERNET_OPTION_REFRESH = 37;

        [DllImport("wininet.dll", SetLastError = true)]
        private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

        /// <summary>
        /// The Internet settings of the user at the desktop. The tray may run elevated under
        /// another administrator account, whose HKCU is not the hive the browser reads.
        /// </summary>
        private static RegistryKey? OpenInternetSettings(bool writable)
        {
            var desktopUser = PipeAccessPolicy.GetDesktopUserSid();
            var trayUser = System.Security.Principal.WindowsIdentity.GetCurrent().User;
            if (desktopUser != null && desktopUser != trayUser)
            {
                string path = $@"{desktopUser.Value}\{InternetSettingsKeyPath}";
                RegistryKey? userKey = writable ? Registry.Users.CreateSubKey(path) : Registry.Users.OpenSubKey(path, writable: false);
                if (userKey != null)
                    return userKey;
            }
            return writable
                ? Registry.CurrentUser.CreateSubKey(InternetSettingsKeyPath)
                : Registry.CurrentUser.OpenSubKey(InternetSettingsKeyPath, writable: false);
        }

        public static Dictionary<string, string> ReadProxySettings()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ProxyServer"] = string.Empty,
                ["ProxyOverride"] = string.Empty
            };

            using (var key = OpenInternetSettings(writable: false))
            {
                if (key != null)
                {
                    var proxyServer = key.GetValue("ProxyServer") as string ?? string.Empty;
                    var proxyOverride = key.GetValue("ProxyOverride") as string ?? string.Empty;

                    result["ProxyServer"] = proxyServer;
                    result["ProxyOverride"] = proxyOverride;
                }
            }

            return result;
        }

        public sealed record ProxySnapshot(int? ProxyEnable, string? ProxyServer, string? ProxyOverride);

        public static ProxySnapshot ReadProxySnapshot()
        {
            using var key = OpenInternetSettings(writable: false);
            return new ProxySnapshot(
                key?.GetValue("ProxyEnable") as int?,
                key?.GetValue("ProxyServer") as string,
                key?.GetValue("ProxyOverride") as string);
        }

        public static void RestoreProxySnapshot(ProxySnapshot snapshot)
        {
            using (var key = OpenInternetSettings(writable: true))
            {
                if (key == null)
                    throw new InvalidOperationException("ERR_REGISTRY_WRITE");

                SetOrDelete(key, "ProxyServer", snapshot.ProxyServer);
                SetOrDelete(key, "ProxyOverride", snapshot.ProxyOverride);
                key.SetValue("ProxyEnable", snapshot.ProxyEnable ?? 0, RegistryValueKind.DWord);
            }

            NotifySettingsChanged();
        }

        // The snapshot is also kept on disk while CDPI UI owns the system proxy, so the proxy of
        // the user comes back after a crash, a kill or a shutdown of the tray.
        private static string BackupFilePath =>
            Path.Combine(Utils.GetDataDirectory(), "proxy-backup.json");

        public static void SaveSnapshotBackup(ProxySnapshot snapshot)
        {
            File.WriteAllText(BackupFilePath, System.Text.Json.JsonSerializer.Serialize(snapshot));
        }

        public static void DeleteSnapshotBackup()
        {
            try { File.Delete(BackupFilePath); } catch (IOException) { }
        }

        /// <summary>
        /// Restores the proxy settings left by a tray that did not stop its components.
        /// </summary>
        public static void RestoreLeftoverSnapshot()
        {
            try
            {
                if (!File.Exists(BackupFilePath))
                    return;
                var snapshot = System.Text.Json.JsonSerializer.Deserialize<ProxySnapshot>(File.ReadAllText(BackupFilePath));
                if (snapshot != null)
                    RestoreProxySnapshot(snapshot);
                DeleteSnapshotBackup();
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(RegeditHelper), $"Cannot restore the proxy settings: {ex.Message}");
            }
        }

        private static void SetOrDelete(RegistryKey key, string name, string? value)
        {
            if (value == null)
                key.DeleteValue(name, throwOnMissingValue: false);
            else
                key.SetValue(name, value, RegistryValueKind.String);
        }

        private static void NotifySettingsChanged()
        {
            if (!InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "ERR_WININET_CALL");

            if (!InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "ERR_WININET_CALL");
        }

        public static void SaveProxySettings(string proxyServer, string proxyOverride, int proxyEnable)
        {
            if (proxyEnable != 0 && proxyEnable != 1)
                throw new ArgumentOutOfRangeException(nameof(proxyEnable), "ERR_REGEDIT_HELPER_INTERNAL");

            using (var key = OpenInternetSettings(writable: true))
            {
                if (key == null)
                    throw new InvalidOperationException("ERR_REGISTRY_WRITE");

                if (!string.IsNullOrEmpty(proxyServer))
                    key.SetValue("ProxyServer", proxyServer ?? string.Empty, RegistryValueKind.String);

                if (!string.IsNullOrEmpty(proxyOverride))
                    key.SetValue("ProxyOverride", proxyOverride ?? string.Empty, RegistryValueKind.String);

                key.SetValue("ProxyEnable", proxyEnable, RegistryValueKind.DWord);
            }

            NotifySettingsChanged();
        }
    }
}
