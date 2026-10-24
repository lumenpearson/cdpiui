using System.IO.Compression;
using System.Text;
using Update;

namespace CDPIUI.Tests.Tools;

public class UpdateInstallHelperTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cdpiui-update-tests", Guid.NewGuid().ToString("N"));
    private readonly string _target;
    private readonly string _archive;

    public UpdateInstallHelperTests()
    {
        _target = Path.Combine(_root, "install");
        _archive = Path.Combine(_root, "patch.cdpipatch");
        Directory.CreateDirectory(_target);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); }
        catch (IOException) { /* A file can stay locked for a moment; the folder is in the temp directory. */ }
    }

    private void WriteArchive(params (string Name, string Content)[] entries)
    {
        using var zip = ZipFile.Open(_archive, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            var entry = zip.CreateEntry(name);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
        }
    }

    private InstallHelper CreateHelper()
    {
        var helper = new InstallHelper();
        helper.Init(_archive, _target, "CDPIUI_TrayIcon.exe");
        return helper;
    }

    private string Staged(string relativePath) => Path.Combine(_target, ".update-staging", relativePath);

    [Theory]
    [InlineData("CDPIUI.exe")]
    [InlineData("Assets/Icons/app.ico")]
    [InlineData("Help/EN/../RU/index.md")]
    public void DestinationInsideTheRootIsAccepted(string relativePath)
    {
        Assert.True(InstallHelper.TryGetDestination(_target, relativePath, out string destination));
        Assert.StartsWith(Path.GetFullPath(_target), destination);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("../evil.exe")]
    [InlineData("Assets/../../evil.exe")]
    [InlineData("C:/Windows/evil.exe")]
    [InlineData("file.txt:stream")]
    public void DestinationOutsideTheRootIsRejected(string? relativePath)
    {
        Assert.False(InstallHelper.TryGetDestination(_target, relativePath!, out string destination));
        Assert.Null(destination);
    }

    [Fact]
    public void RootedDestinationIsRejected()
    {
        string rooted = Path.Combine(Path.GetTempPath(), "evil.exe");
        Assert.False(InstallHelper.TryGetDestination(_target, rooted, out _));
    }

    [Fact]
    public void LayersAreReadInNumericOrder()
    {
        WriteArchive(
            ("layers/10/CDPIUI.exe", "c"),
            ("layers/2/CDPIUI.exe", "b"),
            ("layers/0/CDPIUI.exe", "a"),
            ("layers/name/CDPIUI.exe", "x"),
            ("requirements.json", "{}"));

        using var zip = ZipFile.OpenRead(_archive);
        Assert.Equal(new[] { "layers/0/", "layers/2/", "layers/10/" }, InstallHelper.GetLayerPrefixes(zip));
    }

    [Fact]
    public void PublishedPatchWithoutLayersUsesTheApplicationFolder()
    {
        WriteArchive(("CDPIUI/CDPIUI.exe", "a"), ("requirements.json", "{}"));

        using var zip = ZipFile.OpenRead(_archive);
        Assert.Equal(new[] { "CDPIUI/" }, InstallHelper.GetLayerPrefixes(zip));
    }

    [Fact]
    public async Task LaterLayerReplacesTheFileOfAnEarlierLayer()
    {
        WriteArchive(
            ("layers/0/CDPIUI.exe", "old"),
            ("layers/0/Assets/a.txt", "a"),
            ("layers/1/CDPIUI.exe", "new"));

        var helper = CreateHelper();
        List<string> staged = await helper.StageAsync();

        Assert.NotNull(staged);
        Assert.Equal(new[] { "CDPIUI.exe", "Assets/a.txt" }, staged);
        Assert.Equal("new", File.ReadAllText(Staged("CDPIUI.exe")));
        Assert.Equal(InstallError.None, helper.FinishStatus);
    }

    [Fact]
    public async Task CatalogIsNeverStaged()
    {
        WriteArchive(
            ("CDPIUI/catalog.cat", "catalog"),
            ("CDPIUI/Sub/catalog.cat", "catalog"),
            ("CDPIUI/CDPIUI.exe", "exe"));

        List<string> staged = await CreateHelper().StageAsync();

        Assert.Equal(new[] { "CDPIUI.exe" }, staged);
        Assert.False(File.Exists(Staged("catalog.cat")));
    }

    [Fact]
    public async Task InstalledUpdaterIsNotReplaced()
    {
        File.WriteAllText(Path.Combine(_target, "Update.exe"), "running updater");
        WriteArchive(("CDPIUI/Update.exe", "new updater"), ("CDPIUI/_Update.exe", "new updater"));

        List<string> staged = await CreateHelper().StageAsync();

        Assert.Equal(new[] { "_Update.exe" }, staged);
    }

    [Fact]
    public async Task UpdaterIsStagedWhenItIsNotInstalled()
    {
        WriteArchive(("CDPIUI/Update.exe", "updater"));

        List<string> staged = await CreateHelper().StageAsync();

        Assert.Equal(new[] { "Update.exe" }, staged);
    }

    [Fact]
    public async Task EntryOutsideTheTargetStopsTheUpdate()
    {
        WriteArchive(("CDPIUI/CDPIUI.exe", "exe"), ("CDPIUI/../../evil.exe", "evil"));

        var helper = CreateHelper();
        Assert.Null(await helper.StageAsync());
        Assert.Equal(InstallError.ExtractingFile, helper.FinishStatus);
        Assert.False(File.Exists(Path.Combine(_root, "evil.exe")));
    }

    [Fact]
    public async Task ArchiveWithoutFilesStopsTheUpdate()
    {
        WriteArchive(("requirements.json", "{}"));

        var helper = CreateHelper();
        Assert.Null(await helper.StageAsync());
        Assert.Equal(InstallError.ExtractingFile, helper.FinishStatus);
    }

    [Fact]
    public async Task MissingArchiveStopsTheUpdate()
    {
        var helper = CreateHelper();
        Assert.Null(await helper.StageAsync());
        Assert.Equal(InstallError.FileNotFound, helper.FinishStatus);
    }

    [Fact]
    public async Task ApplyKeepsFilesThatAreNotInThePatch()
    {
        File.WriteAllText(Path.Combine(_target, "CDPIUI.exe"), "old exe");
        Directory.CreateDirectory(Path.Combine(_target, "Assets"));
        File.WriteAllText(Path.Combine(_target, "Assets", "kept.png"), "kept");
        WriteArchive(("CDPIUI/CDPIUI.exe", "new exe"), ("CDPIUI/Assets/new.png", "new"));

        var helper = CreateHelper();
        List<string> staged = await helper.StageAsync();
        Assert.True(helper.ApplyStaged(staged));

        Assert.Equal("new exe", File.ReadAllText(Path.Combine(_target, "CDPIUI.exe")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(_target, "Assets", "new.png")));
        Assert.Equal("kept", File.ReadAllText(Path.Combine(_target, "Assets", "kept.png")));
        Assert.Equal("old exe", File.ReadAllText(Path.Combine(_target, ".update-backup", "CDPIUI.exe")));
    }

    [Fact]
    public async Task FailedApplyRestoresThePreviousFiles()
    {
        File.WriteAllText(Path.Combine(_target, "a.txt"), "old a");
        WriteArchive(("CDPIUI/a.txt", "new a"), ("CDPIUI/b.txt", "new b"));

        var helper = CreateHelper();
        List<string> staged = await helper.StageAsync();
        // The second staged file is missing, so its move fails after the first file was applied.
        File.Delete(Staged("b.txt"));

        Assert.False(helper.ApplyStaged(staged));
        Assert.Equal("old a", File.ReadAllText(Path.Combine(_target, "a.txt")));
        Assert.False(File.Exists(Path.Combine(_target, "b.txt")));
        Assert.Equal(InstallError.ExtractingFile, helper.FinishStatus);
    }
}
