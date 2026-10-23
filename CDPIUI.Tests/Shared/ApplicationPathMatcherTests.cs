using CDPIUI.Shared.Basic.Filesystem;

namespace CDPIUI.Tests.Shared;

public class ApplicationPathMatcherTests
{
    private const string InstallDirectory = @"C:\Program Files\Storik4\CDPI UI";

    [Theory]
    [InlineData(@"C:\Program Files\Storik4\CDPI UI\CDPIUI_TrayIcon.exe")]
    [InlineData(@"c:\program files\storik4\cdpi ui\Store\Items\CSZTBN012\bin\winws.exe")]
    [InlineData(@"\??\C:\Users\user\AppData\Local\CDPIUI\Store\Items\CSZTBN012\bin\WinDivert64.sys")]
    [InlineData("\"C:\\Users\\user\\AppData\\Local\\CDPIUI\\Store\\Items\\CSGIVS036\\x86_64\\goodbyedpi.exe\"")]
    [InlineData(@"C:/Users/user/AppData/Local/CDPIUI/Store/Items/CSBIHA024/ciadpi.exe")]
    public void ApplicationFilesMatch(string path)
    {
        Assert.True(ApplicationPathMatcher.BelongsToApplication(path, InstallDirectory));
    }

    [Theory]
    [InlineData(@"C:\Program Files\Storik4\CDPI UI Other\winws.exe")]
    [InlineData(@"C:\zapret\bin\winws.exe")]
    [InlineData(@"\??\C:\zapret\bin\WinDivert64.sys")]
    [InlineData(@"C:\Users\user\AppData\Local\CDPIUI2\winws.exe")]
    [InlineData(@"\SystemRoot\System32\drivers\WinDivert.sys")]
    [InlineData("")]
    [InlineData(null)]
    public void OtherFilesDoNotMatch(string? path)
    {
        Assert.False(ApplicationPathMatcher.BelongsToApplication(path, InstallDirectory));
    }

    [Fact]
    public void InstallDirectoryWithTrailingSeparatorMatches()
    {
        Assert.True(ApplicationPathMatcher.BelongsToApplication(
            @"C:\Program Files\Storik4\CDPI UI\CDPIUI.exe", InstallDirectory + "\\"));
    }
}
