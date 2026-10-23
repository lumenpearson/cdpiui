using Microsoft.Win32;
using System;
using System.IO;

namespace CDPIUI.Core
{
    public static class State
    {
#if SINGLEFILE
        public static bool IsApplicationBuildAsSingleFile => true;
        public static bool IsApplicationBuildAsMsi => false;
#elif MSIFILE
        public static bool IsApplicationBuildAsSingleFile => false;
        public static bool IsApplicationBuildAsMsi => true;
#else
        // A Release build is packed both as the MSI and as the portable ZIP, so the kind of
        // installation is found at run time: the MSI writes its install folder to the registry.
        // "#elif Release" never matched (the symbol is RELEASE), and such builds looked for MSI updates.
        public static bool IsApplicationBuildAsSingleFile => !InstalledByMsi.Value;
        public static bool IsApplicationBuildAsMsi => InstalledByMsi.Value;

        private static readonly Lazy<bool> InstalledByMsi = new(DetectMsiInstallation);

        private static bool DetectMsiInstallation()
        {
            try
            {
                using RegistryKey? key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                    .OpenSubKey(@"Software\Storik4\CDPI UI");
                if (key?.GetValue("InstallLocation") is not string location || string.IsNullOrWhiteSpace(location))
                    return false;
                return string.Equals(
                    Path.GetFullPath(location).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }
#endif
    }
}
