using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using CDPIUI.Shared.PrettyErrorConversionService;

namespace CDPIUI.Tests.Shared;

public class ErrorConvertorTests
{
    private readonly ErrorConvertor _convertor = new();

    [Theory]
    [InlineData(5, PrettyErrorCode.ACCESS_DENIED)]
    [InlineData(2, PrettyErrorCode.IO_FILE_NOT_FOUND)]
    [InlineData(32, PrettyErrorCode.IO_FILE_USED_FOR_ANOTHER_PROCESS)]
    [InlineData(1223, PrettyErrorCode.OPERATION_CANCELLED_BY_USER)]
    public void Win32ErrorsAreMapped(int error, PrettyErrorCode expected)
    {
        Assert.Equal(expected, _convertor.MapExceptionToCode(new Win32Exception(error), out _));
    }

    [Fact]
    public void Win32ErrorInsideAnotherExceptionIsMapped()
    {
        var exception = new InvalidOperationException("Start failed", new Win32Exception(5));
        Assert.Equal(PrettyErrorCode.ACCESS_DENIED, _convertor.MapExceptionToCode(exception, out _));
    }

    [Theory]
    [InlineData(SocketError.HostNotFound, PrettyErrorCode.HOST_NAME_NOT_RESOLVED)]
    [InlineData(SocketError.ConnectionRefused, PrettyErrorCode.CANNOT_CONNECT)]
    [InlineData(SocketError.ConnectionReset, PrettyErrorCode.CONNECTION_RESET)]
    [InlineData(SocketError.TimedOut, PrettyErrorCode.TIMEOUT)]
    public void NetworkErrorsAreMapped(SocketError error, PrettyErrorCode expected)
    {
        var exception = new HttpRequestException("Request failed",
            new IOException("Transport failed", new SocketException((int)error)));
        Assert.Equal(expected, _convertor.MapExceptionToCode(exception, out _));
    }

    [Fact]
    public void HttpStatusCodeIsKept()
    {
        var exception = new HttpRequestException("Not found", null, HttpStatusCode.NotFound);

        PrettyErrorCode code = _convertor.MapExceptionToCode(exception, out _, out int? statusCode);

        Assert.Equal(PrettyErrorCode.UNEXPECTED_STATUS_CODE, code);
        Assert.Equal(404, statusCode);
        Assert.StartsWith("TEST_UNEXPECTED_STATUS_CODE_404", _convertor.GetPrettyErrorCode("TEST", exception, null));
    }

    [Fact]
    public void TimeoutInsideCancellationIsMapped()
    {
        var exception = new TaskCanceledException("Timed out", new TimeoutException());
        Assert.Equal(PrettyErrorCode.TIMEOUT, _convertor.MapExceptionToCode(exception, out _));
    }

    [Fact]
    public void ErrorModelKeepsHresult()
    {
        var exception = new IOException("Disk full", unchecked((int)0x80070070));

        ErrorModel model = _convertor.GetErrorModel("test", exception, null);

        Assert.Equal(nameof(PrettyErrorCode.IO_DISK_FULL), model.ErrorCode);
        Assert.Equal("0x80070070", model.HResult);
    }

    [Fact]
    public void SuccessCodeIsUnchanged()
    {
        Assert.Equal("HOSTEDIT_SUCCESS (0x00000000)", _convertor.GetPrettyErrorCode("HOSTEDIT", 0, null));
    }
}
