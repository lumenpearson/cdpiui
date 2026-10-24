using CDPIUI.Core.Store.Network;

namespace CDPIUI.Tests.Core;

public sealed class DownloadHashTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "cdpiui-hash-" + Guid.NewGuid().ToString("N"));

    public DownloadHashTests() => File.WriteAllText(_file, "abc");

    public void Dispose() => File.Delete(_file);

    [Theory]
    [InlineData("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    [InlineData("BA7816BF 8F01CFEA 414140DE 5DAE2223 B00361A3 96177A9C B410FF61 F20015AD")]
    public void MatchingHashIsAccepted(string hash) => Assert.True(DownloadWorker.HasSha256(_file, hash));

    [Theory]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("ba7816bf")]
    public void OtherHashIsRejected(string hash) => Assert.False(DownloadWorker.HasSha256(_file, hash));
}
