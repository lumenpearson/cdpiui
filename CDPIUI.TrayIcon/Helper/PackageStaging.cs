using CDPIUI.Shared.Secrets;
using CDPIUI.Shared.Security;
using CDPIUI.TrayIcon.Helper.Basic;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace CDPIUI.TrayIcon.Helper
{
    /// <summary>
    /// Copies packages that the tray installs elevated out of the user's data directory and checks
    /// them there, so the file cannot be replaced between the check and the installation
    /// </summary>
    public static class PackageStaging
    {
        // Under the installation directory, which only administrators can change in a per-machine installation.
        private static string Root => Path.Combine(Utils.GetDataDirectory(), ".staging");

        public static string Stage(string sourcePath)
        {
            string directory = Path.Combine(Root, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string stagedPath = Path.Combine(directory, Path.GetFileName(sourcePath));
            File.Copy(sourcePath, stagedPath);
            return stagedPath;
        }

        public static void Release(string stagedPath)
        {
            try
            {
                string? directory = Path.GetDirectoryName(stagedPath);
                if (directory != null && Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateWarningLog(nameof(PackageStaging), $"Cannot delete {stagedPath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes packages left from earlier runs, for example by an installer that was started without waiting.
        /// </summary>
        public static void CleanUp()
        {
            try
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, true);
            }
            catch { }
        }

        /// <summary>
        /// Checks the signed catalog of every layer of an application update package
        /// </summary>
        public static async Task<bool> VerifyUpdatePackageAsync(string stagedPackage)
        {
            string verifyDirectory = Path.Combine(Path.GetDirectoryName(stagedPackage)!, "verify");
            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(stagedPackage))
                {
                    archive.ExtractToDirectory(verifyDirectory);
                }

                string layersDirectory = Path.Combine(verifyDirectory, "layers");
                List<string> layers = Directory.Exists(layersDirectory)
                    ? Directory.GetDirectories(layersDirectory).ToList()
                    : [Path.Combine(verifyDirectory, "CDPIUI")];

                if (layers.Count == 0 || !layers.All(Directory.Exists))
                    return false;

                foreach (string layer in layers)
                {
                    CatalogVerification result = await CatalogVerifier.VerifyAsync(
                        Path.Combine(layer, "catalog.cat"), layer, Secret.TrustedCertificates);
                    if (result != CatalogVerification.Success)
                    {
                        Logger.Instance.CreateWarningLog(nameof(PackageStaging), $"Update layer {Path.GetFileName(layer)} failed verification: {result}");
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateWarningLog(nameof(PackageStaging), $"Cannot verify the update package: {ex.Message}");
                return false;
            }
            finally
            {
                try { if (Directory.Exists(verifyDirectory)) Directory.Delete(verifyDirectory, true); } catch { }
            }
        }

        /// <summary>
        /// Checks that the file has a valid Authenticode signature that chains to a trusted root
        /// </summary>
        public static bool HasValidSignature(string path)
        {
            var fileInfo = new WINTRUST_FILE_INFO
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_FILE_INFO>(),
                pcwszFilePath = path,
            };
            IntPtr fileInfoPointer = Marshal.AllocHGlobal(Marshal.SizeOf<WINTRUST_FILE_INFO>());
            try
            {
                Marshal.StructureToPtr(fileInfo, fileInfoPointer, false);
                var data = new WINTRUST_DATA
                {
                    cbStruct = (uint)Marshal.SizeOf<WINTRUST_DATA>(),
                    dwUIChoice = WTD_UI_NONE,
                    fdwRevocationChecks = WTD_REVOKE_NONE,
                    dwUnionChoice = WTD_CHOICE_FILE,
                    pFile = fileInfoPointer,
                    dwStateAction = WTD_STATEACTION_IGNORE,
                };

                Guid action = WINTRUST_ACTION_GENERIC_VERIFY_V2;
                int result = WinVerifyTrust(IntPtr.Zero, ref action, ref data);
                if (result != 0)
                    Logger.Instance.CreateWarningLog(nameof(PackageStaging), $"Signature check of {path} failed: 0x{result:X8}");
                return result == 0;
            }
            finally
            {
                Marshal.DestroyStructure<WINTRUST_FILE_INFO>(fileInfoPointer);
                Marshal.FreeHGlobal(fileInfoPointer);
            }
        }

        private static readonly Guid WINTRUST_ACTION_GENERIC_VERIFY_V2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
        private const uint WTD_UI_NONE = 2;
        private const uint WTD_REVOKE_NONE = 0;
        private const uint WTD_CHOICE_FILE = 1;
        private const uint WTD_STATEACTION_IGNORE = 0;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WINTRUST_FILE_INFO
        {
            public uint cbStruct;
            [MarshalAs(UnmanagedType.LPWStr)] public string pcwszFilePath;
            public IntPtr hFile;
            public IntPtr pgKnownSubject;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINTRUST_DATA
        {
            public uint cbStruct;
            public IntPtr pPolicyCallbackData;
            public IntPtr pSIPClientData;
            public uint dwUIChoice;
            public uint fdwRevocationChecks;
            public uint dwUnionChoice;
            public IntPtr pFile;
            public uint dwStateAction;
            public IntPtr hWVTStateData;
            public IntPtr pwszURLReference;
            public uint dwProvFlags;
            public uint dwUIContext;
            public IntPtr pSignatureSettings;
        }

        [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false, CharSet = CharSet.Unicode)]
        private static extern int WinVerifyTrust(IntPtr hwnd, ref Guid pgActionID, ref WINTRUST_DATA pWVTData);
    }
}
