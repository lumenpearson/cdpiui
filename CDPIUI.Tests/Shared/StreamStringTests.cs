using CDPIUI.Shared.Pipe;

namespace CDPIUI.Tests.Shared;

public class StreamStringTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(32767)]
    [InlineData(32768)]
    [InlineData(200_000)]
    public async Task MessagesOfAnyLengthRoundTrip(int length)
    {
        string message = string.Concat(Enumerable.Range(0, length).Select(i => (char)('а' + i % 32)));
        using var stream = new MemoryStream();

        await new StreamString(stream).WriteStringAsync(message);
        stream.Position = 0;

        Assert.Equal(message, await new StreamString(stream).ReadStringAsync());
    }

    [Fact]
    public async Task ShortMessageKeepsTwoByteHeader()
    {
        using var stream = new MemoryStream();
        await new StreamString(stream).WriteStringAsync("ab");

        Assert.Equal(new byte[] { 0, 4, (byte)'a', 0, (byte)'b', 0 }, stream.ToArray());
    }
}
