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

    [Fact]
    public void ArgumentKeepsNestedParentheses()
    {
        Assert.Equal(
            "useA==true ? $GETCURRENTDIR()/a.txt $SEPARATOR b.txt",
            LScriptCore.GetArgumentsFromScript("$LOCALCONDITION(useA==true ? $GETCURRENTDIR()/a.txt $SEPARATOR b.txt)"));
    }

    [Fact]
    public void ConditionWithoutParametersIsEmpty()
    {
        Assert.Equal(string.Empty, LScriptCore.LocalCondition("useA==true ? a $SEPARATOR b", null));
    }

    [Fact]
    public void ConditionStringWithDrivePathIsSplitAtTheTernaryColon()
    {
        var result = LScriptCore.GetNameOnOffValuesFromConditionString("useA==true?C:\\a.txt:D:\\b.txt");

        Assert.NotNull(result);
        Assert.Equal("C:\\a.txt", result!.Item3);
        Assert.Equal("D:\\b.txt", result.Item4);
    }
}
