using System.Collections.Specialized;
using CDPIUI.Shared.Pipe.Models;

namespace CDPIUI.Tests.Shared;

public class PipeMessageModelTests
{
    [Theory]
    [InlineData("--dpi-desync-repeats=6 --dpi-desync-fooling=badseq --dup=+1:3-64")]
    [InlineData("C:\\Tom & Jerry\\list #1.txt")]
    [InlineData("100% = a+b")]
    [InlineData("Список сайтов.txt")]
    [InlineData("")]
    public void ValuesSurviveRoundTrip(string value)
    {
        var model = new CONPTYMessageModel
        {
            MessageType = CONPTYMessageIds.StartProcessId,
            MessageData = new NameValueCollection
            {
                { "componentId", "CSZTBN012" },
                { "args", value },
            },
        };

        var parsed = PipeModelConvertor.ConvertBack(model.ToString()) as CONPTYMessageModel;

        Assert.NotNull(parsed);
        Assert.Equal(CONPTYMessageIds.StartProcessId, parsed!.MessageType);
        Assert.Equal("CSZTBN012", parsed.MessageData?["componentId"]);
        Assert.Equal(value, parsed.MessageData?["args"]);
    }

    [Fact]
    public void MessageWithoutDataHasNoQuery()
    {
        var model = new CONPTYMessageModel { MessageType = CONPTYMessageIds.StartProcessId };

        Assert.DoesNotContain("?", model.ToString());
    }
}
