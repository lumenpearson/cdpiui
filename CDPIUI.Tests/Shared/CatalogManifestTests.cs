using System.Text;
using CDPIUI.Shared.Security;

namespace CDPIUI.Tests.Shared;

public sealed class CatalogManifestTests
{
    private const string Header = "CDPIUI-CATALOG-V1\nSHA256\n";
    private static string Entry(string name, string? hash = null) => Convert.ToBase64String(Encoding.UTF8.GetBytes(name)) + ":" + (hash ?? new string('A', 64)) + "\n";

    [Theory]
    [InlineData("../escape.bin")]
    [InlineData("C:\\escape.bin")]
    [InlineData("\\\\server\\file")]
    [InlineData("nested\\file.")]
    [InlineData("nested\\file:stream")]
    [InlineData("nested\\NUL")]
    [InlineData("nested\\\\file")]
    [InlineData("nested\u0000file")]
    public void RejectsAmbiguousOrEscapingCatalogMember(string name) => Assert.Null(CatalogManifest.Parse(Header + Entry(name)));

    [Theory]
    [InlineData("not-base64:abc\n")]
    [InlineData("/w==:\n")]
    [InlineData("ZGF0YQ==:short\n")]
    [InlineData("\n")]
    public void RejectsMalformedProtocol(string member) => Assert.Null(CatalogManifest.Parse(Header + member));

    [Fact]
    public void RejectsCaseAndSeparatorAliases() => Assert.Null(CatalogManifest.Parse(Header + Entry("nested/A.bin") + Entry("NESTED\\a.bin")));

    [Theory]
    [InlineData("SHA512")]
    [InlineData("MD5")]
    public void RejectsUnknownHashAlgorithm(string algorithm) => Assert.Null(CatalogManifest.Parse("CDPIUI-CATALOG-V1\n" + algorithm + "\n"));

    [Fact]
    public void RejectsExcessiveCatalogEntries()
    {
        var text = new StringBuilder(Header);
        for (int i = 0; i <= 20000; i++) text.Append(Entry(i + ".bin"));
        Assert.Null(CatalogManifest.Parse(text.ToString()));
    }
}
