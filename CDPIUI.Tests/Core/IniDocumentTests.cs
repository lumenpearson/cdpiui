using CDPIUI.Core.Basic;

namespace CDPIUI.Tests.Core;

public class IniDocumentTests
{
    private const string Source =
        "// GoodCheck settings\n" +
        "[General]\n" +
        "ConnectionTimeout=5\n" +
        "Name = Test value\n" +
        "\n" +
        "[Advanced]\n" +
        "; comment\n" +
        "SkipCertVerify=false\n";

    [Fact]
    public void ValuesAreRead()
    {
        var document = IniDocument.Parse(Source);
        Assert.True(document.TryGetValue("General", "ConnectionTimeout", out string timeout));
        Assert.Equal("5", timeout);
        Assert.True(document.TryGetValue("general", "name", out string name));
        Assert.Equal("Test value", name);
        Assert.False(document.TryGetValue("Advanced", "Missing", out _));
    }

    [Fact]
    public void ChangedValueKeepsTheOtherLines()
    {
        var document = IniDocument.Parse(Source);
        document.SetValue("Advanced", "SkipCertVerify", "true");
        string expected = Source.Replace("SkipCertVerify=false", "SkipCertVerify=true").Replace("\n", Environment.NewLine);
        Assert.Equal(expected, document.ToString());
    }

    [Fact]
    public void NewKeyGoesToTheEndOfItsSection()
    {
        var document = IniDocument.Parse(Source);
        document.SetValue("General", "Retries", "3");
        string[] lines = document.ToString().Split(Environment.NewLine);
        Assert.Equal("Retries=3", lines[4]);
        Assert.Equal(string.Empty, lines[5]);
        Assert.Equal("[Advanced]", lines[6]);
    }

    [Fact]
    public void NewSectionIsAppended()
    {
        var document = IniDocument.Parse(Source);
        document.SetValue("Extra", "Key", "Value");
        Assert.EndsWith($"{Environment.NewLine}[Extra]{Environment.NewLine}Key=Value{Environment.NewLine}", document.ToString());
        Assert.True(document.TryGetValue("Extra", "Key", out string value));
        Assert.Equal("Value", value);
    }

    [Fact]
    public void ValueMayContainEqualsSign()
    {
        var document = IniDocument.Parse("[Advanced]\r\nConnectivityTestURL=https://example.com/?a=b\r\n");
        Assert.True(document.TryGetValue("Advanced", "ConnectivityTestURL", out string url));
        Assert.Equal("https://example.com/?a=b", url);
    }
}
