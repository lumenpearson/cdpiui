using System.Text;
using CDPIUI.Core.System;

namespace CDPIUI.Tests.Core;

public class BatchFileEncodingTests
{
    private const string Text = "rem Обход блокировок YouTube и Discord\r\nset BIN=%~dp0bin\\";

    static BatchFileEncodingTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    [Theory]
    [InlineData(866)]
    [InlineData(1251)]
    [InlineData(65001)]
    public void CyrillicBatchFilesAreDecoded(int codePage)
    {
        byte[] bytes = Encoding.GetEncoding(codePage).GetBytes(Text);
        Assert.Equal(Text, BatchFileEncoding.Decode(bytes));
    }

    [Fact]
    public void Utf8BomIsRemoved()
    {
        byte[] bytes = [.. new UTF8Encoding(true).GetPreamble(), .. Encoding.UTF8.GetBytes(Text)];
        Assert.Equal(Text, BatchFileEncoding.Decode(bytes));
    }
}
