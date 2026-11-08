using CDPIUI.Shared.Security;

namespace CDPIUI.Tests.Shared;

[Collection("Windows path leases")]
public sealed class ReadOnlyDirectorySnapshotTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cdpiui-snapshot-" + Guid.NewGuid().ToString("N"));
    public ReadOnlyDirectorySnapshotTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void CapturedBytesCannotChangeAndLaterFilesAreNotIncluded()
    {
        if (!OperatingSystem.IsWindows()) return;
        string directory = Path.Combine(_root, "sub");
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "a.bin");
        File.WriteAllText(file, "checked");
        using (var snapshot = ReadOnlyDirectorySnapshot.Capture(_root))
        {
            Assert.Throws<IOException>(() => File.WriteAllText(file, "changed"));
            Assert.Throws<IOException>(() => Directory.Move(directory, directory + "-moved"));
            File.WriteAllText(Path.Combine(_root, "late.bin"), "unverified");
            var captured = Assert.Single(snapshot.Files);
            Assert.Equal(Path.Combine("sub", "a.bin"), captured.RelativePath);
            using var reader = new StreamReader(captured.Stream, leaveOpen: true);
            Assert.Equal("checked", reader.ReadToEnd());
        }
        File.WriteAllText(file, "released");
    }

    [Fact]
    public void LinkedSubdirectoryIsRejectedAndAllHandlesAreReleased()
    {
        if (!OperatingSystem.IsWindows()) return;
        string target = Path.Combine(_root, "target");
        Directory.CreateDirectory(target);
        Directory.CreateSymbolicLink(Path.Combine(_root, "link"), target);
        Assert.Throws<UnauthorizedAccessException>(() => ReadOnlyDirectorySnapshot.Capture(_root));
        Directory.Move(_root, _root + "-moved");
        Directory.Move(_root + "-moved", _root);
    }

    [Fact]
    public void ExcessiveDepthIsRejectedAndAllHandlesAreReleased()
    {
        if (!OperatingSystem.IsWindows()) return;
        Directory.CreateDirectory(Path.Combine(_root, string.Join(Path.DirectorySeparatorChar, Enumerable.Repeat("d", 65))));
        Assert.Throws<InvalidDataException>(() => ReadOnlyDirectorySnapshot.Capture(_root));
        Directory.Move(_root, _root + "-moved");
        Directory.Move(_root + "-moved", _root);
    }
}
