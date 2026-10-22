using CDPIUI.Shared.Pipe;

namespace CDPIUI.Tests.Shared;

public class PipePayloadCodecTests
{
    [Theory]
    [InlineData("")]
    [InlineData("plain")]
    [InlineData("C:\\Tom & Jerry\\list #1+2.txt")]
    [InlineData("Юникод ✓")]
    public void StringRoundTrip(string value)
    {
        Assert.Equal(value, PipePayloadCodec.DecodeString(PipePayloadCodec.Encode(value)));
    }
}
