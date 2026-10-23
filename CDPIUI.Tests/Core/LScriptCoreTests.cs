using CDPIUI.Core.LScript;

namespace CDPIUI.Tests.Core;

public class LScriptCoreTests
{
    [Theory]
    [InlineData(true, "C:\\lists\\a.txt")]
    [InlineData(false, "D:\\lists\\b.txt")]
    public void ConditionWithDrivePathsPicksTheRightBranch(bool flag, string expected)
    {
        var jparams = new Dictionary<string, bool> { ["useA"] = flag };

        Assert.Equal(expected, LScriptCore.LocalCondition("useA==true?C:\\lists\\a.txt:D:\\lists\\b.txt", jparams));
    }
}
