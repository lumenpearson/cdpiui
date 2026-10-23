using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Threading.Tasks;

namespace CDPIUI.Shared.Security
{
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
        private static readonly TimeSpan FileCheckTimeout = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Checks the catalog signature and that the files of the directory match the catalog
        /// </summary>
        /// <param name="catalogFile">Catalog file</param>
        /// <param name="directory">Directory the catalog describes</param>
        /// <param name="trustedThumbprints">SHA-1 thumbprints of the certificates allowed to sign catalogs</param>
        public static async Task<CatalogVerification> VerifyAsync(string catalogFile, string directory, IEnumerable<string> trustedThumbprints)
        {
            if (!File.Exists(catalogFile))
                return CatalogVerification.NoSignature;

            CatalogVerification signature = VerifySignature(File.ReadAllBytes(catalogFile), trustedThumbprints);
            if (signature != CatalogVerification.Success)
                return signature;

            return await VerifyFilesAsync(catalogFile, directory) ? CatalogVerification.Success : CatalogVerification.FilesDoNotMatch;
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

        private static async Task<bool> VerifyFilesAsync(string catalogFile, string directory)
        {
            var startInfo = new ProcessStartInfo("powershell.exe")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            // Paths go through the environment, so quotes in them cannot change the command.
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add(
                "(Test-FileCatalog -CatalogFilePath $env:CDPIUI_CATALOG -Path $env:CDPIUI_CATALOG_DIRECTORY -FilesToSkip catalog.cat).ToString()");
            startInfo.Environment["CDPIUI_CATALOG"] = Path.GetFullPath(catalogFile);
            startInfo.Environment["CDPIUI_CATALOG_DIRECTORY"] = Path.GetFullPath(directory);

            using var process = Process.Start(startInfo);
            if (process == null)
                return false;

            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> errors = process.StandardError.ReadToEndAsync();
            Task exited = Task.Run(() => process.WaitForExit());

            if (await Task.WhenAny(exited, Task.Delay(FileCheckTimeout)) != exited)
            {
                try { process.Kill(); } catch { }
                return false;
            }

            await Task.WhenAll(output, errors);
            return string.Equals(output.Result.Trim(), "Valid", StringComparison.OrdinalIgnoreCase);
        }
    }
}
