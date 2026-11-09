using CDPIUI.Shared.Secrets;
using CDPIUI.Shared.Security;

namespace CDPIUI.Core.Security;

public enum CatalogCheckResult
{
    Success,
    FailureNoSignature,
    FailureNotTrustedSignature,
    FailureNotValid,
    FailureUnknown
}

public class CertificateCheck
{
    public static Task<CatalogCheckResult> CheckCatalog(string catalogFile, string compareDirectory) =>
        CheckCatalogCore(() => CatalogVerifier.VerifyAsync(catalogFile, compareDirectory, Secret.TrustedCertificates));

    internal static Task<CatalogCheckResult> CheckCatalog(string catalogFile, ReadOnlyDirectorySnapshot snapshot) =>
        CheckCatalogCore(() => CatalogVerifier.VerifySnapshotAsync(catalogFile, snapshot, Secret.TrustedCertificates));

    private static async Task<CatalogCheckResult> CheckCatalogCore(Func<Task<CatalogVerification>> verify)
    {
        try
        {
            CatalogVerification verification = await verify();

            return verification switch
            {
                CatalogVerification.Success => CatalogCheckResult.Success,
                CatalogVerification.NoSignature => CatalogCheckResult.FailureNoSignature,
                CatalogVerification.NotTrustedSignature => CatalogCheckResult.FailureNotTrustedSignature,
                CatalogVerification.FilesDoNotMatch => CatalogCheckResult.FailureNotValid,
                _ => CatalogCheckResult.FailureUnknown,
            };
        }
        catch
        {
            return CatalogCheckResult.FailureUnknown;
        }
    }
}
