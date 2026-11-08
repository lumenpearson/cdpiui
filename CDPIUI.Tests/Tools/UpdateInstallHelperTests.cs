using System.IO.Compression;
using System.Text;
using Update;

namespace CDPIUI.Tests.Tools;

[Collection("Windows path leases")]
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
        try
        { Directory.Delete(_root, true); }
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
    [InlineData(".. /evil.exe")]
    [InlineData("Assets /evil.exe")]
    [InlineData("CON.txt")]
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
            ("requirements.json", "{}"));

        using var zip = ZipFile.OpenRead(_archive);
        Assert.Equal(new[] { "layers/0/", "layers/2/", "layers/10/" }, InstallHelper.GetLayerPrefixes(zip));
    }

    [Theory]
    // The tray checked the catalog of every folder under layers/, this tool installed only the
    // numeric ones, or CDPIUI/ when none was numeric: a package could pass the check with one
    // set of files and install another. Such packages are rejected on both sides now.
    [InlineData("layers/name/CDPIUI.exe", "CDPIUI/CDPIUI.exe")]
    [InlineData("layers/01/CDPIUI.exe", "layers/1/CDPIUI.exe")]
    [InlineData("layers/0/CDPIUI.exe", "CDPIUI/CDPIUI.exe")]
    [InlineData("layers/CDPIUI.exe", "layers/0/CDPIUI.exe")]
    public void PackageTheTrayCouldReadDifferentlyIsRejected(string first, string second)
    {
        WriteArchive((first, "a"), (second, "b"));

        using var zip = ZipFile.OpenRead(_archive);
        Assert.Throws<InvalidDataException>(() => InstallHelper.GetLayerPrefixes(zip));
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
    public async Task ArchiveOverTheLimitsStopsTheUpdate()
    {
        // A file that is compressed far more than a real one: the tray rejects such a package,
        // and the tool also rejects it before it extracts anything.
        WriteArchive(("CDPIUI/CDPIUI.exe", "exe"), ("CDPIUI/bomb.bin", new string('a', 4 * 1024 * 1024)));

        var helper = CreateHelper();
        Assert.Null(await helper.StageAsync());
        Assert.Equal(InstallError.ExtractingFile, helper.FinishStatus);
        Assert.False(File.Exists(Staged("CDPIUI.exe")));
        Assert.False(File.Exists(Staged("bomb.bin")));
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
    public async Task ModifiedStagedBytesAreRejectedBeforeInstallingAnything()
    {
        if (!OperatingSystem.IsWindows()) return;
        File.WriteAllText(Path.Combine(_target, "a.txt"), "old");
        WriteArchive(("CDPIUI/a.txt", "verified"));
        var helper = CreateHelper();
        List<string> staged = await helper.StageAsync();
        File.WriteAllText(Staged("a.txt"), "injected");
        Assert.False(helper.ApplyStaged(staged));
        Assert.Equal("old", File.ReadAllText(Path.Combine(_target, "a.txt")));
        Assert.False(File.Exists(Path.Combine(_target, ".update-backup", "a.txt")));
    }

    [Fact]
    public async Task StagedFilesCannotChangeAfterApplyHasStarted()
    {
        if (!OperatingSystem.IsWindows()) return;
        WriteArchive(("CDPIUI/a.txt", "verified a"), ("CDPIUI/b.txt", "verified b"));
        var helper = CreateHelper();
        List<string> staged = await helper.StageAsync();
        helper.CurrentFileChanged += message =>
        {
            if (message == "Copying a.txt")
                Assert.Throws<IOException>(() => File.WriteAllText(Staged("b.txt"), "injected"));
        };
        Assert.True(helper.ApplyStaged(staged));
        Assert.Equal("verified b", File.ReadAllText(Path.Combine(_target, "b.txt")));
    }

    [Fact]
    public async Task PublishedFilesAndParentsStayPinnedUntilTheTransactionEnds()
    {
        if (!OperatingSystem.IsWindows()) return;
        WriteArchive(("CDPIUI/Assets/a.txt", "verified a"), ("CDPIUI/b.txt", "verified b"));
        var helper = CreateHelper();
        List<string> staged = await helper.StageAsync();
        helper.ProgressChanged += progress =>
        {
            if (progress != 0.5) return;
            Assert.Throws<IOException>(() => File.WriteAllText(Path.Combine(_target, "Assets", "a.txt"), "injected"));
            Assert.Throws<IOException>(() => Directory.Move(Path.Combine(_target, "Assets"), Path.Combine(_root, "displaced")));
        };
        Assert.True(helper.ApplyStaged(staged));
        Assert.Equal("verified a", File.ReadAllText(Path.Combine(_target, "Assets", "a.txt")));
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
    public async Task ApplyRejectsLinkedDestinationFolder()
    {
        WriteArchive(("CDPIUI/Assets/a.txt", "new"));
        var helper = CreateHelper();
        List<string> staged = await helper.StageAsync();
        string outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "a.txt"), "outside");
        Directory.CreateSymbolicLink(Path.Combine(_target, "Assets"), outside);

        Assert.False(helper.ApplyStaged(staged));
        Assert.Equal("outside", File.ReadAllText(Path.Combine(outside, "a.txt")));
    }

    [Fact]
    public async Task ApplyRejectsLinkedStagingFolder()
    {
        WriteArchive(("CDPIUI/Assets/a.txt", "new"));
        var helper = CreateHelper();
        List<string> staged = await helper.StageAsync();
        string outside = Path.Combine(_root, "outside");
        Directory.Move(Staged("Assets"), outside);
        Directory.CreateSymbolicLink(Staged("Assets"), outside);

        Assert.False(helper.ApplyStaged(staged));
        Assert.Equal("new", File.ReadAllText(Path.Combine(outside, "a.txt")));
        Assert.False(File.Exists(Path.Combine(_target, "Assets", "a.txt")));
    }

    [Fact]
    public async Task ApplyRejectsLinkedBackupFolder()
    {
        File.WriteAllText(Path.Combine(_target, "a.txt"), "old");
        WriteArchive(("CDPIUI/a.txt", "new"));
        var helper = CreateHelper();
        List<string> staged = await helper.StageAsync();
        string outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outside);
        Directory.CreateSymbolicLink(Path.Combine(_target, ".update-backup"), outside);

        Assert.False(helper.ApplyStaged(staged));
        Assert.Equal("old", File.ReadAllText(Path.Combine(_target, "a.txt")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(outside));
    }

    [Fact]
    public async Task StageKeepsBackupLeftByAnEarlierUpdate()
    {
        string backup = Path.Combine(_target, ".update-backup");
        Directory.CreateDirectory(backup);
        File.WriteAllText(Path.Combine(backup, "recover.txt"), "only surviving original");
        WriteArchive(("CDPIUI/a.txt", "new"));

        Assert.Null(await CreateHelper().StageAsync());
        Assert.Equal("only surviving original", File.ReadAllText(Path.Combine(backup, "recover.txt")));
    }

    [Fact]
    public async Task ApplyRechecksDestinationAfterProgressCallback()
    {
        WriteArchive(("CDPIUI/Assets/a.txt", "new"));
        var helper = CreateHelper();
        List<string> staged = await helper.StageAsync();
        string outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "a.txt"), "outside");
        string assets = Path.Combine(_target, "Assets");
        Directory.CreateDirectory(assets);
        helper.CurrentFileChanged += message =>
        {
            if (!message.StartsWith("Copying ", StringComparison.Ordinal))
                return;
            Directory.Move(assets, Path.Combine(_root, "displaced-assets"));
            Directory.CreateSymbolicLink(assets, outside);
        };

        Assert.False(helper.ApplyStaged(staged));
        Assert.Equal("outside", File.ReadAllText(Path.Combine(outside, "a.txt")));
    }

    [Fact]
    public async Task RollbackPreservesOriginalsAndNeverWritesThroughAReplacedParent()
    {
        Directory.CreateDirectory(Path.Combine(_target, "Assets"));
        File.WriteAllText(Path.Combine(_target, "Assets", "a.txt"), "original");
        WriteArchive(("CDPIUI/Assets/a.txt", "new"), ("CDPIUI/b.txt", "new b"));
        var helper = CreateHelper();
        List<string> staged = await helper.StageAsync();
        string outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "a.txt"), "outside");
        helper.ProgressChanged += progress =>
        {
            if (progress != 0.5)
                return;
            string assets = Path.Combine(_target, "Assets");
            if (OperatingSystem.IsWindows())
                Assert.Throws<IOException>(() => Directory.Move(assets, Path.Combine(_root, "displaced-assets")));
            else
            {
                Directory.Move(assets, Path.Combine(_root, "displaced-assets"));
                Directory.CreateSymbolicLink(assets, outside);
            }
            throw new IOException("Failure after the first publication.");
        };

        Assert.False(helper.ApplyStaged(staged));
        Assert.Equal("outside", File.ReadAllText(Path.Combine(outside, "a.txt")));
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(InstallError.ExtractingFile, helper.FinishStatus);
            Assert.Equal("original", File.ReadAllText(Path.Combine(_target, "Assets", "a.txt")));
        }
        else
        {
            Assert.Equal(InstallError.NoOldVersion, helper.FinishStatus);
            Assert.Equal("original", File.ReadAllText(Path.Combine(_target, ".update-backup", "Assets", "a.txt")));
        }
    }

    [Fact]
    public async Task RollbackKeepsTheOriginalBackupWhenTheNewObjectCannotBeRemoved()
    {
        if (!OperatingSystem.IsWindows()) return;
        string target = Path.Combine(_target, "a.txt");
        File.WriteAllText(target, "original");
        WriteArchive(("CDPIUI/a.txt", "new"), ("CDPIUI/b.txt", "new b"));
        var helper = CreateHelper();
        List<string> staged = await helper.StageAsync();
        helper.ProgressChanged += progress =>
        {
            if (progress != 0.5) return;
            File.SetAttributes(target, FileAttributes.ReadOnly);
            throw new IOException("Force a rollback with an attribute-only change.");
        };
        try
        {
            Assert.False(helper.ApplyStaged(staged));
            Assert.Equal(InstallError.NoOldVersion, helper.FinishStatus);
            Assert.Equal("original", File.ReadAllText(Path.Combine(_target, ".update-backup", "a.txt")));
        }
        finally { if (File.Exists(target)) File.SetAttributes(target, FileAttributes.Normal); }
    }

    [Fact]
    public async Task MissingStagedFileLeavesThePreviousFilesIntact()
    {
        File.WriteAllText(Path.Combine(_target, "a.txt"), "old a");
        WriteArchive(("CDPIUI/a.txt", "new a"), ("CDPIUI/b.txt", "new b"));

        var helper = CreateHelper();
        List<string> staged = await helper.StageAsync();
        // Windows verifies every captured source before changing the first installed file.
        File.Delete(Staged("b.txt"));

        Assert.False(helper.ApplyStaged(staged));
        Assert.Equal("old a", File.ReadAllText(Path.Combine(_target, "a.txt")));
        Assert.False(File.Exists(Path.Combine(_target, "b.txt")));
        Assert.Equal(InstallError.ExtractingFile, helper.FinishStatus);
    }
}
