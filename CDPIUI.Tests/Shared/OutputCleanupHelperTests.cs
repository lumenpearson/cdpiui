using CDPIUI.Shared.ComponentsTask;

namespace CDPIUI.Tests.Shared;

public class OutputCleanupHelperTests
{
    [Fact]
    public void PlainTextWithBracketsIsKept()
    {
        Assert.Equal("[HOSTLIST] loaded [K] 1C;", OutputCleanupHelper.ReplaceSymbols("[HOSTLIST] loaded [K] 1C;"));
    }

    [Fact]
    public void EscapeSequencesAreRemoved()
    {
        string raw = "\u001b]0;C:\\winws.exe\u0007\u001b[?25lready\u001b[K\u001b[4;1Hnext\u001b[3Cword\u001b[0m";
        Assert.Equal("ready\nnext   word", OutputCleanupHelper.ReplaceSymbols(raw));
    }

    [Fact]
    public void IncompleteTailIsHeldBack()
    {
        string chunk = "line\r\n\u001b[1";
        string tail = OutputCleanupHelper.TakeIncompleteTail(ref chunk);
        Assert.Equal("line\r\n", chunk);
        Assert.Equal("\u001b[1", tail);

        string next = tail + "2;1H[HOSTLIST]";
        Assert.Equal(string.Empty, OutputCleanupHelper.TakeIncompleteTail(ref next));
        Assert.Equal("\n[HOSTLIST]", OutputCleanupHelper.ReplaceSymbols(next));
    }
}
