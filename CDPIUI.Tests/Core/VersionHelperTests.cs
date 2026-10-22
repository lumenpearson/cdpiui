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
    }
}
