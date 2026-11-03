using System.Security.AccessControl;
using System.Security.Principal;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.Shared.Security;
using CDPIUI.TrayIcon.Helper.Basic;
using Microsoft.Win32;

namespace CDPIUI.TrayIcon.Helper;

/// <summary>
/// Limits the files that the UI may ask the elevated tray to run, install or change access to
/// </summary>
public static class TrustedPaths
{
    // The installation directory and the data directory of MSI builds for the tray user and,
    // when the tray was elevated with another account, for the desktop user that runs the UI.
    private static IEnumerable<string?> Roots =>
    [
        Utils.GetDataDirectory(),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CDPIUI"),
        GetDesktopUserDataDirectory(),
    ];

    public static IEnumerable<string?> AllowedRoots => Roots;

    /// <summary>
    /// A portable executable and its directory are user-controlled. For either installation
    /// type, do not expose elevated operations when another account can change the application.
    /// </summary>
    public static bool HasSafeElevationContext()
    {
        bool installedByMsi = Utils.IsInstalledByMsi();
        string applicationDirectory = Utils.GetDataDirectory();
        if (installedByMsi)
        {
            try
            {
                string? trayUserSid = WindowsIdentity.GetCurrent().User?.Value;
                return ProtectedInstallLocationPolicy.IsProtected(applicationDirectory, trayUserSid);
            }
            catch
            {
                return false;
            }
        }

        if (!TrustedPathValidator.IsInside(applicationDirectory + Path.DirectorySeparatorChar, [applicationDirectory]))
            return false;

        try
        {
            string? trayUserSid = WindowsIdentity.GetCurrent().User?.Value;
            string? desktopUserSid = PipeAccessPolicy.GetDesktopUserSid()?.Value;
            string? directoryOwnerSid = new DirectoryInfo(applicationDirectory)
                .GetAccessControl(AccessControlSections.Owner)
                .GetOwner(typeof(SecurityIdentifier)) is SecurityIdentifier owner
                    ? owner.Value
                    : null;

            return PortableElevationPolicy.IsAllowed(
                installedByMsi, trayUserSid, desktopUserSid, directoryOwnerSid);
        }
        catch
        {
            return false;
        }
    }

    public static string? GetDesktopUserDataDirectory()
    {
        try
        {
            var sid = PipeAccessPolicy.GetDesktopUserSid();
            if (sid == null)
                return null;

            using RegistryKey? profile = Registry.LocalMachine.OpenSubKey(
                $@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\{sid.Value}");
            if (profile?.GetValue("ProfileImagePath") is not string profilePath)
                return null;

            return Path.Combine(Environment.ExpandEnvironmentVariables(profilePath), "AppData", "Local", "CDPIUI");
        }
        catch
        {
            return null;
        }
    }

    public static bool IsAllowed(string? path, string operation)
    {
        if (TrustedPathValidator.IsInside(path, Roots))
            return true;

        Logger.Instance.CreateWarningLog(nameof(TrustedPaths), $"Rejected {operation} for \"{path}\": the path is outside the application directories");
        return false;
    }

    public static bool IsInStore(string? path, string operation)
    {
        // In an MSI installation only administrators can change the installation folder, and
        // ExecutableTrust starts programs from it without a check; access to a folder in it
        // must not be handed to the user. A portable copy keeps its Store next to the tray.
        IEnumerable<string?> roots = Utils.IsInstalledByMsi()
            ? Roots.Where(root => root != null && !IsSameDirectory(root, Utils.GetDataDirectory()))
            : Roots;
        if (TrustedPathValidator.IsInside(path, roots.Select(root => root == null ? null : Path.Combine(root, "Store"))))
            return true;

        Logger.Instance.CreateWarningLog(nameof(TrustedPaths), $"Rejected {operation} for \"{path}\": the path is outside the store directory");
        return false;
    }

    private static bool IsSameDirectory(string first, string second)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
