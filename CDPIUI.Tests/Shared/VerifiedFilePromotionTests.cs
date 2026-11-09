using CDPIUI.Shared.Security;
using System.Security.AccessControl;
using System.Security.Principal;

namespace CDPIUI.Tests.Shared;

[Collection("Windows path leases")]
public sealed class VerifiedFilePromotionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cdpiui-promotion-" + Guid.NewGuid().ToString("N"));
    private string Source => Path.Combine(_root, "source");
    private string Target => Path.Combine(_root, "target");
    public VerifiedFilePromotionTests()
    {
        Directory.CreateDirectory(Source);
        Directory.CreateDirectory(Target);
    }
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void BusyLaterOriginalAbortsBeforeAnyInstalledReplacement()
    {
        if (!OperatingSystem.IsWindows()) return;
        File.WriteAllText(Path.Combine(Source, "first.bin"), "new-first");
        File.WriteAllText(Path.Combine(Source, "second.bin"), "new-second");
        File.WriteAllText(Path.Combine(Target, "first.bin"), "old-first");
        string second = Path.Combine(Target, "second.bin");
        File.WriteAllText(second, "old-second");
        using (var busy = File.Open(second, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var snapshot = ReadOnlyDirectorySnapshot.Capture(Source))
            Assert.Throws<IOException>(() => VerifiedFilePromotion.Publish(snapshot, Target));
        Assert.Equal("old-first", File.ReadAllText(Path.Combine(Target, "first.bin")));
        Assert.Equal("old-second", File.ReadAllText(second));
        Assert.Equal(2, Directory.GetFiles(Target).Length);
    }

    [Fact]
    public void SymbolicLaterTargetCannotReceiveVerifiedBytesOrCausePartialReplacement()
    {
        if (!OperatingSystem.IsWindows()) return;
        File.WriteAllText(Path.Combine(Source, "first.bin"), "new-first");
        File.WriteAllText(Path.Combine(Source, "second.bin"), "new-second");
        File.WriteAllText(Path.Combine(Target, "first.bin"), "old-first");
        string outside = Path.Combine(_root, "outside.bin");
        File.WriteAllText(outside, "keep");
        File.CreateSymbolicLink(Path.Combine(Target, "second.bin"), outside);
        using (var snapshot = ReadOnlyDirectorySnapshot.Capture(Source))
            Assert.Throws<UnauthorizedAccessException>(() => VerifiedFilePromotion.Publish(snapshot, Target));
        Assert.Equal("old-first", File.ReadAllText(Path.Combine(Target, "first.bin")));
        Assert.Equal("keep", File.ReadAllText(outside));
        Assert.Equal(2, Directory.GetFiles(Target).Length);
    }

    [Fact]
    public void AllInstalledOriginalsRemainHeldUntilTheBatchHasFinished()
    {
        if (!OperatingSystem.IsWindows()) return;
        File.WriteAllText(Path.Combine(Source, "first.bin"), "new-first");
        File.WriteAllText(Path.Combine(Source, "second.bin"), "new-second");
        File.WriteAllText(Path.Combine(Target, "first.bin"), "old-first");
        string second = Path.Combine(Target, "second.bin");
        File.WriteAllText(second, "old-second");
        int called = 0;
        using (var snapshot = ReadOnlyDirectorySnapshot.Capture(Source))
            VerifiedFilePromotion.Publish(snapshot, Target, relative =>
            {
                called++;
                Assert.Throws<IOException>(() => File.WriteAllText(second, "substituted"));
                Assert.Throws<IOException>(() => Directory.Move(Target, Target + "-moved"));
            });
        Assert.Equal(2, called);
        Assert.Equal("new-first", File.ReadAllText(Path.Combine(Target, "first.bin")));
        Assert.Equal("new-second", File.ReadAllText(second));
        Assert.Equal(2, Directory.GetFiles(Target).Length);
    }

    [Fact]
    public void FailureAfterAReplacementRestoresEveryOriginalAndRemovesNewFiles()
    {
        if (!OperatingSystem.IsWindows()) return;
        File.WriteAllText(Path.Combine(Source, "first.bin"), "new-first");
        File.WriteAllText(Path.Combine(Source, "second.bin"), "new-second");
        File.WriteAllText(Path.Combine(Target, "first.bin"), "old-first");
        File.WriteAllText(Path.Combine(Target, "second.bin"), "old-second");
        using var snapshot = ReadOnlyDirectorySnapshot.Capture(Source);
        var failure = new InvalidOperationException("Consumer failed after publishing");
        int published = 0;

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => VerifiedFilePromotion.Publish(snapshot, Target, _ =>
        {
            if (++published == 2) throw failure;
        })));

        Assert.Equal(2, published);
        Assert.Equal("old-first", File.ReadAllText(Path.Combine(Target, "first.bin")));
        Assert.Equal("old-second", File.ReadAllText(Path.Combine(Target, "second.bin")));
        Assert.Equal(2, Directory.GetFiles(Target).Length);
    }

    [Fact]
    public void FailureAfterPublishingANewFileRemovesItWithoutTouchingUnrelatedData()
    {
        if (!OperatingSystem.IsWindows()) return;
        File.WriteAllText(Path.Combine(Source, "new.bin"), "new");
        File.WriteAllText(Path.Combine(Target, "unrelated.bin"), "keep");
        using var snapshot = ReadOnlyDirectorySnapshot.Capture(Source);
        Assert.Throws<InvalidOperationException>(() => VerifiedFilePromotion.Publish(snapshot, Target,
            _ => throw new InvalidOperationException("Stop")));
        Assert.Equal(["unrelated.bin"], Directory.GetFiles(Target).Select(Path.GetFileName));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(Target, "unrelated.bin")));
    }

    [Fact]
    public void LateUnrelatedDestinationIsPreservedAndEarlierFilesAreRolledBack()
    {
        if (!OperatingSystem.IsWindows()) return;
        File.WriteAllText(Path.Combine(Source, "first.bin"), "new-first");
        File.WriteAllText(Path.Combine(Source, "second.bin"), "new-second");
        File.WriteAllText(Path.Combine(Target, "first.bin"), "old-first");
        using var snapshot = ReadOnlyDirectorySnapshot.Capture(Source);
        string late = Path.Combine(Target, "second.bin");
        Assert.Throws<IOException>(() => VerifiedFilePromotion.Publish(snapshot, Target, relative =>
        {
            if (relative == "first.bin") File.WriteAllText(late, "late-unrelated");
        }));
        Assert.Equal("old-first", File.ReadAllText(Path.Combine(Target, "first.bin")));
        Assert.Equal("late-unrelated", File.ReadAllText(late));
        Assert.Equal(2, Directory.GetFiles(Target).Length);
    }

    [Fact]
    public void ReplacementPreservesTheOriginalProtectedDacl()
    {
        if (!OperatingSystem.IsWindows()) return;
        File.WriteAllText(Path.Combine(Source, "data.bin"), "new");
        string destination = Path.Combine(Target, "data.bin");
        File.WriteAllText(destination, "old");
        var info = new FileInfo(destination);
        var security = new FileSecurity();
        security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.FullControl, AccessControlType.Allow));
        info.SetAccessControl(security);
        using (var snapshot = ReadOnlyDirectorySnapshot.Capture(Source)) VerifiedFilePromotion.Publish(snapshot, Target);
        Assert.Equal("new", File.ReadAllText(destination));
        var actual = new FileInfo(destination).GetAccessControl();
        Assert.True(actual.AreAccessRulesProtected);
        var rule = Assert.Single(actual.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>());
        Assert.Equal(WindowsIdentity.GetCurrent().User, rule.IdentityReference);
        Assert.Equal(FileSystemRights.FullControl, rule.FileSystemRights);
        Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
        Assert.Equal(InheritanceFlags.None, rule.InheritanceFlags);
        Assert.False(rule.IsInherited);
        Assert.Single(Directory.GetFiles(Target));
    }

    [Fact]
    public void ReadOnlyOriginalAbortsPreparationWithoutChangingEarlierFiles()
    {
        if (!OperatingSystem.IsWindows()) return;
        File.WriteAllText(Path.Combine(Source, "first.bin"), "new-first");
        File.WriteAllText(Path.Combine(Source, "second.bin"), "new-second");
        File.WriteAllText(Path.Combine(Target, "first.bin"), "old-first");
        string readOnly = Path.Combine(Target, "second.bin");
        File.WriteAllText(readOnly, "old-second");
        File.SetAttributes(readOnly, FileAttributes.ReadOnly);
        try
        {
            using var snapshot = ReadOnlyDirectorySnapshot.Capture(Source);
            Assert.Throws<UnauthorizedAccessException>(() => VerifiedFilePromotion.Publish(snapshot, Target));
            Assert.Equal("old-first", File.ReadAllText(Path.Combine(Target, "first.bin")));
            Assert.Equal("old-second", File.ReadAllText(readOnly));
            Assert.Equal(2, Directory.GetFiles(Target).Length);
        }
        finally { File.SetAttributes(readOnly, FileAttributes.Normal); }
    }

    [Fact]
    public void RollbackFailureKeepsOriginalBackupAndReportsItsRecoveryPath()
    {
        if (!OperatingSystem.IsWindows()) return;
        File.WriteAllText(Path.Combine(Source, "data.bin"), "new");
        string destination = Path.Combine(Target, "data.bin");
        File.WriteAllText(destination, "old");
        try
        {
            using var snapshot = ReadOnlyDirectorySnapshot.Capture(Source);
            var failure = Assert.Throws<AggregateException>(() => VerifiedFilePromotion.Publish(snapshot, Target, _ =>
            {
                File.SetAttributes(destination, FileAttributes.ReadOnly);
                throw new InvalidOperationException("Stop after replacement");
            }));
            string backup = Assert.Single(Directory.GetFiles(Target, "*.bak"));
            Assert.Equal("old", File.ReadAllText(backup));
            Assert.Equal("new", File.ReadAllText(destination));
            Assert.Contains(backup, failure.ToString());
        }
        finally { File.SetAttributes(destination, FileAttributes.Normal); }
    }

    [Fact]
    public void PublishesNestedFilesAndEmptyDirectoriesAndReleasesEveryHandle()
    {
        if (!OperatingSystem.IsWindows()) return;
        Directory.CreateDirectory(Path.Combine(Source, "nested", "empty"));
        File.WriteAllText(Path.Combine(Source, "nested", "data.bin"), "verified");
        using (var snapshot = ReadOnlyDirectorySnapshot.Capture(Source))
            VerifiedFilePromotion.Publish(snapshot, Target + Path.DirectorySeparatorChar);
        Assert.Equal("verified", File.ReadAllText(Path.Combine(Target, "nested", "data.bin")));
        Assert.True(Directory.Exists(Path.Combine(Target, "nested", "empty")));
        Assert.Single(Directory.GetFiles(Target, "*", SearchOption.AllDirectories));
        Directory.Move(Target, Target + "-released");
    }
}
