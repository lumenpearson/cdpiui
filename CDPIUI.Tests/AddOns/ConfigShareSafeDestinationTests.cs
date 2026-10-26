using CDPIUI.AddOns.ConfigShare;

namespace CDPIUI.Tests.AddOns;

public sealed class ConfigShareSafeDestinationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cdpiui-share-dest-" + Guid.NewGuid().ToString("N"));

    public ConfigShareSafeDestinationTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, true);

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
        string destination = ConfigShareService.SafeDestination(_root, relative);

        Assert.Equal(Path.Combine([_root, .. relative.Split('/')]), destination);
    }

    [Fact]
    public void RootWithTrailingSeparatorGivesTheSamePath()
    {
        Assert.Equal(
            ConfigShareService.SafeDestination(_root, "a.txt"),
            ConfigShareService.SafeDestination(_root + Path.DirectorySeparatorChar, "a.txt"));
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
        var exception = Assert.Throws<ConfigShareException>(() => ConfigShareService.SafeDestination(_root, relative));

        Assert.Equal("SHARE_ARCHIVE_INVALID", exception.Code);
    }

    [Fact]
    public void LinkInsideTheRootIsRejected()
    {
        string outside = Path.Combine(_root + "-outside");
        Directory.CreateDirectory(outside);
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(_root, "resources"), outside);

            var exception = Assert.Throws<ConfigShareException>(() =>
                ConfigShareService.SafeDestination(_root, "resources/a.txt"));

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
        Directory.CreateDirectory(Path.Combine(_root, "resources", "sub"));

        Assert.EndsWith("tls.bin", ConfigShareService.SafeDestination(_root, "resources/sub/tls.bin"));
    }
}
