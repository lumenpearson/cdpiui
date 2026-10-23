using CDPIUI.Shared;
using CDPIUI.Shared.Pipe.Models;
using CDPIUI.TrayIcon.Helper.Basic;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI.TrayIcon.Helper
{
    public class Utils
    {
        public static Assembly Assembly = Assembly.GetExecutingAssembly();

        public static string GetDataDirectory()
        {
            try
            {
                var procPath = Environment.ProcessPath;
                return Path.GetDirectoryName(procPath)!;
            }
            catch
            {
                return "";
            }
        }

        // The MSI writes its install folder here (NewSetup/Product.wxs); the GUI uses the same check.
        private static bool IsInstalledByMsi()
        {
            try
            {
                using var key = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64)
                    .OpenSubKey(@"Software\Storik4\CDPI UI");
                return key?.GetValue("InstallLocation") is string location &&
                    !string.IsNullOrWhiteSpace(location) &&
                    string.Equals(Path.GetFullPath(location).TrimEnd(Path.DirectorySeparatorChar),
                        Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar),
                        StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string GetSettingsFile()
        {
            string filePath = Path.Combine(GetDataDirectory(), "Settings", "Settings.xml");

            if (File.Exists(filePath))
            {
                return filePath;
            }
            else
            {
                // The UI keeps its settings in the profile of the desktop user, which differs from
                // the tray profile when the tray was elevated with another administrator account.
                string? desktopUserFolder = TrustedPaths.GetDesktopUserDataDirectory();
                if (desktopUserFolder != null)
                {
                    string desktopUserFile = Path.Combine(desktopUserFolder, "Settings", "Settings.xml");
                    if (File.Exists(desktopUserFile))
                        return desktopUserFile;
                }

                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var targetFolder = Path.Combine(localAppData, "CDPIUI");
                string localAppDataFile = Path.Combine(targetFolder, "Settings", "Settings.xml");

                if (File.Exists(localAppDataFile))
                {
                    return localAppDataFile;
                }

                // No settings yet. An MSI installation keeps the GUI settings in the profile of the
                // desktop user; a file created next to the tray would later win over them.
                if (desktopUserFolder != null && IsInstalledByMsi())
                    return Path.Combine(desktopUserFolder, "Settings", "Settings.xml");

                return filePath;
            }
        }

        public static async void StartUpdate(string targetFile)
        {
            if (!TrustedPaths.IsAllowed(targetFile, "application update"))
            {
                NotifyHelper.ShowMessage("CDPI UI", LocaleHelper.GetLocaleString("UpdateFailure"), "UPDATE:OPEN_LOG");
                return;
            }

            // The installer runs from a copy the user cannot change, checked where possible.
            string stagedFile;
            try
            {
                stagedFile = PackageStaging.Stage(targetFile);
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(Utils), $"Cannot copy the update package: {ex.Message}");
                NotifyHelper.ShowMessage("CDPI UI", LocaleHelper.GetLocaleString("UpdateFailure"), "UPDATE:OPEN_LOG");
                return;
            }

            if (string.Equals(Path.GetExtension(stagedFile), ".msi", StringComparison.OrdinalIgnoreCase))
            {
                // Published Setup.msi files are not Authenticode-signed, so there is no signature to check yet.
                RunHelper.Run(Path.Combine(Environment.SystemDirectory, "msiexec.exe"), $"/i \"{stagedFile}\" /qn+");
                _ = PipeHelper.SendApplicationPacket(ApplicationMessageIds.CloseApplicationUI);
                NotifyHelper.Instance.Dispose();
                Application.Exit();
            }
            else
            {
                if (!await PackageStaging.VerifyUpdatePackageAsync(stagedFile))
                {
                    PackageStaging.Release(stagedFile);
                    NotifyHelper.ShowMessage("CDPI UI", LocaleHelper.GetLocaleString("UpdateFailure"), "UPDATE:OPEN_LOG");
                    return;
                }
                RunHelper.Run(Path.Combine(GetDataDirectory(), "Update.exe"), $"--directory-to-zip \"{stagedFile}\" --destination-directory \"{GetDataDirectory()}\"");
            }
        }

        public static Bitmap? GetBitmapFromResourses(string resourseKey)
        {
            var resource = Utils.Assembly.GetManifestResourceStream(resourseKey);
            if (resource != null)
            {
                return new Bitmap(resource);
            }
            return null;
        }

        public static float GetScalingFactorForMainDisplay()
        {
            var currentDPI = Int32.Parse((string?)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ThemeManager", "LastLoadedDPI", "96") ?? "96");
            var scale = 96 / (float?)currentDPI ?? 96;
            return scale;
        }

        public static async void GrantAccess(string file, bool conptySignal)
        {
            if (!TrustedPaths.IsInStore(file, "access change"))
            {
                if (conptySignal) await PipeHelper.SendGrantAccessPacket(false);
                return;
            }

            try
            {
                bool exists = System.IO.Directory.Exists(file);
                if (!exists)
                {
                    DirectoryInfo di = System.IO.Directory.CreateDirectory(file);
                }
                DirectoryInfo dInfo = new DirectoryInfo(file);
                DirectorySecurity dSecurity = dInfo.GetAccessControl();
                // Only the user the tray runs for gets write access: these directories hold
                // the executables that the tray starts elevated.
                SecurityIdentifier user = WindowsIdentity.GetCurrent().User
                    ?? throw new InvalidOperationException("Cannot get the current user.");
                dSecurity.AddAccessRule(
                    new FileSystemAccessRule(
                        user,
                        FileSystemRights.Modify,
                        InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit,
                        PropagationFlags.None,
                        AccessControlType.Allow
                        )
                    );
                dInfo.SetAccessControl(dSecurity);
                if (conptySignal) await PipeHelper.SendGrantAccessPacket(true);
                return;
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(Utils), $"Cannot grant access for \"{file}\". Exception message: {ex.Message}");
            }

            if (conptySignal) await PipeHelper.SendGrantAccessPacket(false);
        }
    }
}
