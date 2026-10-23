using CDPIUI.Shared.Security;

namespace CDPIUI.Tests.Shared;

public sealed class FileWritingOptionsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "cdpiui-write-" + Guid.NewGuid().ToString("N"));
    private readonly string outside = Path.Combine(Path.GetTempPath(), "cdpiui-outside-" + Guid.NewGuid().ToString("N"));

    public FileWritingOptionsTests()
    {
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
    }

    public void Dispose()
    {
        Directory.Delete(root, true);
        Directory.Delete(outside, true);
    }

    [Fact]
    public void WriteOutsideTheRootsIsFound()
    {
        string target = Path.Combine(outside, "hosts");
        Assert.NotNull(FileWritingOptions.FindOutsideWrite($"--wf-tcp=443 --debug=@\"{target}\"", [root], root));
        Assert.NotNull(FileWritingOptions.FindOutsideWrite($"--wf-save {target}", [root], root));
    }

    [Fact]
    public void RelativeWriteResolvesAgainstTheWorkingDirectory()
    {
        Assert.Null(FileWritingOptions.FindOutsideWrite("--hostlist-auto=lists/auto.txt", [root], root));
        Assert.NotNull(FileWritingOptions.FindOutsideWrite("--hostlist-auto=../auto.txt", [root], root));
        Assert.NotNull(FileWritingOptions.FindOutsideWrite("--hostlist-auto=lists/auto.txt", [root], baseDirectory: null));
    }

    [Fact]
    public void OptionsThatWriteNothingAreIgnored()
    {
        Assert.Null(FileWritingOptions.FindOutsideWrite("--debug=1 --hostlist=C:/lists/list.txt --dpi-desync=fake", [root], root));
    }

    [Fact]
    public void ArgumentFilesAreFollowed()
    {
        File.WriteAllText(Path.Combine(root, "args.txt"), $"--wf-save={Path.Combine(outside, "x.bin")}");
        Assert.Null(FileWritingOptions.FindOutsideWrite("@args.txt", [root], root));
        Assert.NotNull(FileWritingOptions.FindOutsideWrite("@args.txt", [root], root, followArgumentFiles: true));
    }

    [Fact]
    public void ArgumentFileLoopStops()
    {
        File.WriteAllText(Path.Combine(root, "loop.txt"), "@loop.txt");
        Assert.Null(FileWritingOptions.FindOutsideWrite("@loop.txt", [root], root, followArgumentFiles: true));
    }
}
