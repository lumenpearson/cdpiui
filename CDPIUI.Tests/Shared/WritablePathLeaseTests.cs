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
    public void HandleRenamePublishesHeldBytesAndPinsTheNewParent()
    {
        if (!OperatingSystem.IsWindows()) return;
        string source = Path.Combine(_root, "source", "data.bin");
        string destination = Path.Combine(_root, "target", "data.bin");
        using (var lease = WritablePathLease.CreateForMove(source))
        {
            lease.Stream.WriteByte(42);
            Assert.Throws<IOException>(() => File.Move(source, source + ".replaced"));
            lease.MoveTo(destination);
            Assert.False(File.Exists(source));
            Assert.True(File.Exists(destination));
            Assert.Throws<IOException>(() => Directory.Move(Path.GetDirectoryName(destination)!, Path.Combine(_root, "moved")));
        }
        Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(destination));
        Assert.Empty(Directory.EnumerateFiles(_root, ".cdpiui-directory-lease-*", SearchOption.AllDirectories));
    }

    [Fact]
    public void HandleRenameWithoutReplacementPreservesBothFilesOnConflict()
    {
        if (!OperatingSystem.IsWindows()) return;
        string source = Path.Combine(_root, "source.bin");
        string destination = Path.Combine(_root, "target.bin");
        File.WriteAllText(source, "new");
        File.WriteAllText(destination, "old");
        using (var lease = WritablePathLease.OpenExistingForMove(source))
            Assert.Throws<IOException>(() => lease.MoveTo(destination));
        Assert.Equal("new", File.ReadAllText(source));
        Assert.Equal("old", File.ReadAllText(destination));
    }

    [Fact]
    public void HandleReplacementDoesNotWriteThroughAnExistingHardLink()
    {
        if (!OperatingSystem.IsWindows()) return;
        string source = Path.Combine(_root, "source.bin");
        string destination = Path.Combine(_root, "target.bin");
        string outside = Path.Combine(_root, "outside.bin");
        File.WriteAllText(source, "new");
        File.WriteAllText(outside, "keep");
        Assert.True(CreateHardLinkW(destination, outside, IntPtr.Zero));
        using (var lease = WritablePathLease.OpenExistingForMove(source)) lease.MoveTo(destination, replace: true);
        Assert.Equal("new", File.ReadAllText(destination));
        Assert.Equal("keep", File.ReadAllText(outside));
    }

    [Fact]
    public void ExactHeldFileCanBeMarkedForDeletionAndRemovedOnClose()
    {
        if (!OperatingSystem.IsWindows()) return;
        string file = Path.Combine(_root, "owned.bin");
        using (var lease = WritablePathLease.CreateForMove(file))
        {
            lease.Stream.WriteByte(42);
            lease.DeleteOnClose();
        }
        Assert.False(File.Exists(file));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root));
    }

    [Fact]
    public void LockFileIsValidatedBeforeItCanBeDeletedOnClose()
    {
        if (!OperatingSystem.IsWindows()) return;
        string file = Path.Combine(_root, "settings.lock");
        using (var lease = WritablePathLease.OpenLock(file))
        {
            Assert.Equal(0, lease.Stream.Length);
            Assert.True(Record.Exception(() => File.OpenWrite(file)) is IOException or UnauthorizedAccessException);
        }
        Assert.False(File.Exists(file));
        File.WriteAllText(file, "unknown contents");
        Assert.Throws<UnauthorizedAccessException>(() => WritablePathLease.OpenLock(file));
        Assert.Equal("unknown contents", File.ReadAllText(file));
    }

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
