using CDPIUI.Shared.Security;

namespace CDPIUI.Tests.Shared;

public sealed class Sha256FileTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "cdpiui-sha256-" + Guid.NewGuid().ToString("N"));

    public Sha256FileTests() => File.WriteAllText(_file, "abc");

    public void Dispose() => File.Delete(_file);

    [Fact]
    public void ComputeReturnsTheFileDigest() =>
        Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", Sha256File.Compute(_file));

    [Theory]
    [InlineData("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    [InlineData("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD")]
    public void MatchingFileDigestIsAccepted(string expected) =>
        Assert.True(Sha256File.Matches(_file, expected));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ba7816bf")]
    [InlineData("gg7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000")]
    public void DifferentOrInvalidFileDigestIsRejected(string? expected) =>
        Assert.False(Sha256File.Matches(_file, expected));
}
