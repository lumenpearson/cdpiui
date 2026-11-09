using CDPIUI.Shared.Basic.Filesystem;
using System.Runtime.InteropServices;
using CDPIUI.Shared.Security;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace CDPIUI.Tests.Shared;

[Collection("Windows path leases")]
public sealed class FileSystemWriteSafetyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cdpiui-file-write-" + Guid.NewGuid().ToString("N"));
    public FileSystemWriteSafetyTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public async Task FailedPreparationKeepsOriginalBytesAndDeletesOnlyItsHeldTemporary()
    {
        if (!OperatingSystem.IsWindows()) return;
        string destination = Path.Combine(_root, "data.bin");
        File.WriteAllText(destination, "old");
        var failure = new InvalidOperationException("Writer failed");
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => WindowsAtomicFile.WriteAsync(destination, stream =>
        {
            stream.WriteByte(42);
            throw failure;
        })));
        Assert.Equal("old", File.ReadAllText(destination));
        Assert.Single(Directory.GetFiles(_root));
    }

    [Fact]
    public async Task CanceledPreparationLeavesNoNewDestinationOrTemporary()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        string destination = Path.Combine(_root, "data.bin");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WindowsAtomicFile.WriteAsync(destination,
            _ => Task.FromCanceled(cancellation.Token)));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root));
    }

    [Fact]
    public async Task PendingPreparationPinsTheParentAndOriginalAcrossAwait()
    {
        if (!OperatingSystem.IsWindows()) return;
        string directory = Path.Combine(_root, "parent");
        Directory.CreateDirectory(directory);
        string destination = Path.Combine(directory, "data.bin");
        File.WriteAllText(destination, "old");
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task write = WindowsAtomicFile.WriteAsync(destination, async stream =>
        {
            stream.Write(Encoding.UTF8.GetBytes("new"));
            entered.SetResult();
            await release.Task;
        });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("old", File.ReadAllText(destination));
            Assert.Throws<IOException>(() => File.WriteAllText(destination, "substituted"));
            Assert.Throws<IOException>(() => Directory.Move(directory, directory + "-moved"));
        }
        finally { release.TrySetResult(); await write; }
        Assert.Equal("new", File.ReadAllText(destination));
        Assert.Single(Directory.GetFiles(directory));
    }

    [Fact]
    public async Task LateSymbolicDestinationIsReplacedAsANameWithoutWritingItsTarget()
    {
        if (!OperatingSystem.IsWindows()) return;
        string outside = Path.Combine(_root, "outside.bin");
        File.WriteAllText(outside, "keep");
        string destination = Path.Combine(_root, "data.bin");
        await WindowsAtomicFile.WriteAsync(destination, stream =>
        {
            stream.Write(Encoding.UTF8.GetBytes("new"));
            File.CreateSymbolicLink(destination, outside);
            return Task.CompletedTask;
        });
        Assert.Equal("new", File.ReadAllText(destination));
        Assert.Equal("keep", File.ReadAllText(outside));
        Assert.Null(new FileInfo(destination).LinkTarget);
        Assert.Equal(2, Directory.GetFiles(_root).Length);
    }

    [Fact]
    public void AtomicTextPreservesProtectedPermissionsAndUtf8WithoutBom()
    {
        if (!OperatingSystem.IsWindows()) return;
        string destination = Path.Combine(_root, "data.txt");
        File.WriteAllText(destination, "old");
        var security = new FileSecurity();
        security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.FullControl, AccessControlType.Allow));
        new FileInfo(destination).SetAccessControl(security);
        FileSystemService.WriteAllTextAtomic(destination, "Привет");
        Assert.Equal(new byte[] { 0xD0, 0x9F, 0xD1, 0x80, 0xD0, 0xB8, 0xD0, 0xB2, 0xD0, 0xB5, 0xD1, 0x82 }, File.ReadAllBytes(destination));
        var actual = new FileInfo(destination).GetAccessControl();
        Assert.True(actual.AreAccessRulesProtected);
        var rule = Assert.Single(actual.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>());
        Assert.Equal(WindowsIdentity.GetCurrent().User, rule.IdentityReference);
        Assert.Equal(FileSystemRights.FullControl, rule.FileSystemRights);
        Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
        Assert.Single(Directory.GetFiles(_root));
    }

    [Fact]
    public async Task CopyPublishesCompleteBytesAndSamePathIsANoOp()
    {
        if (!OperatingSystem.IsWindows()) return;
        string source = Path.Combine(_root, "source.bin");
        string destination = Path.Combine(_root, "destination.bin");
        byte[] bytes = Enumerable.Range(0, 100000).Select(i => (byte)(i % 251)).ToArray();
        File.WriteAllBytes(source, bytes);
        File.WriteAllText(destination, "old");
        await FileSystemService.CopyFileAsync(source, destination);
        await FileSystemService.CopyFileAsync(source, source);
        Assert.Equal(bytes, File.ReadAllBytes(source));
        Assert.Equal(bytes, File.ReadAllBytes(destination));
        Assert.Equal(2, Directory.GetFiles(_root).Length);
    }

    [Fact]
    public void WriteProbeLeavesNoArtifactsAndDoesNotCreateMissingDirectories()
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.True(DirectoryWritePermissionCheckService.HasWritePermission(_root).Success);
        string missing = Path.Combine(_root, "missing", "nested");
        Assert.False(DirectoryWritePermissionCheckService.HasWritePermission(missing).Success);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root));
    }

    [Fact]
    public void AtomicTextRefusesALinkedParentWithoutWritingOutside()
    {
        if (!OperatingSystem.IsWindows()) return;
        string outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "data.txt"), "keep");
        string link = Path.Combine(_root, "link");
        Directory.CreateSymbolicLink(link, outside);
        Assert.ThrowsAny<Exception>(() => FileSystemService.WriteAllTextAtomic(Path.Combine(link, "data.txt"), "changed"));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(outside, "data.txt")));
        Assert.Single(Directory.GetFiles(outside));
    }

    [Fact]
    public async Task CopyRefusesAHardLinkedDestinationWithoutTruncatingEitherName()
    {
        if (!OperatingSystem.IsWindows()) return;
        string source = Path.Combine(_root, "source.bin");
        string outside = Path.Combine(_root, "outside.bin");
        string destination = Path.Combine(_root, "destination.bin");
        File.WriteAllText(source, "new");
        File.WriteAllText(outside, "keep");
        Assert.True(CreateHardLinkW(destination, outside, IntPtr.Zero));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => FileSystemService.CopyFileAsync(source, destination));
        Assert.Equal("keep", File.ReadAllText(outside));
        Assert.Equal("keep", File.ReadAllText(destination));
    }

    [Fact]
    public async Task CopyRefusesASymbolicSourceWithoutExportingItsTarget()
    {
        if (!OperatingSystem.IsWindows()) return;
        string outside = Path.Combine(_root, "outside.bin");
        File.WriteAllText(outside, "private");
        string source = Path.Combine(_root, "source.bin");
        File.CreateSymbolicLink(source, outside);
        string destination = Path.Combine(_root, "destination.bin");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => FileSystemService.CopyFileAsync(source, destination));
        Assert.False(File.Exists(destination));
        Assert.Equal("private", File.ReadAllText(outside));
    }

    [Fact]
    public void WriteProbeRefusesLinkedDirectoriesAndLeavesNoFiles()
    {
        if (!OperatingSystem.IsWindows()) return;
        string outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outside);
        string link = Path.Combine(_root, "link");
        Directory.CreateSymbolicLink(link, outside);
        Assert.False(DirectoryWritePermissionCheckService.HasWritePermission(link).Success);
        Assert.Empty(Directory.GetFiles(outside));
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string name, string existing, IntPtr security);
}
