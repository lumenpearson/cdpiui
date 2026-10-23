using CDPIUI.Shared.Basic.Filesystem;

namespace CDPIUI.Tests.Shared;

public sealed class TrustedPathValidatorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "cdpiui-trusted-" + Guid.NewGuid().ToString("N"));
    private readonly string outside = Path.Combine(Path.GetTempPath(), "cdpiui-outside-" + Guid.NewGuid().ToString("N"));

    public TrustedPathValidatorTests()
    {
        Directory.CreateDirectory(Path.Combine(root, "Store", "Items", "CSZTBN012"));
        File.WriteAllText(Path.Combine(root, "Store", "Items", "CSZTBN012", "winws.exe"), "");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "tool.exe"), "");
    }

    public void Dispose()
    {
        Directory.Delete(root, true);
        Directory.Delete(outside, true);
    }

    [Fact]
    public void FileInsideRootIsAllowed()
    {
        Assert.True(TrustedPathValidator.IsInside(Path.Combine(root, "Store", "Items", "CSZTBN012", "winws.exe"), [root]));
    }

    [Fact]
    public void MissingFileInsideRootIsAllowed()
    {
        Assert.True(TrustedPathValidator.IsInside(Path.Combine(root, "Store", "Items", "new", "patch.msi"), [root]));
    }

    [Fact]
    public void FileOutsideRootsIsRejected()
    {
        Assert.False(TrustedPathValidator.IsInside(Path.Combine(outside, "tool.exe"), [root, null, ""]));
    }

    [Fact]
    public void ParentTraversalIsRejected()
    {
        string path = Path.Combine(root, "Store", "..", "..", Path.GetFileName(outside), "tool.exe");
        Assert.False(TrustedPathValidator.IsInside(path, [root]));
    }

    [Fact]
    public void SiblingWithSamePrefixIsRejected()
    {
        Assert.False(TrustedPathValidator.IsInside(root + "-other" + Path.DirectorySeparatorChar + "tool.exe", [root]));
    }

    [Fact]
    public void LinkedDirectoryInsideRootIsRejected()
    {
        string link = Path.Combine(root, "Store", "link");
        Directory.CreateSymbolicLink(link, outside);

        Assert.False(TrustedPathValidator.IsInside(Path.Combine(link, "tool.exe"), [root]));
    }

    [Fact]
    public void LinkedFileInsideRootIsRejected()
    {
        string link = Path.Combine(root, "Store", "tool.exe");
        File.CreateSymbolicLink(link, Path.Combine(outside, "tool.exe"));

        Assert.False(TrustedPathValidator.IsInside(link, [root]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyPathIsRejected(string? path)
    {
        Assert.False(TrustedPathValidator.IsInside(path, [root]));
    }
}
