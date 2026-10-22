using CDPIUI.AddOns.ConfigImport;

namespace CDPIUI.Tests.AddOns;

public class ConfigImportServiceTests
{
    [Theory]
    [InlineData("set LIST=--hostlist=\"%~dp0lists\\list.txt\"", "LIST", "--hostlist=\"%~dp0lists\\list.txt\"")]
    [InlineData("set \"BIN=%~dp0bin\\\"", "BIN", "%~dp0bin\\")]
    [InlineData("set \"ARGS=--hostlist=\"a b.txt\"\"", "ARGS", "--hostlist=\"a b.txt\"")]
    [InlineData("@SET PORT=443", "PORT", "443")]
    public void TryParseSetKeepsQuotesInsideValue(string line, string expectedName, string expectedValue)
    {
        Assert.True(ConfigImportService.TryParseSet(line, out string name, out string value));
        Assert.Equal(expectedName, name);
        Assert.Equal(expectedValue, value);
    }
}
