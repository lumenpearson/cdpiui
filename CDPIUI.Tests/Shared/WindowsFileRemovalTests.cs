using System.Runtime.InteropServices;
using CDPIUI.Shared.Security;

namespace CDPIUI.Tests.Shared;

[Collection("Windows path leases")]
public sealed class WindowsFileRemovalTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cdpiui-removal-" + Guid.NewGuid().ToString("N"));
    private string Owned => Path.Combine(_root, "owned");
    public WindowsFileRemovalTests() => Directory.CreateDirectory(Owned);
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void MissingParentsAreNotCreatedForCleanup()
    {
        if (!OperatingSystem.IsWindows()) return;
        string missing = Path.Combine(_root, "missing");
        Assert.False(WindowsFileRemoval.DeleteDirectoryIfExists(Path.Combine(missing, "child")));
        Assert.False(WindowsFileRemoval.DeleteFileIfExists(Path.Combine(missing, "file.bin")));
        Assert.False(Directory.Exists(missing));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LinkedRootOrAncestorNeverDeletesTheTarget(bool ancestor)
    {
        if (!OperatingSystem.IsWindows()) return;
        string outside = Path.Combine(_root, "outside"), link = Path.Combine(_root, "linked");
        Directory.CreateDirectory(Path.Combine(outside, "child"));
        string data = Path.Combine(outside, "child", "keep.bin");
        File.WriteAllText(data, "keep");
        Directory.CreateSymbolicLink(link, outside);
        Assert.Throws<UnauthorizedAccessException>(() => WindowsFileRemoval.DeleteDirectoryIfExists(ancestor ? Path.Combine(link, "child") : link));
        Assert.Equal("keep", File.ReadAllText(data));
        Assert.NotNull(new DirectoryInfo(link).LinkTarget);
    }

    [Fact]
    public void ReadOnlyAliasIsNotMadeWritableToForceCleanup()
    {
        if (!OperatingSystem.IsWindows()) return;
        string outside = Path.Combine(_root, "outside.txt"), alias = Path.Combine(Owned, "alias.txt");
        File.WriteAllText(outside, "keep");
        Assert.True(CreateHardLinkW(alias, outside, IntPtr.Zero));
        File.SetAttributes(outside, FileAttributes.ReadOnly);
        try
        {
            Assert.Throws<IOException>(() => WindowsFileRemoval.DeleteFileIfExists(alias));
            Assert.Equal("keep", File.ReadAllText(outside));
            Assert.True((File.GetAttributes(outside) & FileAttributes.ReadOnly) != 0);
        }
        finally { File.SetAttributes(outside, FileAttributes.Normal); }
    }

    [Fact]
    public void EmptyOnlyRemovalNeverDeletesRecoveryContents()
    {
        if (!OperatingSystem.IsWindows()) return;
        string file = Path.Combine(Owned, "original.bin");
        File.WriteAllText(file, "recovery data");
        IOException error = Assert.Throws<IOException>(() => WindowsFileRemoval.DeleteEmptyDirectoryIfExists(Owned));
        Assert.Equal(145, error.HResult & 0xFFFF);
        Assert.Equal("recovery data", File.ReadAllText(file));
        File.Delete(file);
        Assert.True(WindowsFileRemoval.DeleteEmptyDirectoryIfExists(Owned));
        Assert.False(WindowsFileRemoval.DeleteEmptyDirectoryIfExists(Owned));
    }

    [Fact]
    public void ExcessiveEntriesFailPreflightWithoutRemovingAnyFile()
    {
        if (!OperatingSystem.IsWindows()) return;
        for (int i = 0; i <= 20000; i++) File.WriteAllBytes(Path.Combine(Owned, i + ".bin"), []);
        Assert.Throws<InvalidDataException>(() => WindowsFileRemoval.DeleteDirectoryIfExists(Owned));
        Assert.Equal(20001, Directory.EnumerateFiles(Owned).Count());
    }

    [Fact]
    public void RemovesTheCapturedTreeWithoutDeletingFileLinkTargets()
    {
        if (!OperatingSystem.IsWindows()) return;
        string outside = Path.Combine(_root, "outside.txt");
        File.WriteAllText(outside, "preserve outside");
        Directory.CreateDirectory(Path.Combine(Owned, "nested", "empty"));
        File.WriteAllText(Path.Combine(Owned, "nested", "data.bin"), "owned");
        File.CreateSymbolicLink(Path.Combine(Owned, "soft.txt"), outside);
        File.CreateSymbolicLink(Path.Combine(Owned, "dangling.txt"), outside + ".missing");
        Assert.True(CreateHardLinkW(Path.Combine(Owned, "hard.txt"), outside, IntPtr.Zero));
        Assert.True(WindowsFileRemoval.DeleteDirectoryIfExists(Owned));
        Assert.False(Directory.Exists(Owned));
        Assert.Equal("preserve outside", File.ReadAllText(outside));
        Assert.False(WindowsFileRemoval.DeleteDirectoryIfExists(Owned));
    }

    [Fact]
    public void DirectoryLinkFailsPreflightWithoutDeletingAnyCapturedFile()
    {
        if (!OperatingSystem.IsWindows()) return;
        string outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "data.bin"), "outside");
        File.WriteAllText(Path.Combine(Owned, "owned.bin"), "owned");
        Directory.CreateSymbolicLink(Path.Combine(Owned, "directory-link"), outside);
        Assert.Throws<UnauthorizedAccessException>(() => WindowsFileRemoval.DeleteDirectoryIfExists(Owned));
        Assert.Equal("owned", File.ReadAllText(Path.Combine(Owned, "owned.bin")));
        Assert.Equal("outside", File.ReadAllText(Path.Combine(outside, "data.bin")));
    }

    [Fact]
    public void BusyFileFailsBeforeAnyDeletionAndCanBeRetriedAfterRelease()
    {
        if (!OperatingSystem.IsWindows()) return;
        string ordinary = Path.Combine(Owned, "ordinary.txt"), busy = Path.Combine(Owned, "busy.txt");
        File.WriteAllText(ordinary, "ordinary");
        File.WriteAllText(busy, "busy");
        using (var held = new FileStream(busy, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.Throws<IOException>(() => WindowsFileRemoval.DeleteDirectoryIfExists(Owned));
            Assert.Equal("ordinary", File.ReadAllText(ordinary));
            Assert.Equal("busy", File.ReadAllText(busy));
        }
        Assert.True(WindowsFileRemoval.DeleteDirectoryIfExists(Owned));
    }

    [Fact]
    public void ExcessiveDepthFailsPreflightWithoutRemovingOwnedData()
    {
        if (!OperatingSystem.IsWindows()) return;
        Directory.CreateDirectory(Path.Combine(Owned, string.Join(Path.DirectorySeparatorChar, Enumerable.Repeat("d", 65))));
        File.WriteAllText(Path.Combine(Owned, "keep.txt"), "keep");
        Assert.Throws<InvalidDataException>(() => WindowsFileRemoval.DeleteDirectoryIfExists(Owned));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(Owned, "keep.txt")));
    }

    [Fact]
    public void FileRemovalUnlinksOnlyTheSelectedAlias()
    {
        if (!OperatingSystem.IsWindows()) return;
        string outside = Path.Combine(_root, "outside.txt"), alias = Path.Combine(Owned, "alias.txt");
        File.WriteAllText(outside, "keep");
        Assert.True(CreateHardLinkW(alias, outside, IntPtr.Zero));
        Assert.True(WindowsFileRemoval.DeleteFileIfExists(alias));
        Assert.False(File.Exists(alias));
        Assert.Equal("keep", File.ReadAllText(outside));
        Assert.False(WindowsFileRemoval.DeleteFileIfExists(alias));
    }

    [Fact]
    public void VolumeRootIsRefusedBeforeOpeningOrDeletingIt()
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.Throws<UnauthorizedAccessException>(() => WindowsFileRemoval.DeleteDirectoryIfExists(Path.GetPathRoot(_root)!));
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string path, string target, IntPtr attributes);
}
