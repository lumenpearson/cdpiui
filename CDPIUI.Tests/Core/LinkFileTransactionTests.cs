using CDPIUI.Core.Features;

namespace CDPIUI.Tests.Core;

public sealed class LinkFileTransactionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cdpiui-link-transaction-" + Guid.NewGuid().ToString("N"));
    private string Destination => Path.Combine(_root, "list.txt");

    public LinkFileTransactionTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

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
            path => File.CreateSymbolicLink(path, source));
        Assert.True(File.GetAttributes(Destination).HasFlag(FileAttributes.ReparsePoint));
        Assert.Equal("shared", File.ReadAllText(Destination));

        LinkFileTransaction.Restore(Destination, backup);

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
    public void FailedCreationCleansPartialTemporaryFile()
    {
        File.WriteAllText(Destination, "original");
        Assert.Throws<IOException>(() => LinkFileTransaction.Replace(Destination, false, path =>
        {
            File.WriteAllText(path, "partial");
            throw new IOException("Link creation failed.");
        }));
        Assert.Equal("original", File.ReadAllText(Destination));
        Assert.Empty(Directory.GetFiles(_root, "*.link"));
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
}
