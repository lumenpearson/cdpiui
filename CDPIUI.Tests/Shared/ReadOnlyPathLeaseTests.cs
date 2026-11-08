using System.Runtime.InteropServices;
using CDPIUI.Shared.Security;

namespace CDPIUI.Tests.Shared;

public sealed class ReadOnlyPathLeaseTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cdpiui-read-lease-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_root, "package.bin");
    public ReadOnlyPathLeaseTests()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(FilePath, "verified bytes");
    }
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void HoldsFileAndParentsUntilDisposed()
    {
        if (!OperatingSystem.IsWindows()) return;
        using (var lease = ReadOnlyPathLease.OpenFile(FilePath))
        {
            Assert.Throws<IOException>(() => File.WriteAllText(FilePath, "changed"));
            Assert.Throws<IOException>(() => File.Delete(FilePath));
            Assert.Throws<IOException>(() => File.Move(FilePath, FilePath + ".moved"));
            Assert.Throws<IOException>(() => Directory.Move(_root, _root + "-moved"));
            using var reader = File.OpenRead(FilePath);
            Assert.Equal(reader.Length, lease.Stream!.Length);
        }
        File.WriteAllText(FilePath, "after disposal");
        Directory.Move(_root, _root + "-moved");
        Directory.Move(_root + "-moved", _root);
    }

    [Fact]
    public void ConsumerLeaseKeepsProtectionAfterProducerDisposesItsLease()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var producer = ReadOnlyPathLease.OpenFile(FilePath);
        using (var consumer = ReadOnlyPathLease.OpenFile(FilePath))
        {
            producer.Dispose();
            Assert.Throws<IOException>(() => File.WriteAllText(FilePath, "changed during handoff"));
            Assert.Throws<IOException>(() => Directory.Move(_root, _root + "-moved"));
        }
        File.WriteAllText(FilePath, "after consumer exits");
    }

    [Fact]
    public void ExistingWriterIsRejected()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var writer = new FileStream(FilePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
        Assert.Throws<IOException>(() => ReadOnlyPathLease.OpenFile(FilePath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SymbolicFileLinkIsRejected(bool dangling)
    {
        if (!OperatingSystem.IsWindows()) return;
        string link = Path.Combine(_root, "link.bin");
        File.CreateSymbolicLink(link, dangling ? Path.Combine(_root, "missing.bin") : FilePath);
        Assert.Throws<UnauthorizedAccessException>(() => ReadOnlyPathLease.OpenFile(link));
        // Failure must release the directory chain too.
        Directory.Move(_root, _root + "-moved");
        Directory.Move(_root + "-moved", _root);
    }

    [Fact]
    public void SymbolicParentIsRejected()
    {
        if (!OperatingSystem.IsWindows()) return;
        string target = Path.Combine(_root, "target");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "a.bin"), "bytes");
        string link = Path.Combine(_root, "parent-link");
        Directory.CreateSymbolicLink(link, target);
        Assert.Throws<UnauthorizedAccessException>(() => ReadOnlyPathLease.OpenFile(Path.Combine(link, "a.bin")));
    }

    [Fact]
    public void HardLinkedFileIsRejected()
    {
        if (!OperatingSystem.IsWindows()) return;
        string alias = Path.Combine(_root, "alias.bin");
        Assert.True(CreateHardLinkW(alias, FilePath, IntPtr.Zero), $"CreateHardLink failed: {Marshal.GetLastWin32Error()}");
        Assert.Throws<UnauthorizedAccessException>(() => ReadOnlyPathLease.OpenFile(FilePath));
    }

    [Fact]
    public void MissingFileDoesNotLeaveParentLocked()
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.Throws<IOException>(() => ReadOnlyPathLease.OpenFile(Path.Combine(_root, "missing.bin")));
        Directory.Move(_root, _root + "-moved");
        Directory.Move(_root + "-moved", _root);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string name, string existing, IntPtr attributes);
}
