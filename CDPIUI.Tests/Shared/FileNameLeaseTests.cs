using System.Runtime.InteropServices;
using CDPIUI.Shared.Security;

namespace CDPIUI.Tests.Shared;

[Collection("Windows path leases")]
public sealed class FileNameLeaseTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cdpiui-name-lease-" + Guid.NewGuid().ToString("N"));
    public FileNameLeaseTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    [Theory]
    [InlineData("symbolic")]
    [InlineData("dangling")]
    [InlineData("hard")]
    public void RenamesAndDeletesOnlyTheHeldLinkName(string kind)
    {
        if (!OperatingSystem.IsWindows()) return;
        string target = Path.Combine(_root, "target.txt");
        File.WriteAllText(target, "preserve target bytes");
        string name = Path.Combine(_root, "link.txt");
        if (kind == "hard") Assert.True(CreateHardLinkW(name, target, IntPtr.Zero));
        else File.CreateSymbolicLink(name, kind == "dangling" ? target + ".missing" : target);
        string published = Path.Combine(_root, "published", "link.txt");
        using (var lease = FileNameLease.OpenExisting(name))
        {
            Assert.Throws<IOException>(() => File.Delete(name));
            lease.MoveTo(published);
            Assert.False(File.Exists(name));
            Assert.Equal(published, lease.CurrentPath);
            Assert.Throws<IOException>(() => Directory.Move(Path.GetDirectoryName(published)!, Path.Combine(_root, "moved")));
            lease.DeleteOnClose();
        }
        Assert.False(File.Exists(published));
        Assert.False(new FileInfo(published).LinkTarget != null);
        Assert.Equal("preserve target bytes", File.ReadAllText(target));
    }

    [Fact]
    public void RenameConflictPreservesTheHeldOriginalAndExistingDestination()
    {
        if (!OperatingSystem.IsWindows()) return;
        string source = Path.Combine(_root, "source.txt"), destination = Path.Combine(_root, "destination.txt");
        File.WriteAllText(source, "source");
        File.WriteAllText(destination, "destination");
        using (var lease = FileNameLease.OpenExisting(source)) Assert.Throws<IOException>(() => lease.MoveTo(destination));
        Assert.Equal("source", File.ReadAllText(source));
        Assert.Equal("destination", File.ReadAllText(destination));
    }

    [Fact]
    public void RejectsDirectoryLinksWithoutRemovingThem()
    {
        if (!OperatingSystem.IsWindows()) return;
        string name = Path.Combine(_root, "directory-link");
        Directory.CreateSymbolicLink(name, _root);
        Assert.ThrowsAny<IOException>(() => FileNameLease.OpenExisting(name));
        Assert.NotNull(new DirectoryInfo(name).LinkTarget);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string name, string target, IntPtr security);
}
