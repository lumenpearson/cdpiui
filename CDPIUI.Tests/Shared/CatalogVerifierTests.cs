using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using CDPIUI.Shared.Security;

namespace CDPIUI.Tests.Shared;

public class CatalogVerifierTests
{
    // Signer of the catalogs in the published application patches (CN=Storik4).
    private const string ReleaseThumbprint = "03AB9EBCE1C2A66BE3974FD17AB90E0ECCDB2909";

    private static byte[] ReleaseCatalog() =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestData", "release-catalog.cat"));

    [Fact]
    public void ReleaseCatalogSignedByTrustedCertificatePasses()
    {
        Assert.Equal(CatalogVerification.Success, CatalogVerifier.VerifySignature(ReleaseCatalog(), [ReleaseThumbprint]));
    }

    [Fact]
    public void ThumbprintFormattingIsIgnored()
    {
        Assert.Equal(CatalogVerification.Success,
            CatalogVerifier.VerifySignature(ReleaseCatalog(), ["03 ab 9e bc e1 c2 a6 6b e3 97 4f d1 7a b9 0e 0e cc db 29 09"]));
    }

    [Fact]
    public void ReleaseCatalogWithUnknownThumbprintFails()
    {
        Assert.Equal(CatalogVerification.NotTrustedSignature,
            CatalogVerifier.VerifySignature(ReleaseCatalog(), ["0000000000000000000000000000000000000000"]));
    }

    [Fact]
    public void ModifiedCatalogContentFails()
    {
        var cms = new SignedCms();
        cms.Decode(ReleaseCatalog());
        byte[] content = (byte[])cms.ContentInfo.Content.Clone();

        // Change one byte of the signed content and keep the original signature and certificate.
        byte[] catalog = ReleaseCatalog();
        int index = IndexOf(catalog, content);
        Assert.True(index > 0);
        catalog[index + content.Length / 2] ^= 0xFF;

        Assert.Equal(CatalogVerification.NotTrustedSignature, CatalogVerifier.VerifySignature(catalog, [ReleaseThumbprint]));
    }

    [Fact]
    public void CatalogSignedByAnotherKeyWithEmbeddedTrustedCertificateFails()
    {
        var release = new SignedCms();
        release.Decode(ReleaseCatalog());
        X509Certificate2 trusted = release.Certificates[0];

        using RSA key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Storik4", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 attacker = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        var forged = new SignedCms(release.ContentInfo, false);
        forged.ComputeSignature(new CmsSigner(attacker));
        forged.Certificates.Add(trusted);

        Assert.Equal(CatalogVerification.NotTrustedSignature,
            CatalogVerifier.VerifySignature(forged.Encode(), [ReleaseThumbprint]));
    }

    [Fact]
    public void UnsignedDataFails()
    {
        Assert.Equal(CatalogVerification.NoSignature, CatalogVerifier.VerifySignature([1, 2, 3], [ReleaseThumbprint]));
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            int j = 0;
            while (j < needle.Length && haystack[i + j] == needle[j]) j++;
            if (j == needle.Length) return i;
        }
        return -1;
    }
}
