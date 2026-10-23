using CDPIUI.Shared.Secrets;
using CDPIUI.Shared.Security;

namespace CDPIUI.Core.Security
{
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
        public static async Task<CatalogCheckResult> CheckCatalog(string catalogFile, string compareDirectory)
        {
            try
            {
                CatalogVerification verification = await CatalogVerifier.VerifyAsync(
                    catalogFile, compareDirectory, Secret.TrustedCertificates);

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
}
