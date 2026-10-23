using CDPIUI.AddOns.ConfigShare;

namespace CDPIUI.Tests.AddOns;

public sealed class ConfigShareSafeDestinationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "cdpiui-share-dest-" + Guid.NewGuid().ToString("N"));

    public ConfigShareSafeDestinationTests() => Directory.CreateDirectory(root);

    public void Dispose() => Directory.Delete(root, true);

    [Theory]
    [InlineData("manifest.json")]
    [InlineData("resources/hosts.txt")]
    [InlineData("resources/sub/tls.bin")]
    [InlineData("resources/con1.txt")]
    [InlineData("resources/CONSOLE.txt")]
    [InlineData("resources/a..b.txt")]
    [InlineData("resources/.hidden")]
    public void PathsInsideTheRootAreAccepted(string relative)
    {
        string destination = ConfigShareService.SafeDestination(root, relative);

        Assert.Equal(Path.Combine([root, .. relative.Split('/')]), destination);
    }

    [Fact]
    public void RootWithTrailingSeparatorGivesTheSamePath()
    {
        Assert.Equal(
            ConfigShareService.SafeDestination(root, "a.txt"),
            ConfigShareService.SafeDestination(root + Path.DirectorySeparatorChar, "a.txt"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("../evil.txt")]
    [InlineData("resources/../../evil.txt")]
    [InlineData("resources/./a.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("resources//a.txt")]
    [InlineData("resources/")]
    [InlineData("resources\\a.txt")]
    [InlineData("C:/Windows/evil.txt")]
    [InlineData("resources/a.txt:stream")]
    [InlineData("resources/a.txt.")]
    [InlineData("resources/a.txt ")]
    [InlineData("resources/CON")]
    [InlineData("resources/nul.txt")]
    [InlineData("resources/Com1.log")]
    [InlineData("LPT9")]
    [InlineData("resources/a\0b.txt")]
    public void UnsafePathsAreRejected(string relative)
    {
        var exception = Assert.Throws<ConfigShareException>(() => ConfigShareService.SafeDestination(root, relative));

        Assert.Equal("SHARE_ARCHIVE_INVALID", exception.Code);
    }

    [Fact]
    public void LinkInsideTheRootIsRejected()
    {
        string outside = Path.Combine(root + "-outside");
        Directory.CreateDirectory(outside);
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(root, "resources"), outside);

            var exception = Assert.Throws<ConfigShareException>(() =>
                ConfigShareService.SafeDestination(root, "resources/a.txt"));

            Assert.Equal("SHARE_DESTINATION_INVALID", exception.Code);
        }
        finally
        {
            Directory.Delete(outside, true);
        }
    }

    [Fact]
    public void ExistingNormalFoldersAreAccepted()
    {
        Directory.CreateDirectory(Path.Combine(root, "resources", "sub"));

        Assert.EndsWith("tls.bin", ConfigShareService.SafeDestination(root, "resources/sub/tls.bin"));
    }
}
