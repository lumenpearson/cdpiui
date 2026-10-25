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
    [InlineData("sha256:ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    public void MatchingHashIsAccepted(string hash) => Assert.True(DownloadWorker.HasSha256(_file, hash));

    [Theory]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("ba7816bf")]
    public void OtherHashIsRejected(string hash) => Assert.False(DownloadWorker.HasSha256(_file, hash));

    [Theory]
    // Characters used to be dropped: "sha512:" kept "a512", and a typo became a mismatch.
    [InlineData("sha512:ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    [InlineData("ba7816bf-8f01cfea-414140de-5dae2223-b00361a3-96177a9c-b410ff61-f20015ad")]
    [InlineData("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015az")]
    public void HashInAnotherFormatIsInvalid(string hash) => Assert.Null(DownloadWorker.NormalizeSha256(hash));
}
