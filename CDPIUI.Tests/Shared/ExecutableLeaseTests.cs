using CDPIUI.Shared.Pipe;
using CDPIUI.Shared.Security;

namespace CDPIUI.Tests.Shared;

public sealed class ExecutableLeaseTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "cdpiui-lease-" + Guid.NewGuid().ToString("N"));

    public ExecutableLeaseTests() => Directory.CreateDirectory(directory);

    public void Dispose() => Directory.Delete(directory, true);

    private string Write(string relative, string content)
    {
        string path = Path.Combine(directory, relative);
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
        string file = Path.Combine(directory, ".trust", "approved.txt");
        var approved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [program] = "AAAA",
            [Path.Combine(directory, "missing.exe")] = "BBBB",
        };

        ExecutableApprovals.Save(file, approved);
        ExecutableApprovals.Save(file, approved);
        Dictionary<string, string> loaded = ExecutableApprovals.Load(file);

        Assert.Single(loaded);
        Assert.Equal("AAAA", loaded[program.ToUpperInvariant()]);
    }

    [Fact]
    public void MissingApprovalFileGivesNoApprovals()
    {
        Assert.Empty(ExecutableApprovals.Load(Path.Combine(directory, "none.txt")));
    }

    [Theory]
    [InlineData("Store/Items/CSZTBN012/bin/winws.exe", "Store/Items/CSZTBN012")]
    [InlineData("Store/Items/CSBIHA024/ciadpi.exe", "Store/Items/CSBIHA024")]
    [InlineData("Tools/winws.exe", null)]
    public void StoreItemFolderIsFound(string program, string? expected)
    {
        string? folder = ExecutableApprovals.GetStoreItemDirectory(Path.Combine(directory, program));
        Assert.Equal(expected == null ? null : Path.GetFullPath(Path.Combine(directory, expected)), folder);
    }

    [Fact]
    public void PipeKeyIsReadFromTheSessionFolder()
    {
        Assert.Null(PipeSessionKey.TryRead(directory));
        Write(".session/pipe.key", " ABCDEF \r\n");
        Assert.Equal("ABCDEF", PipeSessionKey.TryRead(directory));
    }
}
