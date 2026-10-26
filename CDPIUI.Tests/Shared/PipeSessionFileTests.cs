using System.Text.RegularExpressions;
using CDPIUI.Shared.Pipe;

namespace CDPIUI.Tests.Shared;

public sealed class PipeSessionFileTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "cdpiui-session-" + Guid.NewGuid().ToString("N"));

    public PipeSessionFileTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, true);

    private string SessionPath => PipeSessionFile.GetPath(_directory);

    private static PipeSession NewSession() => new(PipeSession.CreatePipeName(), PipeSession.CreateKey());

    // The tray passes a stream with the protected access list; the access list is Windows-only.
    private static Stream CreateNewFile(string path) =>
        new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);

    private void WriteRaw(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SessionPath)!);
        File.WriteAllText(SessionPath, content);
    }

    [Fact]
    public void PipeNameHasThePrefixAnd128RandomBits()
    {
        string name = PipeSession.CreatePipeName();

        Assert.Matches(new Regex("^CDPIUI_[0-9A-F]{32}$"), name);
        Assert.True(PipeSession.IsValidPipeName(name));
        Assert.NotEqual(name, PipeSession.CreatePipeName());
    }

    [Fact]
    public void KeyHas256RandomBits()
    {
        string key = PipeSession.CreateKey();

        Assert.Matches(new Regex("^[0-9A-F]{64}$"), key);
        Assert.True(PipeSession.IsValidKey(key));
        Assert.NotEqual(key, PipeSession.CreateKey());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{203ABE1D-01A2-47C0-B33D-8C8F7934CF4F}_CDPIUI")]
    [InlineData("CDPIUI_")]
    [InlineData("CDPIUI_0123456789ABCDEF0123456789ABCDE")]
    [InlineData("CDPIUI_0123456789ABCDEF0123456789ABCDEF0")]
    [InlineData("CDPIUI_0123456789abcdef0123456789abcdef")]
    [InlineData("cdpiui_0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("OTHER_0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("CDPIUI_..\\..\\0123456789ABCDEF0123456789AB")]
    [InlineData(" CDPIUI_0123456789ABCDEF0123456789ABCDEF")]
    public void OnlyNamesOfTheTrayFormatAreValid(string? name)
    {
        Assert.False(PipeSession.IsValidPipeName(name));
    }

    [Fact]
    public void ToStringShowsNeitherTheNameNorTheKey()
    {
        var session = NewSession();

        Assert.DoesNotContain(session.PipeName, session.ToString());
        Assert.DoesNotContain(session.Key, session.ToString());
    }

    [Fact]
    public void WrittenNameAndKeyAreReadBack()
    {
        var session = NewSession();

        PipeSessionFile.Write(_directory, session, CreateNewFile);

        Assert.Equal(session, PipeSessionFile.TryRead(_directory));
        Assert.Equal(session.PipeName + "\n" + session.Key + "\n", File.ReadAllText(SessionPath));
    }

    [Fact]
    public void WriteReplacesTheSessionOfAnEarlierStart()
    {
        PipeSessionFile.Write(_directory, NewSession(), CreateNewFile);
        var current = NewSession();

        PipeSessionFile.Write(_directory, current, CreateNewFile);

        Assert.Equal(current, PipeSessionFile.TryRead(_directory));
        Assert.Equal([PipeSessionFile.FileName], Directory.GetFiles(Path.GetDirectoryName(SessionPath)!).Select(Path.GetFileName));
    }

    [Fact]
    public void WriteReplacesALinkInsteadOfWritingThroughIt()
    {
        string target = Path.Combine(_directory, "target.txt");
        File.WriteAllText(target, "unchanged");
        Directory.CreateDirectory(Path.GetDirectoryName(SessionPath)!);
        File.CreateSymbolicLink(SessionPath, target);
        var session = NewSession();

        PipeSessionFile.Write(_directory, session, CreateNewFile);

        Assert.Equal("unchanged", File.ReadAllText(target));
        Assert.Null(new FileInfo(SessionPath).LinkTarget);
        Assert.Equal(session, PipeSessionFile.TryRead(_directory));
    }

    [Fact]
    public void FailedWriteKeepsTheEarlierSessionAndLeavesNoTemporaryFile()
    {
        var earlier = NewSession();
        PipeSessionFile.Write(_directory, earlier, CreateNewFile);

        Assert.Throws<IOException>(() => PipeSessionFile.Write(_directory, NewSession(), path =>
        {
            CreateNewFile(path).Dispose();
            throw new IOException("The disk is full.");
        }));

        Assert.Equal(earlier, PipeSessionFile.TryRead(_directory));
        Assert.Equal([PipeSessionFile.FileName], Directory.GetFiles(Path.GetDirectoryName(SessionPath)!).Select(Path.GetFileName));
    }

    [Fact]
    public void MissingFileGivesNoSession()
    {
        Assert.Null(PipeSessionFile.TryRead(_directory));
    }

    [Fact]
    public void LineEndsAndSpacesAroundTheValuesAreAccepted()
    {
        var session = NewSession();
        WriteRaw($"  {session.PipeName} \r\n {session.Key}\r\n\r\n");

        Assert.Equal(session, PipeSessionFile.TryRead(_directory));
    }

    [Theory]
    [InlineData("")]
    [InlineData("\r\n")]
    // The format of earlier versions: the key only, for the fixed pipe name.
    [InlineData("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("CDPIUI_0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("CDPIUI_0123456789ABCDEF0123456789ABCDEF\n0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDE")]
    [InlineData("CDPIUI_0123456789ABCDEF0123456789ABCDEF\n0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    [InlineData("{203ABE1D-01A2-47C0-B33D-8C8F7934CF4F}_CDPIUI\n0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF\nCDPIUI_0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("CDPIUI_0123456789ABCDEF0123456789ABCDEF\n0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF\nextra")]
    [InlineData("CDPIUI_0123456789ABCDEF0123456789ABCDEF 0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF")]
    public void DamagedFileGivesNoSession(string content)
    {
        WriteRaw(content);

        Assert.Null(PipeSessionFile.TryRead(_directory));
    }

    [Fact]
    public void TruncatedFileGivesNoSession()
    {
        string complete = PipeSessionFile.Format(NewSession());
        WriteRaw(complete.Substring(0, complete.Length - 10));

        Assert.Null(PipeSessionFile.TryRead(_directory));
    }

    [Fact]
    public void OversizedFileGivesNoSession()
    {
        var session = NewSession();
        WriteRaw(session.PipeName + "\n" + session.Key + new string(' ', 2048) + "\n");

        Assert.Null(PipeSessionFile.TryRead(_directory));
    }

    [Fact]
    public void SessionOfAStoppedTrayIsStillRead()
    {
        // The file cannot show that its tray has stopped. The GUI checks the image of the pipe
        // server before it sends the key, so a program that took the old name gets nothing.
        var stale = NewSession();
        WriteRaw(PipeSessionFile.Format(stale));

        Assert.Equal(stale, PipeSessionFile.TryRead(_directory));
    }
}
