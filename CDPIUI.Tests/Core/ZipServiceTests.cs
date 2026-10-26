using System.IO.Compression;
using CDPIUI.Core.System;

namespace CDPIUI.Tests.Core;

public sealed class ZipServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "cdpiui-zip-" + Guid.NewGuid().ToString("N"));

    public void Dispose() => Directory.Delete(_directory, true);

    [Fact]
    public async Task DomainListsAreMergedByWholeLines()
    {
        string target = Path.Combine(_directory, "item");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "list.txt"), "reddit.com");

        string archivePath = Path.Combine(_directory, "pack.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(archive.CreateEntry("list.txt").Open()))
            writer.Write("t.co\nreddit.com\nyoutube.com\n");

        await ZipService.ExtractZip(archivePath, "/", target);

        Assert.Equal(["reddit.com", "t.co", "youtube.com"], File.ReadAllLines(Path.Combine(target, "list.txt")));
    }

    [Fact]
    public async Task DomainListsMergeIntoTheBackupOfALinkedList()
    {
        string target = Path.Combine(_directory, "item");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "list.txt"), "linked\n");
        File.WriteAllText(Path.Combine(target, "list.txt.bak"), "reddit.com\n");
        string archivePath = CreateArchive(("list.txt", "reddit.com\nt.co\n"));

        await ZipService.ExtractZip(archivePath, null, target);

        Assert.Equal(["linked"], File.ReadAllLines(Path.Combine(target, "list.txt")));
        Assert.Equal(["reddit.com", "t.co"], File.ReadAllLines(Path.Combine(target, "list.txt.bak")));
    }

    [Fact]
    public async Task FolderPatternSelectsEntriesAndAnyMatchesOneSegment()
    {
        string target = Path.Combine(_directory, "item");
        string archivePath = CreateArchive(
            ("pkg-1.0/bin/tool.exe", "tool"),
            ("PKG-2.0/BIN/sub/lib.dll", "lib"),
            ("pkg-1.0/doc/readme.md", "doc"),
            ("bin/root.exe", "root"));

        await ZipService.ExtractZip(archivePath, "$ANY/bin/", target);

        Assert.Equal("tool", File.ReadAllText(Path.Combine(target, "tool.exe")));
        Assert.Equal("lib", File.ReadAllText(Path.Combine(target, "sub", "lib.dll")));
        Assert.Equal(2, Directory.GetFiles(target, "*", SearchOption.AllDirectories).Length);
    }

    [Fact]
    public async Task SkippedFilesMatchByExtensionOrByPathPart()
    {
        string target = Path.Combine(_directory, "item");
        string archivePath = CreateArchive(
            ("readme.MD", "skip"),
            ("tests/case.txt", "skip"),
            ("keep.txt", "keep"));

        await ZipService.ExtractZip(archivePath, "/", target, ["*.md", "TEST"]);

        Assert.Equal(["keep.txt"], Directory.GetFiles(target, "*", SearchOption.AllDirectories).Select(Path.GetFileName));
    }

    [Fact]
    public async Task EntryOutsideTheTargetIsRejected()
    {
        string target = Path.Combine(_directory, "item");
        string archivePath = CreateArchive(("../evil.txt", "evil"));

        await Assert.ThrowsAsync<InvalidDataException>(() => ZipService.ExtractZip(archivePath, "/", target));
        Assert.False(File.Exists(Path.Combine(_directory, "evil.txt")));
    }

    private string CreateArchive(params (string Name, string Content)[] entries)
    {
        Directory.CreateDirectory(_directory);
        string archivePath = Path.Combine(_directory, "pack.zip");
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            using var writer = new StreamWriter(archive.CreateEntry(name).Open());
            writer.Write(content);
        }
        return archivePath;
    }
}
