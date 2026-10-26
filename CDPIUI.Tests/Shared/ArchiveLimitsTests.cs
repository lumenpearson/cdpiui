using System.IO.Compression;
using CDPIUI.Shared.Basic.Filesystem;

namespace CDPIUI.Tests.Shared;

public sealed class ArchiveLimitsTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "cdpiui-limits-" + Guid.NewGuid().ToString("N"));

    public ArchiveLimitsTests() => Directory.CreateDirectory(directory);

    public void Dispose() => Directory.Delete(directory, true);

    private string CreateArchive(Action<ZipArchive> fill)
    {
        string path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".zip");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            fill(archive);
        return path;
    }

    private static void WriteEntry(ZipArchive archive, string name, byte[] content)
    {
        using var stream = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
        stream.Write(content);
    }

    [Fact]
    public void NormalArchiveIsAccepted()
    {
        string path = CreateArchive(archive => WriteEntry(archive, "bin/winws.exe", new byte[4096]));
        using var archive = ZipFile.OpenRead(path);
        ArchivePaths.EnsureWithinLimits(archive.Entries);
    }

    [Fact]
    public void HighlyCompressedEntryIsRejected()
    {
        // 64 MiB of zeros compress to a few kilobytes.
        string path = CreateArchive(archive => WriteEntry(archive, "bomb.bin", new byte[64 << 20]));
        using var archive = ZipFile.OpenRead(path);
        Assert.Throws<InvalidDataException>(() => ArchivePaths.EnsureWithinLimits(archive.Entries));
    }

    [Fact]
    public void TooManyEntriesAreRejected()
    {
        string path = CreateArchive(archive =>
        {
            for (int index = 0; index <= ArchivePaths.MaxEntries; index++)
                archive.CreateEntry($"f{index}");
        });
        using var archive = ZipFile.OpenRead(path);
        Assert.Throws<InvalidDataException>(() => ArchivePaths.EnsureWithinLimits(archive.Entries));
    }

    [Fact]
    public void BoundedExtractionWritesTheEntry()
    {
        byte[] content = [1, 2, 3, 4, 5];
        string path = CreateArchive(archive => WriteEntry(archive, "a.bin", content));
        string destination = Path.Combine(directory, "a.bin");
        using (var archive = ZipFile.OpenRead(path))
            ArchivePaths.ExtractToFileBounded(archive.Entries[0], destination);
        Assert.Equal(content, File.ReadAllBytes(destination));
    }

    [Fact]
    public void LinkedFolderInsideRootIsRejected()
    {
        string root = Path.Combine(directory, "root");
        string outside = Path.Combine(directory, "outside");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(root, "lists"), outside);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Creating links needs a privilege on Windows without developer mode.
            return;
        }

        Assert.Throws<InvalidDataException>(() =>
            ArchivePaths.EnsureNoReparsePoints(root, Path.Combine(root, "lists", "list.txt")));
    }

    [Fact]
    public void PlainFoldersPassTheLinkCheck()
    {
        string root = Path.Combine(directory, "root");
        Directory.CreateDirectory(Path.Combine(root, "lists"));
        ArchivePaths.EnsureNoReparsePoints(root, Path.Combine(root, "lists", "list.txt"));
    }
}
