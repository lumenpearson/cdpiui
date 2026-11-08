using System.Runtime.InteropServices;
using CDPIUI.Shared.Security;

namespace CDPIUI.Tests.Shared;

[Collection("Windows path leases")]
public sealed class WritablePathLeaseTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cdpiui-write-lease-" + Guid.NewGuid().ToString("N"));
    public WritablePathLeaseTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void ExistingBytesAreNotTruncatedByOpenAndTheHandleCanAppend()
    {
        if (!OperatingSystem.IsWindows()) return;
        string file = Path.Combine(_root, "data.bin");
        File.WriteAllText(file, "original");
        using (var lease = WritablePathLease.Open(file))
        {
            Assert.Equal(8, lease.Stream.Length);
            Assert.Throws<IOException>(() => File.WriteAllText(file, "changed"));
            Assert.Throws<IOException>(() => Directory.Move(_root, _root + "-moved"));
            lease.Stream.Seek(0, SeekOrigin.End);
            lease.Stream.WriteByte((byte)'!');
        }
        Assert.Equal("original!", File.ReadAllText(file));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LinkedLeafIsRejectedWithoutChangingItsTarget(bool hardLink)
    {
        if (!OperatingSystem.IsWindows()) return;
        string outside = Path.Combine(_root, "outside.bin");
        string file = Path.Combine(_root, "linked.bin");
        File.WriteAllText(outside, "keep");
        if (hardLink) Assert.True(CreateHardLinkW(file, outside, IntPtr.Zero));
        else File.CreateSymbolicLink(file, outside);
        Assert.Throws<UnauthorizedAccessException>(() => WritablePathLease.Open(file));
        Assert.Equal("keep", File.ReadAllText(outside));
        Directory.Move(_root, _root + "-moved");
        Directory.Move(_root + "-moved", _root);
    }

    [Fact]
    public void NewFileIsCreatedInsideSafelyCreatedParents()
    {
        if (!OperatingSystem.IsWindows()) return;
        string file = Path.Combine(_root, "new", "nested", "data.bin");
        using (var lease = WritablePathLease.Open(file)) lease.Stream.WriteByte(42);
        Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(file));
    }

    [Fact]
    public void AlternateStreamIsRejectedBeforeCreation()
    {
        if (!OperatingSystem.IsWindows()) return;
        string file = Path.Combine(_root, "data.bin");
        File.WriteAllText(file, "keep");
        Assert.Throws<ArgumentException>(() => WritablePathLease.Open(file + ":stream"));
        Assert.Equal("keep", File.ReadAllText(file));
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string name, string existing, IntPtr attributes);
}
