using CDPIUI.Shared.Basic.Filesystem;

namespace CDPIUI.Tests.Shared;

public sealed class TrustedPathValidatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cdpiui-trusted-" + Guid.NewGuid().ToString("N"));
    private readonly string _outside = Path.Combine(Path.GetTempPath(), "cdpiui-outside-" + Guid.NewGuid().ToString("N"));

    public TrustedPathValidatorTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Store", "Items", "CSZTBN012"));
        File.WriteAllText(Path.Combine(_root, "Store", "Items", "CSZTBN012", "winws.exe"), "");
        Directory.CreateDirectory(_outside);
        File.WriteAllText(Path.Combine(_outside, "tool.exe"), "");
    }

    public void Dispose()
    {
        Directory.Delete(_root, true);
        Directory.Delete(_outside, true);
    }

    [Fact]
    public void FileInsideRootIsAllowed()
    {
        Assert.True(TrustedPathValidator.IsInside(Path.Combine(_root, "Store", "Items", "CSZTBN012", "winws.exe"), [_root]));
    }

    [Fact]
    public void MissingFileInsideRootIsAllowed()
    {
        Assert.True(TrustedPathValidator.IsInside(Path.Combine(_root, "Store", "Items", "new", "patch.msi"), [_root]));
    }

    [Fact]
    public void FileOutsideRootsIsRejected()
    {
        Assert.False(TrustedPathValidator.IsInside(Path.Combine(_outside, "tool.exe"), [_root, null, ""]));
    }

    [Fact]
    public void ParentTraversalIsRejected()
    {
        string path = Path.Combine(_root, "Store", "..", "..", Path.GetFileName(_outside), "tool.exe");
        Assert.False(TrustedPathValidator.IsInside(path, [_root]));
    }

    [Fact]
    public void SiblingWithSamePrefixIsRejected()
    {
        Assert.False(TrustedPathValidator.IsInside(_root + "-other" + Path.DirectorySeparatorChar + "tool.exe", [_root]));
    }

    [Fact]
    public void LinkedDirectoryInsideRootIsRejected()
    {
        string link = Path.Combine(_root, "Store", "link");
        Directory.CreateSymbolicLink(link, _outside);

        Assert.False(TrustedPathValidator.IsInside(Path.Combine(link, "tool.exe"), [_root]));
    }

    [Fact]
    public void LinkedFileInsideRootIsRejected()
    {
        string link = Path.Combine(_root, "Store", "tool.exe");
        File.CreateSymbolicLink(link, Path.Combine(_outside, "tool.exe"));

        Assert.False(TrustedPathValidator.IsInside(link, [_root]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyPathIsRejected(string? path)
    {
        Assert.False(TrustedPathValidator.IsInside(path, [_root]));
    }
}
