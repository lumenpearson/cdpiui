using CDPIUI.Shared.Security;

namespace CDPIUI.Tests.Shared;

public sealed class ExecutableLeaseTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "cdpiui-lease-" + Guid.NewGuid().ToString("N"));

    public ExecutableLeaseTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, true);

    private string Write(string relative, string content)
    {
        string path = Path.Combine(_directory, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void LeaseHoldsTheProgramAndTheCodeFilesNextToIt()
    {
        string program = Write("bin/winws.exe", "program");
        Write("bin/WinDivert.dll", "library");
        Write("bin/WinDivert64.sys", "driver");
        Write("bin/list.txt", "data");

        using var lease = ExecutableLease.Open(program);

        Assert.Equal(
            ["WinDivert.dll", "WinDivert64.sys", "winws.exe"],
            lease.Paths.Select(Path.GetFileName).Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void FingerprintChangesWhenALibraryChanges()
    {
        string program = Write("bin/winws.exe", "program");
        string library = Write("bin/cygwin1.dll", "library");

        string before;
        using (var lease = ExecutableLease.Open(program))
            before = lease.ComputeFingerprint();

        File.WriteAllText(library, "changed library");
        string after;
        using (var lease = ExecutableLease.Open(program))
            after = lease.ComputeFingerprint();

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void FingerprintIgnoresDataFiles()
    {
        string program = Write("bin/winws.exe", "program");
        string list = Write("bin/list.txt", "youtube.com");

        string before;
        using (var lease = ExecutableLease.Open(program))
            before = lease.ComputeFingerprint();

        File.WriteAllText(list, "discord.com");
        using var again = ExecutableLease.Open(program);

        Assert.Equal(before, again.ComputeFingerprint());
    }

    [Fact]
    public void HashOfTheProgramIsItsSha256()
    {
        string program = Write("bin/winws.exe", "abc");
        using var lease = ExecutableLease.Open(program);

        Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", lease.GetHash(program));
    }

    [Fact]
    public void ApprovalsSurviveASaveAndDropMissingPrograms()
    {
        string program = Write("bin/winws.exe", "program");
        string file = Path.Combine(_directory, ".trust", "approved.txt");
        var approved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [program] = "AAAA",
            [Path.Combine(_directory, "missing.exe")] = "BBBB",
        };

        ExecutableApprovals.Save(file, approved);
        ExecutableApprovals.Save(file, approved);
        Dictionary<string, string> loaded = ExecutableApprovals.Load(file);

        Assert.Single(loaded);
        Assert.Equal("AAAA", loaded[program.ToUpperInvariant()]);
    }

    [Fact]
    public void LeaseHoldsTheScriptsOfTheWholeStoreItem()
    {
        // Zapret2 loads --lua-init=@lua/zapret-lib.lua from the item folder, not the folder of the program.
        string program = Write("Store/Items/CSZTBN012/bin/winws2.exe", "program");
        Write("Store/Items/CSZTBN012/lua/zapret-lib.lua", "code");
        Write("Store/Items/CSZTBN012/lists/list.txt", "data");

        using var lease = ExecutableLease.Open(program);

        Assert.Equal(Path.GetFullPath(Path.Combine(_directory, "Store/Items/CSZTBN012")), lease.Root);
        Assert.Equal(
            ["winws2.exe", "zapret-lib.lua"],
            lease.Paths.Select(Path.GetFileName).Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void FingerprintChangesWhenAScriptChanges()
    {
        string program = Write("Store/Items/CSZTBN012/bin/winws2.exe", "program");
        string script = Write("Store/Items/CSZTBN012/lua/zapret-lib.lua", "code");

        string before;
        using (var lease = ExecutableLease.Open(program))
            before = lease.ComputeFingerprint();

        File.WriteAllText(script, "os.execute('changed')");
        using var again = ExecutableLease.Open(program);

        Assert.NotEqual(before, again.ComputeFingerprint());
    }

    [Fact]
    public void LockedArgumentFileCannotBeChanged()
    {
        // Sharing modes and open directory handles block these changes only on Windows.
        if (!OperatingSystem.IsWindows())
            return;

        string program = Write("bin/winws.exe", "program");
        string arguments = Write("bin/args.txt", "--dpi-desync=fake");

        using var lease = ExecutableLease.Open(program);
        Assert.Equal("--dpi-desync=fake", lease.ReadArgumentFile(arguments));

        Assert.Throws<IOException>(() => File.WriteAllText(arguments, "--wf-save=C:\\x"));
    }

    [Fact]
    public void WriteTargetFolderCannotBeRenamedWhileGuarded()
    {
        // Sharing modes and open directory handles block these changes only on Windows.
        if (!OperatingSystem.IsWindows())
            return;

        string program = Write("bin/winws.exe", "program");
        Directory.CreateDirectory(Path.Combine(_directory, "data", "lists"));

        using (var lease = ExecutableLease.Open(program))
        {
            lease.GuardWriteTarget(Path.Combine(_directory, "data", "lists", "auto.txt"), [Path.Combine(_directory, "data")]);
            Assert.ThrowsAny<IOException>(() => Directory.Move(Path.Combine(_directory, "data", "lists"), Path.Combine(_directory, "data", "moved")));

            // The component still creates and appends to the file it writes.
            File.WriteAllText(Path.Combine(_directory, "data", "lists", "auto.txt"), "a.com\n");
            File.AppendAllText(Path.Combine(_directory, "data", "lists", "auto.txt"), "b.com\n");
        }

        Directory.Move(Path.Combine(_directory, "data", "lists"), Path.Combine(_directory, "data", "moved"));
    }

    [Fact]
    public void WriteTargetOutsideTheRootsIsRejected()
    {
        string program = Write("bin/winws.exe", "program");
        using var lease = ExecutableLease.Open(program);

        Assert.Throws<UnauthorizedAccessException>(() =>
            lease.GuardWriteTarget(Path.Combine(_directory, "other", "x.txt"), [Path.Combine(_directory, "data")]));
    }

    [Fact]
    public void ApprovedCodeAndFoldersSurviveASave()
    {
        string folder = Path.Combine(_directory, "component");
        Directory.CreateDirectory(folder);
        string file = Path.Combine(_directory, ".trust", "approved.txt");
        var approved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [folder] = "AAAA",
            [ExecutableApprovals.CodeKey("CCCC")] = "CCCC",
        };

        ExecutableApprovals.Save(file, approved);
        Dictionary<string, string> loaded = ExecutableApprovals.Load(file);

        Assert.Equal("AAAA", loaded[folder]);
        Assert.Equal("CCCC", loaded[ExecutableApprovals.CodeKey("CCCC")]);
    }

    [Fact]
    public void MissingApprovalFileGivesNoApprovals()
    {
        Assert.Empty(ExecutableApprovals.Load(Path.Combine(_directory, "none.txt")));
    }

    [Theory]
    [InlineData("Store/Items/CSZTBN012/bin/winws.exe", "Store/Items/CSZTBN012")]
    [InlineData("Store/Items/CSBIHA024/ciadpi.exe", "Store/Items/CSBIHA024")]
    [InlineData("Tools/winws.exe", null)]
    public void StoreItemFolderIsFound(string program, string? expected)
    {
        string? folder = ExecutableApprovals.GetStoreItemDirectory(Path.Combine(_directory, program));
        Assert.Equal(expected == null ? null : Path.GetFullPath(Path.Combine(_directory, expected)), folder);
    }
}
