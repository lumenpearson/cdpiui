using CDPIUI.AddOns.ConfigImport;

namespace CDPIUI.Tests.AddOns;

public class ZapretConfigImportServiceTests
{
    [Theory]
    [InlineData("--a=1 --b=x^&y & echo done", "--a=1 --b=x&y ")]
    [InlineData("--a=1 ^| --b=2 > log.txt", "--a=1 | --b=2 ")]
    [InlineData("--hostlist=\"a^b & c.txt\" --c=^^", "--hostlist=\"a^b & c.txt\" --c=^")]
    public void ShellTailIsCutAndCaretsAreRemoved(string input, string expected)
    {
        Assert.Equal(expected, ZapretConfigImportService.TrimShellTail(input));
    }

    [Theory]
    [InlineData("dp")]
    [InlineData("nx")]
    [InlineData("f")]
    [InlineData("n")]
    [InlineData("x")]
    public void FileModifiersMatchCmd(string modifiers)
    {
        string file = Path.Combine(Path.GetTempPath(), "zapret", "general.bat");
        string expected = modifiers switch
        {
            "dp" => Path.GetDirectoryName(file) + Path.DirectorySeparatorChar,
            "nx" => "general.bat",
            "f" => file,
            "n" => "general",
            _ => ".bat",
        };

        string actual = ZapretConfigImportService.ExpandFileModifiers(modifiers, file);

        Assert.Equal(expected.Replace('\\', '/'), actual.Replace('\\', '/'));
    }
}
