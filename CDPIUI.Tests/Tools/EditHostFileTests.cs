using System.Text;
using EditHostFile;

namespace CDPIUI.Tests.Tools;

public class EditHostFileTests : IDisposable
{
    private const string BlockStart = "# [CDOM-B]";
    private const string BlockEnd = "# [CDOM-E]";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "cdpiui-hosts-tests", Guid.NewGuid().ToString("N"));
    private readonly string _hosts;

    public EditHostFileTests()
    {
        Directory.CreateDirectory(_root);
        _hosts = Path.Combine(_root, "hosts");
    }

    public void Dispose()
    {
        try
        { Directory.Delete(_root, true); }
        catch (IOException) { /* A file can stay locked for a moment; the folder is in the temp directory. */ }
    }

    private static int CountOf(string text, string value)
    {
        int count = 0;
        for (int i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + 1, StringComparison.Ordinal))
            count++;
        return count;
    }

    [Fact]
    public void AnsiFileWithoutBomKeepsItsBytes()
    {
        Encoding ansi = Program.GetAnsiEncoding();
        // Bytes 0xC0..0xFF are not valid UTF-8 here. In code page 1251 they are Cyrillic letters.
        byte[] original = Encoding.ASCII.GetBytes("# ")
            .Concat(new byte[] { 0xCF, 0xF0, 0xE8, 0xEC, 0xE5, 0xF0 })
            .Concat(Encoding.ASCII.GetBytes("\r\n127.0.0.1 localhost\r\n"))
            .ToArray();
        File.WriteAllBytes(_hosts, original);

        var read = Program.Read(_hosts);
        Assert.Equal(ansi.CodePage, read.Encoding.CodePage);
        Assert.Equal("\r\n", read.NewLine);

        Program.AddNewLines(_hosts);

        byte[] result = File.ReadAllBytes(_hosts);
        Assert.Equal(original, result.Take(original.Length));
        Assert.Equal(original, File.ReadAllBytes(_hosts + ".bak"));
        Assert.Contains(BlockStart, ansi.GetString(result));
    }

    [Fact]
    public void Utf8FileWithBomKeepsTheBomAndLineFeeds()
    {
        File.WriteAllText(_hosts, "# Привет\n127.0.0.1 localhost\n", new UTF8Encoding(true));

        Program.AddNewLines(_hosts);

        byte[] result = File.ReadAllBytes(_hosts);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, result.Take(3));
        string text = new UTF8Encoding(false).GetString(result, 3, result.Length - 3);
        Assert.StartsWith("# Привет\n127.0.0.1 localhost\n" + BlockStart, text);
        Assert.DoesNotContain("\r\n", text);
    }

    [Fact]
    public void Utf16FileKeepsItsEncoding()
    {
        File.WriteAllText(_hosts, "127.0.0.1 localhost\r\n", new UnicodeEncoding(false, true));

        Program.AddNewLines(_hosts);

        byte[] result = File.ReadAllBytes(_hosts);
        Assert.Equal(new byte[] { 0xFF, 0xFE }, result.Take(2));
        Assert.Contains(BlockStart, Encoding.Unicode.GetString(result, 2, result.Length - 2));
    }

    [Fact]
    public void Utf8FileWithoutBomStaysWithoutBom()
    {
        File.WriteAllText(_hosts, "# Привет\r\n", new UTF8Encoding(false));

        var read = Program.Read(_hosts);
        Assert.Equal(65001, read.Encoding.CodePage);
        Assert.Empty(read.Encoding.GetPreamble());
    }

    [Fact]
    public void AddTwiceGivesOneBlock()
    {
        File.WriteAllText(_hosts, "127.0.0.1 localhost");

        Program.AddNewLines(_hosts);
        Program.AddNewLines(_hosts);

        string text = File.ReadAllText(_hosts);
        Assert.StartsWith("127.0.0.1 localhost\r\n" + BlockStart, text);
        Assert.Equal(1, CountOf(text, BlockStart));
        Assert.Equal(1, CountOf(text, BlockEnd));
    }

    [Fact]
    public void AddSkipsLinesThatAreAlreadyOutsideTheBlock()
    {
        Program.AddNewLines(_hosts);
        string block = File.ReadAllText(_hosts);
        string entry = block.Split("\r\n").First(line => line.Length > 0 && !line.StartsWith('#'));

        File.WriteAllText(_hosts, entry + "\r\n");
        Program.AddNewLines(_hosts);

        Assert.Equal(1, CountOf(File.ReadAllText(_hosts), entry + "\r\n"));
    }

    [Fact]
    public void RemoveDeletesOnlyTheBlock()
    {
        const string before = "127.0.0.1 localhost\r\n";
        const string after = "10.0.0.1 router\r\n";
        File.WriteAllText(_hosts, before);
        Program.AddNewLines(_hosts);
        File.AppendAllText(_hosts, after);

        Program.RemoveAddedLines(_hosts);

        Assert.Equal(before + after, File.ReadAllText(_hosts));
    }

    [Fact]
    public void RemoveWithoutBlockDoesNotWriteTheFile()
    {
        File.WriteAllText(_hosts, "127.0.0.1 localhost\r\n");
        var written = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(_hosts, written);

        Program.RemoveAddedLines(_hosts);

        Assert.Equal(written, File.GetLastWriteTimeUtc(_hosts));
    }

    [Fact]
    public void RemoveDoesNotCreateAMissingFile()
    {
        Program.RemoveAddedLines(_hosts);

        Assert.False(File.Exists(_hosts));
    }

    [Fact]
    public void AddCreatesAMissingFileInUtf8WithoutBom()
    {
        Program.AddNewLines(_hosts);

        byte[] result = File.ReadAllBytes(_hosts);
        Assert.NotEqual(0xEF, result[0]);
        Assert.StartsWith(BlockStart, Encoding.UTF8.GetString(result));
        Assert.Equal(0, new FileInfo(_hosts + ".bak").Length);
    }

    [Fact]
    public void RecoverRemovesTheBlockAndTheBackup()
    {
        File.WriteAllText(_hosts, "127.0.0.1 localhost\r\n");
        Program.AddNewLines(_hosts);
        File.AppendAllText(_hosts, "10.0.0.1 router\r\n");

        Program.RestoreBackup(_hosts);

        Assert.Equal("127.0.0.1 localhost\r\n10.0.0.1 router\r\n", File.ReadAllText(_hosts));
        Assert.False(File.Exists(_hosts + ".bak"));
    }
}
