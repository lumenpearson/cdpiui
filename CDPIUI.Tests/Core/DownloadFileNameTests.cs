using CDPIUI.Core.Store.Network;

namespace CDPIUI.Tests.Core;

public sealed class DownloadFileNameTests
{
    [Theory]
    [InlineData("winws")]
    [InlineData("installer.v2")]
    [InlineData("ProxiFyre")]
    public void PlainNameIsAccepted(string name) => Assert.True(DownloadWorker.IsPlainFileName(name));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("..\\..\\Startup\\x")]
    [InlineData("../x")]
    [InlineData("sub\\x")]
    [InlineData("C:\\Windows\\x")]
    [InlineData("x\0")]
    public void PathIsRejected(string name) => Assert.False(DownloadWorker.IsPlainFileName(name));
}
