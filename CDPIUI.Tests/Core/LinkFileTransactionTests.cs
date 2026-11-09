using CDPIUI.Core.Features;
using System.Runtime.InteropServices;

namespace CDPIUI.Tests.Core;

[Collection("Windows path leases")]
public sealed class LinkFileTransactionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cdpiui-link-transaction-" + Guid.NewGuid().ToString("N"));
    private string Destination => Path.Combine(_root, "list.txt");

    public LinkFileTransactionTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void InvalidDestinationIsRejectedBeforeCallingTheCreator()
    {
        if (!OperatingSystem.IsWindows()) return;
        bool called = false;
        Assert.ThrowsAny<Exception>(() => LinkFileTransaction.Replace(Destination + ":stream", false, _ =>
        {
            called = true;
            throw new IOException("The creator must not run for an alternate stream path.");
        }));
        Assert.False(called);
    }

    [Fact]
    public void StaleLinkMetadataCannotDiscardAUsersCurrentOrdinaryFile()
    {
        if (!OperatingSystem.IsWindows()) return;
        string previous = Path.Combine(_root, "previous-target.txt"), next = Path.Combine(_root, "next-target.txt");
        File.WriteAllText(previous, "previous shared data");
        File.WriteAllText(Destination, "new user content");
        Assert.Throws<UnauthorizedAccessException>(() => LinkFileTransaction.Replace(Destination, true,
            path => File.CreateSymbolicLink(path, next), next, symbolic: true, previousTarget: previous));
        Assert.Equal("new user content", File.ReadAllText(Destination));
        Assert.False(File.Exists(Destination + ".bak"));
    }

    [Fact]
    public void ParentCannotBeReplacedDuringLinkCreation()
    {
        if (!OperatingSystem.IsWindows()) return;
        string parent = Path.Combine(_root, "parent"), moved = Path.Combine(_root, "moved"), outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(parent);
        Directory.CreateDirectory(outside);
        string destination = Path.Combine(parent, "list.txt");
        File.WriteAllText(destination, "original");
        File.WriteAllText(Path.Combine(outside, "list.txt"), "outside");
        try
        {
            Assert.Throws<IOException>(() => LinkFileTransaction.Replace(destination, false, temporary =>
            {
                File.WriteAllText(temporary, "prepared");
                Directory.Move(parent, moved);
                Directory.CreateSymbolicLink(parent, outside);
                File.WriteAllText(Path.Combine(outside, Path.GetFileName(temporary)), "planted");
            }));
            Assert.Equal("outside", File.ReadAllText(Path.Combine(outside, "list.txt")));
        }
        finally
        {
            if (new DirectoryInfo(parent).LinkTarget != null)
            {
                Directory.Delete(parent);
                Directory.Move(moved, parent);
            }
        }
    }

    [Fact]
    public void PreparedSymbolicLinkMustMatchTheRequestedTarget()
    {
        if (!OperatingSystem.IsWindows()) return;
        string expected = Path.Combine(_root, "expected.txt"), other = Path.Combine(_root, "other.txt");
        File.WriteAllText(Destination, "original");
        Assert.Throws<UnauthorizedAccessException>(() => LinkFileTransaction.Replace(Destination, false,
            path =>
            {
                File.CreateSymbolicLink(path, expected);
                File.Delete(path);
                File.CreateSymbolicLink(path, other);
            }, expected, symbolic: true));
        Assert.Equal("original", File.ReadAllText(Destination));
        Assert.False(File.Exists(Destination + ".bak"));
        string kept = Assert.Single(Directory.GetFiles(_root, "*.link"));
        Assert.Equal(other, new FileInfo(kept).LinkTarget);
    }

    [Fact]
    public void RealHardLinkCanBeBoundPublishedAndRestored()
    {
        if (!OperatingSystem.IsWindows()) return;
        string source = Path.Combine(_root, "shared.txt");
        File.WriteAllText(source, "shared");
        File.WriteAllText(Destination, "original");
        string? backup = LinkFileTransaction.Replace(Destination, false,
            path => Assert.True(CreateHardLinkW(path, source, IntPtr.Zero)), source);
        Assert.Equal("shared", File.ReadAllText(Destination));
        LinkFileTransaction.Restore(Destination, backup, source);
        Assert.Equal("original", File.ReadAllText(Destination));
        Assert.Equal("shared", File.ReadAllText(source));
    }

    [Fact]
    public void RelativeSymbolicLinkKeepsItsTargetThroughPublicationAndRestore()
    {
        if (!OperatingSystem.IsWindows()) return;
        string source = Path.Combine(_root, "shared.txt");
        File.WriteAllText(source, "shared");
        File.WriteAllText(Destination, "original");
        string? backup = LinkFileTransaction.Replace(Destination, false,
            path => File.CreateSymbolicLink(path, "shared.txt"), "shared.txt", symbolic: true);
        Assert.Equal("shared", File.ReadAllText(Destination));
        LinkFileTransaction.Restore(Destination, backup, "shared.txt");
        Assert.Equal("original", File.ReadAllText(Destination));
        Assert.Equal("shared", File.ReadAllText(source));
    }

    [Fact]
    public void PreparedHardLinkCannotBeReplacedByAnAliasOfADifferentFile()
    {
        if (!OperatingSystem.IsWindows()) return;
        string source = Path.Combine(_root, "shared.txt"), other = Path.Combine(_root, "other.txt");
        File.WriteAllText(source, "same bytes");
        File.WriteAllText(other, "same bytes");
        File.WriteAllText(Destination, "original");
        Assert.Throws<UnauthorizedAccessException>(() => LinkFileTransaction.Replace(Destination, false, path =>
        {
            Assert.True(CreateHardLinkW(path, source, IntPtr.Zero));
            File.Delete(path);
            Assert.True(CreateHardLinkW(path, other, IntPtr.Zero));
        }, source));
        Assert.Equal("original", File.ReadAllText(Destination));
        Assert.False(File.Exists(Destination + ".bak"));
        Assert.Equal("same bytes", File.ReadAllText(source));
        Assert.Equal("same bytes", File.ReadAllText(other));
    }

    [Fact]
    public void RestoreDoesNotDeleteAUserFileThatReplacedTheLink()
    {
        if (!OperatingSystem.IsWindows()) return;
        string target = Path.Combine(_root, "target.txt");
        File.WriteAllText(target, "same bytes");
        File.WriteAllText(Destination, "same bytes");
        File.WriteAllText(Destination + ".bak", "original");
        Assert.Throws<UnauthorizedAccessException>(() => LinkFileTransaction.Restore(Destination, Destination + ".bak", target));
        Assert.Equal("same bytes", File.ReadAllText(Destination));
        Assert.Equal("original", File.ReadAllText(Destination + ".bak"));
    }

    [Fact]
    public void RestoreRejectsAnUnrelatedBackupPath()
    {
        if (!OperatingSystem.IsWindows()) return;
        string unrelated = Path.Combine(_root, "unrelated.txt");
        File.WriteAllText(unrelated, "unrelated data");
        File.WriteAllText(Destination, "link");
        Assert.Throws<UnauthorizedAccessException>(() => LinkFileTransaction.Restore(Destination, unrelated));
        Assert.Equal("unrelated data", File.ReadAllText(unrelated));
        Assert.Equal("link", File.ReadAllText(Destination));
    }

    [Fact]
    public void SuccessfulReplacementKeepsOriginalBackup()
    {
        File.WriteAllText(Destination, "original");
        string? backup = LinkFileTransaction.Replace(Destination, false, path => File.WriteAllText(path, "link"));

        Assert.Equal(Destination + ".bak", backup);
        Assert.Equal("original", File.ReadAllText(backup!));
        Assert.Equal("link", File.ReadAllText(Destination));
    }

    [Fact]
    public void RealSymbolicLinkCanBePublishedAndRestored()
    {
        string source = Path.Combine(_root, "shared.txt");
        File.WriteAllText(source, "shared");
        File.WriteAllText(Destination, "original");

        string? backup = LinkFileTransaction.Replace(Destination, false,
            path => File.CreateSymbolicLink(path, source), source, symbolic: true);
        Assert.True(File.GetAttributes(Destination).HasFlag(FileAttributes.ReparsePoint));
        Assert.Equal("shared", File.ReadAllText(Destination));

        LinkFileTransaction.Restore(Destination, backup, source);

        Assert.False(File.GetAttributes(Destination).HasFlag(FileAttributes.ReparsePoint));
        Assert.Equal("original", File.ReadAllText(Destination));
        Assert.Equal("shared", File.ReadAllText(source));
    }

    [Fact]
    public void ExistingBackupIsNotOverwritten()
    {
        File.WriteAllText(Destination, "current");
        File.WriteAllText(Destination + ".bak", "previous original");

        Assert.Throws<IOException>(() => LinkFileTransaction.Replace(Destination, false,
            path => File.WriteAllText(path, "link")));

        Assert.Equal("current", File.ReadAllText(Destination));
        Assert.Equal("previous original", File.ReadAllText(Destination + ".bak"));
        Assert.Empty(Directory.GetFiles(_root, "*.link"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedPublicationRestoresDisplacedFile(bool replacingLink)
    {
        if (!OperatingSystem.IsWindows())
            return; // The failure is a Windows rename sharing violation.

        File.WriteAllText(Destination, "current");
        if (replacingLink)
            File.WriteAllText(Destination + ".bak", "original");
        FileStream? held = null;
        try
        {
            Assert.Throws<IOException>(() => LinkFileTransaction.Replace(Destination, replacingLink, path =>
            {
                File.WriteAllText(path, "new link");
                held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            }));

            Assert.Equal("current", File.ReadAllText(Destination));
            if (replacingLink)
                Assert.Equal("original", File.ReadAllText(Destination + ".bak"));
            else
                Assert.False(File.Exists(Destination + ".bak"));
        }
        finally { held?.Dispose(); }
    }

    [Fact]
    public void FailedCreationKeepsUnconfirmedTemporaryFileOnWindows()
    {
        File.WriteAllText(Destination, "original");
        Assert.Throws<IOException>(() => LinkFileTransaction.Replace(Destination, false, path =>
        {
            File.WriteAllText(path, "partial");
            throw new IOException("Link creation failed.");
        }));
        Assert.Equal("original", File.ReadAllText(Destination));
        if (OperatingSystem.IsWindows())
            Assert.Equal("partial", File.ReadAllText(Assert.Single(Directory.GetFiles(_root, "*.link"))));
        else Assert.Empty(Directory.GetFiles(_root, "*.link"));
    }

    [Fact]
    public void RestorePublishesOriginalAndRemovesDisplacedLink()
    {
        File.WriteAllText(Destination, "link");
        File.WriteAllText(Destination + ".bak", "original");
        LinkFileTransaction.Restore(Destination, Destination + ".bak");
        Assert.Equal("original", File.ReadAllText(Destination));
        Assert.Single(Directory.EnumerateFileSystemEntries(_root));
    }

    [Fact]
    public void FailedRestoreKeepsWorkingLinkAndBackup()
    {
        if (!OperatingSystem.IsWindows())
            return;
        File.WriteAllText(Destination, "link");
        File.WriteAllText(Destination + ".bak", "original");
        using var held = new FileStream(Destination + ".bak", FileMode.Open, FileAccess.Read, FileShare.Read);

        Assert.Throws<IOException>(() => LinkFileTransaction.Restore(Destination, Destination + ".bak"));

        Assert.Equal("link", File.ReadAllText(Destination));
        Assert.Equal("original", File.ReadAllText(Destination + ".bak"));
        Assert.Empty(Directory.GetFiles(_root, "*.previous"));
    }

    [Fact]
    public void MissingBackupDoesNotRemoveWorkingLink()
    {
        File.WriteAllText(Destination, "link");
        Assert.Throws<FileNotFoundException>(() => LinkFileTransaction.Restore(Destination, Destination + ".bak"));
        Assert.Equal("link", File.ReadAllText(Destination));
    }

    [Fact]
    public void RestoreWithoutOriginalRemovesDanglingLink()
    {
        File.CreateSymbolicLink(Destination, Path.Combine(_root, "missing.txt"));
        LinkFileTransaction.Restore(Destination, null);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root));
    }

    [Fact]
    public void DanglingLinkCanBeReplacedWithoutLeavingDisplacedLink()
    {
        File.CreateSymbolicLink(Destination, Path.Combine(_root, "missing.txt"));

        Assert.Null(LinkFileTransaction.Replace(Destination, false, path => File.WriteAllText(path, "new link")));

        Assert.Equal("new link", File.ReadAllText(Destination));
        Assert.Single(Directory.EnumerateFileSystemEntries(_root));
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string name, string target, IntPtr security);
}
