using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.Shared.Secrets;
using CDPIUI.Shared.Security;
using CDPIUI.TrayIcon.Helper.Basic;

namespace CDPIUI.TrayIcon.Helper;

/// <summary>
/// Copies packages that the tray installs elevated out of the user's data directory and checks
/// them there, so the file cannot be replaced between the check and the installation
/// </summary>
public static class PackageStaging
{
    private static string Root => ProtectedStagingArea.DefaultRoot;

    public static string Stage(string sourcePath)
    {
        return ProtectedStagingArea.Stage(Root, sourcePath);
    }

    /// <summary>
    /// Checks the staged copy against the source digest supplied by the GUI.
    /// </summary>
    public static bool MatchesSha256(string path, string? expectedSha256) =>
        Sha256File.Matches(path, expectedSha256);

    public static void Release(string stagedPath)
    {
        try
        {
            ProtectedStagingArea.Release(Root, stagedPath);
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateWarningLog(nameof(PackageStaging), $"Cannot delete {stagedPath}: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates the persistent cache. Orphan packages need an explicit consumer-lifetime cleanup policy.
    /// </summary>
    public static void CleanUp()
    {
        try
        {
            // The old tray may have exited during MSI installation. Its absence does not
            // prove that an installer stopped using the package. Do not delete orphan packages
            // without a consumer-lifetime protocol (audit FS-12).
            if (Directory.Exists(Root))
                ProtectedStagingArea.RequireProtectedPath(Root);
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateWarningLog(nameof(PackageStaging), $"Cannot validate the package cache: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks the signed catalog of every layer of an application update package
    /// </summary>
    public static async Task<bool> VerifyUpdatePackageAsync(string stagedPackage)
    {
        string verifyDirectory = Path.Combine(Path.GetDirectoryName(stagedPackage)!, "verify");
        try
        {
            // The catalog can only be checked on extracted files, so the archive is limited first:
            // an unverified package must not fill the disk of the installation volume.
            List<string> layerPrefixes;
            using (ZipArchive archive = ZipFile.OpenRead(stagedPackage))
            {
                ArchivePaths.EnsureWithinLimits(archive.Entries);
                // Update.exe installs exactly these folders (the same rule is compiled into it),
                // so every file it installs is in a layer whose catalog is checked below.
                layerPrefixes = UpdatePackageLayers.GetPrefixes(archive.Entries.Select(entry => entry.FullName));
                ProtectedStagingArea.CreatePrivateDirectory(verifyDirectory);
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string destination = ArchivePaths.GetDestinationOrThrow(verifyDirectory, entry.FullName);
                    ArchivePaths.EnsureNoReparsePoints(verifyDirectory, destination);
                    if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
                    {
                        ProtectedStagingArea.CreateVerificationParents(verifyDirectory, destination);
                        continue;
                    }
                    ProtectedStagingArea.CreateVerificationParents(verifyDirectory, Path.GetDirectoryName(destination)!);
                    using FileStream output = ProtectedStagingArea.CreatePrivateFile(destination);
                    ArchivePaths.CopyEntryBounded(entry, output);
                }
            }

            List<string> layers = layerPrefixes
                .Select(prefix => Path.Combine(verifyDirectory, prefix.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar)))
                .ToList();

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
            try
            { if (Directory.Exists(verifyDirectory)) Directory.Delete(verifyDirectory, true); }
            catch (Exception ex)
            {
                Logger.Instance.CreateWarningLog(nameof(PackageStaging), $"Cannot delete the verification folder: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Checks that the file has a valid Authenticode signature that chains to a trusted root
    /// </summary>
    public static bool HasValidSignature(string path)
    {
        int result = VerifyTrust(path);
        if (result != 0)
            Logger.Instance.CreateWarningLog(nameof(PackageStaging), $"Signature check of {path} failed: 0x{result:X8}");
        return result == 0;
    }

    /// <summary>
    /// Checks that the file is signed by one of the pinned certificates of the application
    /// (Secret.TrustedCertificates). Those certificates are self-signed, so a chain to an
    /// untrusted root is accepted; a changed file or another signer is not.
    /// </summary>
    public static bool HasPinnedSignature(string path, IEnumerable<string> trustedThumbprints)
    {
        int result = VerifyTrust(path);
        if (result != 0 && result != CERT_E_UNTRUSTEDROOT && result != CERT_E_CHAINING)
        {
            Logger.Instance.CreateWarningLog(nameof(PackageStaging), $"Signature check of {path} failed: 0x{result:X8}");
            return false;
        }

        string? thumbprint;
        try
        {
#pragma warning disable SYSLIB0057 // The signature itself was checked by WinVerifyTrust above.
            using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
            thumbprint = certificate.Thumbprint;
        }
        catch (CryptographicException exception)
        {
            Logger.Instance.CreateWarningLog(nameof(PackageStaging), $"Cannot read the signer of {path}: {exception.Message}");
            return false;
        }

        bool pinned = trustedThumbprints.Any(trusted =>
            string.Equals(new string(trusted.Where(Uri.IsHexDigit).ToArray()), thumbprint, StringComparison.OrdinalIgnoreCase));
        if (!pinned)
            Logger.Instance.CreateWarningLog(nameof(PackageStaging), $"{path} is signed by an unknown certificate {thumbprint}");
        return pinned;
    }

    /// <summary>
    /// The signer of the file when its Authenticode signature is valid and chains to a trusted
    /// root, otherwise null: the name in a changed or self-signed file proves nothing.
    /// </summary>
    public static string? GetVerifiedSigner(string path)
    {
        if (VerifyTrust(path) != 0)
            return null;
        try
        {
#pragma warning disable SYSLIB0057 // The signature itself was checked by WinVerifyTrust above.
            using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
            return certificate.GetNameInfo(X509NameType.SimpleName, false);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static int VerifyTrust(string path)
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
            return WinVerifyTrust(IntPtr.Zero, ref action, ref data);
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
    // The signature and the file hash are valid, but the certificate chain ends in an untrusted root.
    private const int CERT_E_UNTRUSTEDROOT = unchecked((int)0x800B0109);
    private const int CERT_E_CHAINING = unchecked((int)0x800B010A);

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
