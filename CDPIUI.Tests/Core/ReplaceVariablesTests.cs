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

public class ReplaceFilesPathTests
{
    private static string? Replace(string startup, Dictionary<string, string> files)
    {
        var item = new CDPIUI.Core.ComponentServices.Helpers.Configuration.ConfigItem { startup_string = startup };
        return ConfigurationService.ReplaceFilesPath(item, files).startup_string;
    }

    [Fact]
    public void OnlyWholePathsAreReplaced()
    {
        var files = new Dictionary<string, string> { ["lists\\list.txt"] = "C:\\Data\\My lists\\list.txt" };

        Assert.Equal(
            "--hostlist=\"C:\\Data\\My lists\\list.txt\" --hostlist=lists\\mylist.txt --x=\"\" --name='a'",
            Replace("--hostlist=\"%~dp0lists/list.txt\" --hostlist=lists\\mylist.txt --x=\"\" --name='a'", files));
    }
}
