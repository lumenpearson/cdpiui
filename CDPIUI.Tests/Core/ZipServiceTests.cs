using System.IO.Compression;
using CDPIUI.Core.System;

namespace CDPIUI.Tests.Core;

public sealed class ZipServiceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "cdpiui-zip-" + Guid.NewGuid().ToString("N"));

    public void Dispose() => Directory.Delete(directory, true);

    [Fact]
    public async Task DomainListsAreMergedByWholeLines()
    {
        string target = Path.Combine(directory, "item");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "list.txt"), "reddit.com");

        string archivePath = Path.Combine(directory, "pack.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(archive.CreateEntry("list.txt").Open()))
            writer.Write("t.co\nreddit.com\nyoutube.com\n");

        await ZipService.ExtractZip(archivePath, "/", target);

        Assert.Equal(["reddit.com", "t.co", "youtube.com"], File.ReadAllLines(Path.Combine(target, "list.txt")));
    }
}
