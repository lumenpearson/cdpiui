using CDPIUI.Shared.Security;

namespace CDPIUI.Tests.Shared;

[Collection("Windows path leases")]
public sealed class WindowsDirectoryTransactionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cdpiui-directory-transaction-" + Guid.NewGuid().ToString("N"));
    private string Source => Path.Combine(_root, "source");
    private string Target => Path.Combine(_root, "target");
    public WindowsDirectoryTransactionTests()
    {
        Directory.CreateDirectory(Source);
        Directory.CreateDirectory(Target);
        File.WriteAllText(Path.Combine(Source, "new.bin"), "new");
        File.WriteAllText(Path.Combine(Target, "old.bin"), "old");
    }
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void RecoveryConflictNeverDeletesThePlantedDirectoryOrEitherHeldVersion()
    {
        if (!OperatingSystem.IsWindows()) return;
        var failure = Assert.Throws<AggregateException>(() => WindowsDirectoryTransaction.Replace(Source, Target, published: () =>
        {
            Directory.CreateDirectory(Source);
            File.WriteAllText(Path.Combine(Source, "unrelated.bin"), "keep");
            throw new InvalidOperationException("Stop");
        }));
        string backup = Assert.Single(Directory.GetDirectories(_root, "target.previous-*"));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(Source, "unrelated.bin")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(Target, "new.bin")));
        Assert.Equal("old", File.ReadAllText(Path.Combine(backup, "old.bin")));
        Assert.Contains(backup, failure.ToString());
        Assert.Contains(Target, failure.ToString());
    }

    [Fact]
    public void PreviousFileLinkIsUnlinkedWithoutChangingItsTarget()
    {
        if (!OperatingSystem.IsWindows()) return;
        string outside = Path.Combine(_root, "outside.bin");
        File.WriteAllText(outside, "keep");
        File.CreateSymbolicLink(Path.Combine(Target, "alias.bin"), outside);
        WindowsDirectoryTransaction.Replace(Source, Target);
        Assert.Equal("keep", File.ReadAllText(outside));
        Assert.Equal(["target"], Directory.GetDirectories(_root).Select(Path.GetFileName));
    }

    [Fact]
    public void MissingDestinationCanBePublishedAndAllParentAnchorsAreReleased()
    {
        if (!OperatingSystem.IsWindows()) return;
        Directory.Delete(Target, true);
        WindowsDirectoryTransaction.Replace(Source + Path.DirectorySeparatorChar, Target + Path.DirectorySeparatorChar);
        Assert.Equal("new", File.ReadAllText(Path.Combine(Target, "new.bin")));
        Assert.Empty(Directory.GetFiles(_root));
        Directory.Move(_root, _root + "-moved");
        Directory.Move(_root + "-moved", _root);
    }

    [Fact]
    public void VolumeAndShareRootsAreRejectedByPureNormalizationBeforeAnyLease()
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.Throws<UnauthorizedAccessException>(() => DirectoryNameLease.NormalizeNonRootPath(Path.GetPathRoot(_root)!));
        Assert.Throws<UnauthorizedAccessException>(() => DirectoryNameLease.NormalizeNonRootPath(@"\\cdpiui-nonexistent-server\share\"));
        Assert.Equal("new", File.ReadAllText(Path.Combine(Source, "new.bin")));
        Assert.Equal("old", File.ReadAllText(Path.Combine(Target, "old.bin")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AncestorLinksCannotRedirectEitherNamespaceMutation(bool sourceLink)
    {
        if (!OperatingSystem.IsWindows()) return;
        string linked = Path.Combine(_root, "linked");
        Directory.CreateSymbolicLink(linked, _root);
        Assert.Throws<UnauthorizedAccessException>(() => WindowsDirectoryTransaction.Replace(
            sourceLink ? Path.Combine(linked, "source") : Source,
            sourceLink ? Target : Path.Combine(linked, "target")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(Source, "new.bin")));
        Assert.Equal("old", File.ReadAllText(Path.Combine(Target, "old.bin")));
    }

    [Fact]
    public void ReplacesTheHeldDirectoryAndRemovesOnlyTheHeldPreviousTree()
    {
        if (!OperatingSystem.IsWindows()) return;
        WindowsDirectoryTransaction.Replace(Source, Target);
        Assert.False(Directory.Exists(Source));
        Assert.Equal("new", File.ReadAllText(Path.Combine(Target, "new.bin")));
        Assert.Equal(["new.bin"], Directory.GetFiles(Target).Select(Path.GetFileName));
        Assert.Equal(["target"], Directory.GetDirectories(_root).Select(Path.GetFileName));
        Assert.Empty(Directory.GetFiles(_root));
    }

    [Fact]
    public void BusySourceRestoresTheOriginalDirectoryAfterItsBackupRename()
    {
        if (!OperatingSystem.IsWindows()) return;
        using (var busy = File.OpenRead(Path.Combine(Source, "new.bin")))
            Assert.Throws<IOException>(() => WindowsDirectoryTransaction.Replace(Source, Target));
        Assert.Equal("old", File.ReadAllText(Path.Combine(Target, "old.bin")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(Source, "new.bin")));
        Assert.Equal(2, Directory.GetDirectories(_root).Length);
    }

    [Fact]
    public void PublicationCallbackFailureRestoresBothDirectories()
    {
        if (!OperatingSystem.IsWindows()) return;
        var failure = new InvalidOperationException("Consumer failed");
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => WindowsDirectoryTransaction.Replace(Source, Target,
            published: () => throw failure)));
        Assert.Equal("old", File.ReadAllText(Path.Combine(Target, "old.bin")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(Source, "new.bin")));
        Assert.Equal(2, Directory.GetDirectories(_root).Length);
    }

    [Fact]
    public void ActiveVerifiedSnapshotRefusesTheDirectorySwitchWithoutReleasingItsFiles()
    {
        if (!OperatingSystem.IsWindows()) return;
        using (var snapshot = ReadOnlyDirectorySnapshot.Capture(Source))
        {
            Assert.Throws<IOException>(() => WindowsDirectoryTransaction.Replace(Source, Target));
            Assert.Throws<IOException>(() => File.WriteAllText(Path.Combine(Source, "new.bin"), "changed"));
        }
        Assert.Equal("old", File.ReadAllText(Path.Combine(Target, "old.bin")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(Source, "new.bin")));
    }

    [Fact]
    public void CleanupRefusalRetainsTheExactPreviousRootAndReportsItsPath()
    {
        if (!OperatingSystem.IsWindows()) return;
        string outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "keep.bin"), "keep");
        Directory.CreateSymbolicLink(Path.Combine(Target, "linked-directory"), outside);
        string? retained = null;
        WindowsDirectoryTransaction.Replace(Source, Target, cleanupFailed: (path, error) =>
        {
            retained = path;
            Assert.IsType<UnauthorizedAccessException>(error);
        });
        Assert.NotNull(retained);
        Assert.Equal("old", File.ReadAllText(Path.Combine(retained, "old.bin")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(Target, "new.bin")));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(outside, "keep.bin")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RootLinksAreRejectedWithoutChangingEitherDirectory(bool sourceLink)
    {
        if (!OperatingSystem.IsWindows()) return;
        string linked = Path.Combine(_root, "linked");
        Directory.CreateSymbolicLink(linked, sourceLink ? Source : Target);
        Assert.Throws<UnauthorizedAccessException>(() => WindowsDirectoryTransaction.Replace(sourceLink ? linked : Source, sourceLink ? Target : linked));
        Assert.Equal("old", File.ReadAllText(Path.Combine(Target, "old.bin")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(Source, "new.bin")));
    }
}
