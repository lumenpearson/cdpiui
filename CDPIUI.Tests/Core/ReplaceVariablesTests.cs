using CDPIUI.Core.ComponentServices.Configuration;

namespace CDPIUI.Tests.Core;

public class ReplaceVariablesTests
{
    [Fact]
    public void NestedVariablesAndCaseAreResolved()
    {
        var vars = new Dictionary<string, string>
        {
            ["BIN"] = "C:\\bin\\",
            ["fake-tls"] = "%bin%tls.bin",
        };

        Assert.Equal("--dpi-desync-fake-tls=C:\\bin\\tls.bin",
            ConfigurationService.ReplaceVariables("--dpi-desync-fake-tls=%FAKE-TLS%", vars));
    }

    [Fact]
    public void UnknownVariablesStay()
    {
        Assert.Equal("%~dp0list.txt %X%",
            ConfigurationService.ReplaceVariables("%~dp0list.txt %X%", new Dictionary<string, string> { ["Y"] = "1" }));
    }

    [Fact]
    public void SelfGrowingVariableStopsAtLimit()
    {
        var vars = new Dictionary<string, string> { ["A"] = "%A%%A%%A%%A%" };

        string result = ConfigurationService.ReplaceVariables("%A%", vars);

        Assert.True(result.Length <= ConfigurationService.MaxExpandedLength);
    }
}
