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

    private static void AssertRejected(string archivePath, string code)
    {
        var exception = Assert.Throws<ConfigShareException>(() => new ConfigShareService().Read(archivePath));
        Assert.Equal(code, exception.Code);
    }

    private static ConfigShareManifest CreateManifest() => new()
    {
        Name = "Shared",
        Developer = "Tester",
        Config = new ConfigItem
        {
            target = ["component"],
            startup_string = "--hostlist=share://" + HostsPath,
        },
        Resources =
        [
            new ConfigShareResource
            {
                Path = HostsPath,
                Length = Encoding.UTF8.GetByteCount(HostsText),
                Sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(HostsText))),
            },
        ],
    };

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
