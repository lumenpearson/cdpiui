using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.TrayIcon.Helper.Basic;

namespace CDPIUI.TrayIcon.Helper
{
    /// <summary>
    /// Limits the files that the UI may ask the elevated tray to run, install or change access to
    /// </summary>
    public static class TrustedPaths
    {
        // The installation directory and the data directory of MSI builds for the tray user.
        private static IEnumerable<string> Roots =>
        [
            Utils.GetDataDirectory(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CDPIUI"),
        ];

        public static bool IsAllowed(string? path, string operation)
        {
            if (TrustedPathValidator.IsInside(path, Roots))
                return true;

            Logger.Instance.CreateWarningLog(nameof(TrustedPaths), $"Rejected {operation} for \"{path}\": the path is outside the application directories");
            return false;
        }

        public static bool IsInStore(string? path, string operation)
        {
            if (TrustedPathValidator.IsInside(path, Roots.Select(root => Path.Combine(root, "Store"))))
                return true;

            Logger.Instance.CreateWarningLog(nameof(TrustedPaths), $"Rejected {operation} for \"{path}\": the path is outside the store directory");
            return false;
        }
    }
}
