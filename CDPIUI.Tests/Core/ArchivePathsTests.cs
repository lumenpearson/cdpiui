using CDPIUI.Core.System;

namespace CDPIUI.Tests.Core;

public class ArchivePathsTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "cdpiui-archive-root");

    [Theory]
    [InlineData("file.txt")]
    [InlineData("lists/hosts.txt")]
    [InlineData("a/./b/c.bin")]
    [InlineData("a/../b.txt")]
    public void AcceptsPathsInsideRoot(string relative)
    {
        Assert.True(ArchivePaths.TryGetDestination(Root, relative, out string destination));
        Assert.StartsWith(Path.GetFullPath(Root), destination);
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("a/../../evil.txt")]
    [InlineData("/etc/evil")]
    [InlineData("C:/evil.txt")]
    [InlineData("C:evil.txt")]
    [InlineData("")]
    public void RejectsPathsOutsideRoot(string relative)
    {
        Assert.False(ArchivePaths.TryGetDestination(Root, relative, out _));
    }

    [Fact]
    public void RejectsSiblingDirectoryWithSamePrefix()
    {
        Assert.False(ArchivePaths.TryGetDestination(Root, "../cdpiui-archive-root2/x.txt", out _));
    }

    [Fact]
    public void RootWithTrailingSeparatorBehavesTheSame()
    {
        Assert.True(ArchivePaths.TryGetDestination(Root + Path.DirectorySeparatorChar, "x.txt", out string a));
        Assert.True(ArchivePaths.TryGetDestination(Root, "x.txt", out string b));
        Assert.Equal(a, b);
    }

    [Fact]
    public void GetDestinationOrThrowThrowsInvalidData()
    {
        Assert.Throws<InvalidDataException>(() => ArchivePaths.GetDestinationOrThrow(Root, "../x"));
    }
}
