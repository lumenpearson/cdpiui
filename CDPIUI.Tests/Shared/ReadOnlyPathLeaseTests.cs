using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using CDPIUI.Shared.Security;

namespace CDPIUI.Tests.Shared;

[Collection("Windows path leases")]
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
    public void ChildLeaseKeepsParentAliveWithoutDuplicatingItsChain()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var parent = ReadOnlyPathLease.OpenDirectory(_root);
        using (var child = ReadOnlyPathLease.OpenFileInHeldDirectory(FilePath, parent))
        {
            parent.Dispose();
            Assert.Throws<IOException>(() => Directory.Move(_root, _root + "-moved"));
            Assert.Throws<IOException>(() => File.WriteAllText(FilePath, "changed"));
            Assert.Throws<ObjectDisposedException>(() => ReadOnlyPathLease.OpenFileInHeldDirectory(FilePath, parent));
        }
        Directory.Move(_root, _root + "-moved");
        Directory.Move(_root + "-moved", _root);
    }

    [Fact]
    public void HeldParentOnlyAllowsItsImmediateChildren()
    {
        if (!OperatingSystem.IsWindows()) return;
        string subdirectory = Path.Combine(_root, "child");
        Directory.CreateDirectory(subdirectory);
        string nested = Path.Combine(subdirectory, "nested.bin");
        File.WriteAllText(nested, "bytes");
        using (var parent = ReadOnlyPathLease.OpenDirectory(_root))
        {
            Assert.Throws<ArgumentException>(() => ReadOnlyPathLease.OpenFileInHeldDirectory(nested, parent));
            using var child = ReadOnlyPathLease.OpenDirectoryInHeldDirectory(subdirectory, parent);
            using var file = ReadOnlyPathLease.OpenFileInHeldDirectory(nested, child);
            Assert.Equal(5, file.Stream!.Length);
        }
    }

    [Fact]
    public void FailedChildOpenReleasesItsParentReference()
    {
        if (!OperatingSystem.IsWindows()) return;
        using (var parent = ReadOnlyPathLease.OpenDirectory(_root))
            Assert.Throws<IOException>(() => ReadOnlyPathLease.OpenFileInHeldDirectory(Path.Combine(_root, "missing"), parent));
        Directory.Move(_root, _root + "-moved");
        Directory.Move(_root + "-moved", _root);
    }

    [Fact]
    public void IndependentSiblingLeaseDoesNotPreventRenamingReleasedDirectory()
    {
        if (!OperatingSystem.IsWindows()) return;
        string sibling = _root + "-sibling";
        Directory.CreateDirectory(sibling);
        try
        {
            using var held = ReadOnlyPathLease.OpenDirectory(sibling);
            using (var released = ReadOnlyPathLease.OpenDirectory(_root)) { }
            Directory.Move(_root, _root + "-moved");
            Directory.Move(_root + "-moved", _root);
        }
        finally { Directory.Delete(sibling); }
    }

    [Fact]
    public void PinnedAncestorCannotBeEmptiedOrConvertedToAJunction()
    {
        if (!OperatingSystem.IsWindows()) return;
        string ancestor = Path.Combine(_root, "ancestor");
        string leaf = Path.Combine(ancestor, "leaf");
        Directory.CreateDirectory(leaf);
        string file = Path.Combine(leaf, "data.bin");
        File.WriteAllText(file, "verified");
        using (var lease = ReadOnlyPathLease.OpenFile(file))
        {
            // Ancestors allow write sharing so unrelated children can be renamed. The
            // held child prevents the ancestor becoming empty, which NTFS requires for
            // replacing an ordinary directory with a junction in place.
            using var handle = CreateFileW(ancestor, 0x40000000, 7, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
            Assert.False(handle.IsInvalid, $"Cannot open ancestor: {Marshal.GetLastWin32Error()}");
            byte[] data = JunctionData(_root);
            Assert.False(DeviceIoControl(handle, 0x000900A4, data, data.Length, IntPtr.Zero, 0, out _, IntPtr.Zero));
            Assert.Equal(145, Marshal.GetLastWin32Error()); // ERROR_DIR_NOT_EMPTY, not a sharing denial.
            Assert.Throws<IOException>(() => Directory.Move(leaf, leaf + "-moved"));
        }
    }

    private static byte[] JunctionData(string target)
    {
        byte[] substitute = Encoding.Unicode.GetBytes(@"\??\" + target);
        byte[] print = Encoding.Unicode.GetBytes(target);
        byte[] data = new byte[16 + substitute.Length + 2 + print.Length + 2];
        using var writer = new BinaryWriter(new MemoryStream(data));
        writer.Write(0xA0000003u);
        writer.Write((ushort)(data.Length - 8));
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)substitute.Length);
        writer.Write((ushort)(substitute.Length + 2));
        writer.Write((ushort)print.Length);
        writer.Write(substitute);
        writer.Write((ushort)0);
        writer.Write(print);
        writer.Write((ushort)0);
        return data;
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

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share,
        IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle handle, uint code, byte[] input, int inputSize,
        IntPtr output, int outputSize, out uint returned, IntPtr overlapped);
}
