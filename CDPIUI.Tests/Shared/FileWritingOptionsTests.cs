using CDPIUI.Shared.Security;

namespace CDPIUI.Tests.Shared;

public sealed class FileWritingOptionsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cdpiui-write-" + Guid.NewGuid().ToString("N"));
    private readonly string _outside = Path.Combine(Path.GetTempPath(), "cdpiui-outside-" + Guid.NewGuid().ToString("N"));

    public FileWritingOptionsTests()
    {
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(_outside);
    }

    public void Dispose()
    {
        Directory.Delete(_root, true);
        Directory.Delete(_outside, true);
    }

    [Fact]
    public void WriteOutsideTheRootsIsFound()
    {
        string target = Path.Combine(_outside, "hosts");
        Assert.NotNull(FileWritingOptions.FindOutsideWrite($"--wf-tcp=443 --debug=@\"{target}\"", [_root], _root));
        Assert.NotNull(FileWritingOptions.FindOutsideWrite($"--wf-save {target}", [_root], _root));
    }

    [Fact]
    public void RelativeWriteResolvesAgainstTheWorkingDirectory()
    {
        Assert.Null(FileWritingOptions.FindOutsideWrite("--hostlist-auto=lists/auto.txt", [_root], _root));
        Assert.NotNull(FileWritingOptions.FindOutsideWrite("--hostlist-auto=../auto.txt", [_root], _root));
        Assert.NotNull(FileWritingOptions.FindOutsideWrite("--hostlist-auto=lists/auto.txt", [_root], baseDirectory: null));
    }

    [Fact]
    public void OptionsThatWriteNothingAreIgnored()
    {
        Assert.Null(FileWritingOptions.FindOutsideWrite("--debug=1 --hostlist=C:/lists/list.txt --dpi-desync=fake", [_root], _root));
    }

    [Fact]
    public void ArgumentFilesAreFollowed()
    {
        File.WriteAllText(Path.Combine(_root, "args.txt"), $"--wf-save={Path.Combine(_outside, "x.bin")}");
        Assert.Null(FileWritingOptions.FindOutsideWrite("@args.txt", [_root], _root));
        Assert.NotNull(FileWritingOptions.FindOutsideWrite("@args.txt", [_root], _root, followArgumentFiles: true));
    }

    [Fact]
    public void ArgumentFileLoopIsRejected()
    {
        // The check stops at its depth limit; the component would follow the file further unchecked.
        File.WriteAllText(Path.Combine(_root, "loop.txt"), "@loop.txt");
        Assert.NotNull(FileWritingOptions.FindOutsideWrite("@loop.txt", [_root], _root, followArgumentFiles: true));
    }

    [Fact]
    public void MissingArgumentFileIsRejected()
    {
        // It could be created between the check and the start.
        Assert.NotNull(FileWritingOptions.FindOutsideWrite("@missing.txt", [_root], _root, followArgumentFiles: true));
    }

    [Theory]
    // The component removes the quotes before it reads the option name.
    [InlineData("\"--debug=@{0}\"")]
    [InlineData("--de\"\"bug=@{0}")]
    [InlineData("'--debug=@{0}'")]
    // getopt_long takes a unique prefix of a long option.
    [InlineData("--debu=@{0}")]
    [InlineData("--wf-sav={0}")]
    [InlineData("--hostlist-auto-deb={0}")]
    public void HiddenOrShortenedOptionIsFound(string template)
    {
        string target = Path.Combine(_outside, "x.bin");
        Assert.NotNull(FileWritingOptions.FindOutsideWrite(string.Format(template, target), [_root], _root));
    }

    [Fact]
    public void RequiredValueStartingWithADashIsChecked()
    {
        // getopt_long gives "-x" to --wf-save as its file name; it used to be skipped as an option.
        Assert.NotNull(FileWritingOptions.FindOutsideWrite("--wf-save -x", [_root], baseDirectory: null));
    }

    [Fact]
    public void ExactOptionThatIsAPrefixIsNotTheWritingOption()
    {
        // --hostlist reads a list; getopt_long takes the exact match over --hostlist-auto.
        Assert.Null(FileWritingOptions.FindOutsideWrite($"--hostlist={Path.Combine(_outside, "list.txt")}", [_root], _root));
    }

    [Fact]
    public void WriteTargetsAreReported()
    {
        var targets = new List<string>();
        Assert.Null(FileWritingOptions.FindOutsideWrite("--hostlist-auto=lists/auto.txt", [_root], _root, writeTargets: targets));
        Assert.Contains(Path.Combine(_root, "lists", "auto.txt"), targets);
    }

    [Fact]
    public void LuaInitValuesAreReportedFromArgumentFilesToo()
    {
        File.WriteAllText(Path.Combine(_root, "args.txt"), "--lua-init=@lua/lib.lua");
        var values = new List<(string Value, string? BaseDirectory)>();

        Assert.Null(FileWritingOptions.FindOutsideWrite(
            "--lua-i=\"print(1)\" @args.txt", [_root], _root, followArgumentFiles: true, luaInitValues: values));

        Assert.Contains(("print(1)", (string?)_root), values);
        Assert.Contains(("@lua/lib.lua", (string?)_root), values);
    }

    [Fact]
    public void ArgumentFilesAreReadThroughTheGivenReader()
    {
        var read = new List<string>();
        File.WriteAllText(Path.Combine(_root, "args.txt"), "--dpi-desync=fake");

        Assert.Null(FileWritingOptions.FindOutsideWrite("@args.txt", [_root], _root, followArgumentFiles: true,
            readArgumentFile: path => { read.Add(path); return File.ReadAllText(path); }));

        Assert.Contains(Path.Combine(_root, "args.txt"), read);
    }
}
