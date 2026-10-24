namespace CDPIUI.Helper.AddOns;

public class TestLogSegment
{
    public string Text;
    public TestLogColor Color;

    public TestLogSegment(string text, TestLogColor color = TestLogColor.Default)
    {
        Text = text;
        Color = color;
    }
}
