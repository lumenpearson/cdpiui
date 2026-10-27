using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using CDPIUI.AddOns.ConfigShare;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.JSON;

namespace CDPIUI.Tests.AddOns;

public sealed class ConfigShareReadTests : IDisposable
{
    private const string HostsPath = "resources/lists/hosts.txt";
    private const string HostsText = "example.com\n";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "cdpiui-share-read-" + Guid.NewGuid().ToString("N"));

    public ConfigShareReadTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, true);

    [Fact]
    public void ValidPackageIsExtractedAndReferencesPointToItsFiles()
    {
        string archivePath = CreatePackage(CreateManifest());

        using ConfigSharePackage package = new ConfigShareService().Read(archivePath);

        string extracted = Path.Combine([package.DirectoryPath, .. HostsPath.Split('/')]);
        Assert.Equal(HostsText, File.ReadAllText(extracted));
        Assert.Equal(package.DirectoryPath, package.Config.packId);
        Assert.Null(package.Config.file_name);
        Assert.DoesNotContain("share://", package.Config.startup_string);
        Assert.Contains(extracted.Replace('\\', '/'), package.Config.startup_string);
        Assert.NotNull(package.Config.jparams);
        Assert.NotNull(package.Config.variables);
    }

    [Fact]
    public void UnsupportedVersionIsRejected()
    {
        var manifest = CreateManifest();
        manifest.Version = 2;

        AssertRejected(CreatePackage(manifest), "SHARE_VERSION_UNSUPPORTED");
    }

    [Fact]
    public void ChecksumMismatchIsRejected()
    {
        var manifest = CreateManifest();
        manifest.Resources[0].Sha256 = new string('0', 64);

        AssertRejected(CreatePackage(manifest), "SHARE_CHECKSUM_FAILED");
    }

    [Fact]
    public void ResourceOutsideTheResourcesFolderIsRejected()
    {
        var manifest = CreateManifest();
        manifest.Resources[0].Path = "lists/hosts.txt";

        AssertRejected(CreatePackage(manifest, resourcePath: "lists/hosts.txt"), "SHARE_ARCHIVE_INVALID");
    }

    [Fact]
    public void UnlistedEntryIsRejected()
    {
        AssertRejected(CreatePackage(CreateManifest(), extraEntry: "resources/extra.txt"), "SHARE_ARCHIVE_INVALID");
    }

    [Fact]
    public void SymbolicLinkEntryIsRejected()
    {
        AssertRejected(CreatePackage(CreateManifest(), resourceAttributes: 0xA1FF << 16), "SHARE_ARCHIVE_INVALID");
    }

    [Fact]
    public void ArchiveWithoutManifestIsRejected()
    {
        AssertRejected(CreatePackage(manifest: null), "SHARE_ARCHIVE_INVALID");
    }

    [Fact]
    public void ManifestWithoutComponentIsRejected()
    {
        var manifest = CreateManifest();
        manifest.Config.target = [];

        AssertRejected(CreatePackage(manifest), "SHARE_ARCHIVE_INVALID");
    }

    [Fact]
    public void EntryOverTheSizeLimitIsRejectedBeforeItIsRead()
    {
        // Only the declared size is large: the reader must reject it without reading the entry.
        string archivePath = CreatePackage(CreateManifest());
        SetDeclaredLength(archivePath, HostsPath, ConfigShareService.MaxEntryBytes + 1, compressed: false);

        AssertRejected(archivePath, "SHARE_TOO_LARGE");
    }

    [Fact]
    public void PackageOverTheTotalLimitIsRejectedBeforeItIsRead()
    {
        string[] paths = ["resources/a.txt", "resources/b.txt", "resources/c.txt"];
        string archivePath = CreatePackage(CreateManifest(paths), paths);
        // Each entry is in the entry limit, and the ratio is 1, but together they are too large.
        long length = ConfigShareService.MaxTotalBytes / 3 + 1;
        foreach (string path in paths)
            SetDeclaredLength(archivePath, path, length, compressed: true);

        AssertRejected(archivePath, "SHARE_TOO_LARGE");
    }

    private static void AssertRejected(string archivePath, string code)
    {
        var exception = Assert.Throws<ConfigShareException>(() => new ConfigShareService().Read(archivePath));
        Assert.Equal(code, exception.Code);
    }

    private static ConfigShareManifest CreateManifest(params string[] paths)
    {
        paths = paths.Length == 0 ? [HostsPath] : paths;
        return new()
        {
            Name = "Shared",
            Developer = "Tester",
            Config = new ConfigItem
            {
                target = ["component"],
                startup_string = string.Join(' ', paths.Select(path => "--hostlist=share://" + path)),
            },
            Resources = paths.Select(path => new ConfigShareResource
            {
                Path = path,
                Length = Encoding.UTF8.GetByteCount(HostsText),
                Sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(HostsText))),
            }).ToList(),
        };
    }

    private string CreatePackage(ConfigShareManifest manifest, string[] resourcePaths)
    {
        string archivePath = Path.Combine(_directory, "preset" + ConfigShareService.Extension);
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        WriteEntry(archive, "manifest.json", JSONConvertor.SerializeObject(manifest));
        foreach (string path in resourcePaths)
            WriteEntry(archive, path, HostsText);
        return archivePath;
    }

    /// <summary>
    /// Changes the sizes that the central directory declares for an entry; the data stays small.
    /// </summary>
    private static void SetDeclaredLength(string archivePath, string entryName, long length, bool compressed)
    {
        byte[] bytes = File.ReadAllBytes(archivePath);
        byte[] name = Encoding.UTF8.GetBytes(entryName);
        for (int offset = 0; offset + 46 <= bytes.Length; offset++)
        {
            if (BitConverter.ToUInt32(bytes, offset) != 0x02014b50 ||
                BitConverter.ToUInt16(bytes, offset + 28) != name.Length ||
                !bytes.AsSpan(offset + 46, name.Length).SequenceEqual(name))
                continue;
            BitConverter.GetBytes((uint)length).CopyTo(bytes, offset + 24);
            if (compressed)
                BitConverter.GetBytes((uint)length).CopyTo(bytes, offset + 20);
            File.WriteAllBytes(archivePath, bytes);
            return;
        }
        throw new InvalidOperationException($"No central directory entry {entryName}.");
    }

    private string CreatePackage(
        ConfigShareManifest? manifest,
        string resourcePath = HostsPath,
        string? extraEntry = null,
        int resourceAttributes = 0)
    {
        string archivePath = Path.Combine(_directory, "preset" + ConfigShareService.Extension);
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        if (manifest != null)
            WriteEntry(archive, "manifest.json", JSONConvertor.SerializeObject(manifest));
        ZipArchiveEntry resource = WriteEntry(archive, resourcePath, HostsText);
        if (resourceAttributes != 0)
            resource.ExternalAttributes = resourceAttributes;
        if (extraEntry != null)
            WriteEntry(archive, extraEntry, "extra");
        return archivePath;
    }

    private static ZipArchiveEntry WriteEntry(ZipArchive archive, string name, string text)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name);
        using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
            writer.Write(text);
        return entry;
    }
}
