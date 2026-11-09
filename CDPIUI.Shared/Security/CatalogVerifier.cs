using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CDPIUI.Shared.Security;

public enum CatalogVerification
{
    Success,
    NoSignature,
    NotTrustedSignature,
    FilesDoNotMatch,
    Failure
}

/// <summary>
/// Verifies signed file catalogs (catalog.cat) of packages and application updates
/// </summary>
public static class CatalogVerifier
{
    /// <summary>
    /// Checks the catalog signature and that the files of the directory match the catalog
    /// </summary>
    /// <param name="catalogFile">Catalog file</param>
    /// <param name="directory">Directory the catalog describes</param>
    /// <param name="trustedThumbprints">SHA-1 thumbprints of the certificates allowed to sign catalogs</param>
    /// <param name="filesToSkip">File names that are not compared, in addition to catalog.cat</param>
    public static Task<CatalogVerification> VerifyAsync(
        string catalogFile,
        string directory,
        IEnumerable<string> trustedThumbprints,
        IEnumerable<string>? filesToSkip = null) => VerifyInternalAsync(catalogFile, directory, trustedThumbprints, filesToSkip, null);

    /// <summary>Verifies exactly the held file set that the caller will subsequently consume.</summary>
    public static Task<CatalogVerification> VerifySnapshotAsync(string catalogFile, ReadOnlyDirectorySnapshot snapshot,
        IEnumerable<string> trustedThumbprints, IEnumerable<string>? filesToSkip = null) =>
        VerifyInternalAsync(catalogFile, snapshot.RootPath, trustedThumbprints, filesToSkip, snapshot);

    private static async Task<CatalogVerification> VerifyInternalAsync(string catalogFile, string directory,
        IEnumerable<string> trustedThumbprints, IEnumerable<string>? filesToSkip, ReadOnlyDirectorySnapshot? suppliedSnapshot)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return CatalogVerification.Failure;
        ReadOnlyPathLease catalog;
        try { catalog = ReadOnlyPathLease.OpenFile(catalogFile); }
        catch (FileNotFoundException) { return CatalogVerification.NoSignature; }
        catch (DirectoryNotFoundException) { return CatalogVerification.NoSignature; }
        using (catalog)
        {
            // Bound CMS allocation independently from the 1 GiB package limit.
            if (catalog.Stream!.Length > 64L * 1024 * 1024) return CatalogVerification.Failure;
            using var bytes = new MemoryStream();
            catalog.Stream.CopyTo(bytes);
            CatalogVerification signature = VerifySignature(bytes.ToArray(), trustedThumbprints);
            if (signature != CatalogVerification.Success) return signature;
            string[] skipped = (filesToSkip ?? Enumerable.Empty<string>()).ToArray();
            var skippedNames = new HashSet<string>(skipped, StringComparer.OrdinalIgnoreCase);
            using var ownSnapshot = suppliedSnapshot == null ? ReadOnlyDirectorySnapshot.Capture(directory,
                relative => !skippedNames.Contains(Path.GetFileName(relative))) : null;
            ReadOnlyDirectorySnapshot snapshot = suppliedSnapshot ?? ownSnapshot!;
            CatalogManifest? manifest = await CatalogManifestReader.ReadAsync(catalogFile);
            return manifest != null && manifest.Matches(snapshot, skipped, Path.GetRelativePath(Path.GetFullPath(directory), Path.GetFullPath(catalogFile)))
                ? CatalogVerification.Success : CatalogVerification.FilesDoNotMatch;
        }
    }

    /// <summary>
    /// Checks that the catalog is signed and that every signature is valid and made by a trusted certificate.
    /// The certificates are pinned and self-signed, so the chain is not built.
    /// </summary>
    public static CatalogVerification VerifySignature(byte[] catalog, IEnumerable<string> trustedThumbprints)
    {
        var trusted = new HashSet<string>(
            trustedThumbprints.Select(NormalizeThumbprint), StringComparer.OrdinalIgnoreCase);

        var cms = new SignedCms();
        try
        {
            cms.Decode(catalog);
        }
        catch (CryptographicException)
        {
            return CatalogVerification.NoSignature;
        }

        if (cms.SignerInfos.Count == 0)
            return CatalogVerification.NoSignature;

        foreach (SignerInfo signer in cms.SignerInfos)
        {
            // Only the signer certificate is compared: a certificate merely embedded in the
            // catalog proves nothing about who signed it.
            if (signer.Certificate == null || !trusted.Contains(signer.Certificate.Thumbprint))
                return CatalogVerification.NotTrustedSignature;

            try
            {
                signer.CheckSignature(verifySignatureOnly: true);
            }
            catch (CryptographicException)
            {
                return CatalogVerification.NotTrustedSignature;
            }
        }

        return CatalogVerification.Success;
    }

    private static string NormalizeThumbprint(string thumbprint) =>
        new string(thumbprint.Where(Uri.IsHexDigit).ToArray());

}
