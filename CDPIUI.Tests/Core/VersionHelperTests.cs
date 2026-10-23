using CDPIUI.Core.Store;

namespace CDPIUI.Tests.Core;

public class VersionHelperTests
{
    [Theory]
    [InlineData("1.0.0", "1.0.1")]
    [InlineData("1.9.0", "1.10.0")]
    [InlineData("v1.2.0", "v1.3.0")]
    [InlineData("1.0.0.1", "1.0.0.2")]
    public void OlderVersionPrecedesNewer(string oldVersion, string newVersion)
    {
        Assert.True(VersionHelper.CompareVersionStrings(oldVersion, newVersion) < 0);
    }

    [Fact]
    public void EqualVersionsCompareAsZero()
    {
        Assert.Equal(0, VersionHelper.CompareVersionStrings("1.2.3", "1.2.3"));
        Assert.Equal(0, VersionHelper.CompareVersionStrings("1.0", "1.0.0"));
        Assert.Equal(0, VersionHelper.CompareVersionStrings("1.0.0.0", "1.0"));
    }

    [Theory]
    [InlineData("1.0.0-rc1", "1.0.0")]
    [InlineData("1.0.0rc1", "1.0.0")]
    [InlineData("1.0.0-rc1", "1.0.0-rc2")]
    [InlineData("release-70.5", "release-70.6")]
    [InlineData("zapret 69", "zapret 70")]
    public void PrereleaseAndTaggedVersionsAreOrdered(string oldVersion, string newVersion)
    {
        Assert.True(VersionHelper.CompareVersionStrings(oldVersion, newVersion) < 0);
        Assert.True(VersionHelper.CompareVersionStrings(newVersion, oldVersion) > 0);
    }

    [Theory]
    [InlineData("1.0.4.0", "1.0.0", "NaN", true)]
    [InlineData("1.0.4.0", "1.0.0", null, true)]
    [InlineData("1.0.4.0", "1.0.0", "1.0.3", false)]
    [InlineData("1.0.4.0", "2.0", "NaN", false)]
    [InlineData("1.0.4.0", null, "NaN", false)]
    [InlineData("1.0.4.0", "1.0.0", "garbage", false)]
    public void IsVersionInRangeHandlesMissingBounds(string version, string? min, string? max, bool expected)
    {
        Assert.Equal(expected, VersionHelper.IsVersionInRange(version, min, max));
    }
}
