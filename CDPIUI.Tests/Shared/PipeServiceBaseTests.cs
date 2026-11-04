using System.IO.Pipes;
using CDPIUI.Shared.Pipe;

namespace CDPIUI.Tests.Shared;

public class PipeServiceBaseTests
{
    [Fact]
    public async Task CancellationEndsAnIdleRead()
    {
        var (server, client) = await ConnectAsync();
        using var service = new TestPipeService(client);
        Task reading = service.HandleConnectionAsync();
        try
        {
            service.CancelReading();
            await reading.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            server.Dispose();
            await ObserveShutdownAsync(reading);
        }
    }

    [Fact]
    public async Task DisposeDoesNotWaitForThePeerToReadPendingOutput()
    {
        var (server, client) = await ConnectAsync();
        using var service = new TestPipeService(client);
        Task reading = service.HandleConnectionAsync();
        Task? disposing = null;
        try
        {
            Assert.True(await service.SendMessageAsync("pending output"));
            disposing = Task.Run(service.Dispose);
            await disposing.WaitAsync(TimeSpan.FromSeconds(2));
            await reading.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            server.Dispose();
            if (disposing != null)
                await disposing.WaitAsync(TimeSpan.FromSeconds(5));
            await ObserveShutdownAsync(reading);
        }
    }

    [Fact]
    public async Task ATruncatedMessageEndsTheReadLoop()
    {
        var (server, client) = await ConnectAsync();
        using var service = new TestPipeService(client);
        Task reading = service.HandleConnectionAsync();
        try
        {
            await server.WriteAsync(new byte[] { 0, 10, 65 });
            server.Dispose();
            await reading.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            server.Dispose();
            service.Dispose();
            await ObserveShutdownAsync(reading);
        }
    }

    private static async Task<(NamedPipeServerStream, NamedPipeClientStream)> ConnectAsync()
    {
        string name = "cdpiui-test-" + Guid.NewGuid().ToString("N");
        var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 65536, 65536);
        var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            Task accepting = server.WaitForConnectionAsync();
            await client.ConnectAsync(5000);
            await accepting.WaitAsync(TimeSpan.FromSeconds(5));
            return (server, client);
        }
        catch
        {
            server.Dispose();
            client.Dispose();
            throw;
        }
    }

    private static async Task ObserveShutdownAsync(Task reading)
    {
        try { await reading.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException) { }
    }

    private sealed class TestPipeService : PipeServiceBase
    {
        public TestPipeService(PipeStream pipe)
        {
            PipeStream = pipe;
            CreateCancellationToken();
        }

        public void CancelReading() => CancellationTokenSource!.Cancel();

        protected override Task SendConnectionPacket() => Task.CompletedTask;
    }
}
