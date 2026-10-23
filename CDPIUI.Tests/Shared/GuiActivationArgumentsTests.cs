using CDPIUI.Shared.Basic.CommandLine;

namespace CDPIUI.Tests.Shared;

public class GuiActivationArgumentsTests
{
    [Fact]
    public void ArgumentsSurviveEncoding()
    {
        string[] args = ["C:\\Users\\Иван\\Мои пресеты\\a b.cdpiconfig", "--direct:cdpiui://x?y=1&z=2", "\"quoted\""];

        string? encoded = GuiActivationArguments.Encode(args);

        Assert.NotNull(encoded);
        Assert.DoesNotContain(" ", encoded);
        Assert.True(GuiActivationArguments.TryDecode(["--show-ui", encoded!], out var decoded));
        Assert.Equal(args, decoded);
    }

    [Fact]
    public void MissingOrDamagedValueIsIgnored()
    {
        Assert.False(GuiActivationArguments.TryDecode(["--show-ui"], out _));
        Assert.False(GuiActivationArguments.TryDecode(["--gui-args:%%%"], out _));
        Assert.Null(GuiActivationArguments.Encode([]));
    }

    [Theory]
    [InlineData("simple", "simple")]
    [InlineData("with space", "\"with space\"")]
    [InlineData("C:\\dir with space\\", "\"C:\\dir with space\\\\\"")]
    [InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]
    [InlineData("", "\"\"")]
    public void ArgumentsAreQuotedForCommandLineToArgv(string argument, string expected)
    {
        Assert.Equal(expected, GuiActivationArguments.JoinCommandLine([argument]));
    }
}
