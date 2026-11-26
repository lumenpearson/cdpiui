using System.IO.Compression;
using CDPIUI.Core.Store.Repository;

namespace CDPIUI.Tests.Core.Store;

public class StoreTemporaryArchiveTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProbeRemovesPartialArchiveWhenTransferStops(bool cancel)
    {
        using var scratch = new ArchiveScratchDirectory();
        Exception interrupted = cancel
            ? new OperationCanceledException("The fixture cancelled the transfer.")
            : new IOException("The fixture interrupted the transfer.");

        Exception? actual = await Record.ExceptionAsync(() => StoreRepositoryDownloader.WithTemporaryArchiveAsync(
            scratch.Path,
            async (_, openDestination) =>
            {
                await openDestination().WriteAsync("partial archive"u8.ToArray());
                throw interrupted;
            }));

        Assert.Same(interrupted, actual);
        Assert.Empty(Directory.EnumerateFileSystemEntries(scratch.Path));
    }

    [Fact]
    public async Task SuccessfulTransferReadsTheArchiveFromTheBeginningAndRemovesIt()
    {
        using var scratch = new ArchiveScratchDirectory();
        using var fixture = new MemoryStream();
        using (var zip = new ZipArchive(fixture, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(zip.CreateEntry("main/init.json").Open());
            writer.Write("{}");
        }

        string? content = null;
        await StoreRepositoryDownloader.WithTemporaryArchiveAsync(
            scratch.Path,
            async (_, openDestination) => await openDestination().WriteAsync(fixture.ToArray()),
            archiveStream =>
            {
                Assert.Equal(0L, archiveStream.Position);
                using var zip = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: true);
                using var reader = new StreamReader(Assert.Single(zip.Entries).Open());
                content = reader.ReadToEnd();
            });

        Assert.Equal("{}", content);
        Assert.Empty(Directory.EnumerateFileSystemEntries(scratch.Path));
    }

    private sealed class ArchiveScratchDirectory : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("cdpiui-repo-archive-tests-");

        public string Path => _directory.FullName;

        public void Dispose()
        {
            foreach (FileInfo file in _directory.EnumerateFiles())
                file.Delete();
            _directory.Delete();
        }
    }
}
