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
    public void ArgumentFileLoopStops()
    {
        File.WriteAllText(Path.Combine(_root, "loop.txt"), "@loop.txt");
        Assert.Null(FileWritingOptions.FindOutsideWrite("@loop.txt", [_root], _root, followArgumentFiles: true));
    }
}
