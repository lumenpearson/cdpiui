using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using CDPIUI.Shared.Logger;
using CDPIUI.Shared.Pipe.Models;

namespace CDPIUI.Shared.Pipe;

public class PipeServiceBase : IDisposable
{
    protected PipeStream? PipeStream;
    protected StreamString? StreamString;

    protected CancellationTokenSource? CancellationTokenSource;
    protected CancellationToken? CancellationToken;

    protected ILogger? Logger;

    public event Action? Connected;
    public event Action? Disconnected;

    public bool IsConnected => PipeStream?.IsConnected ?? false;

    protected void CreateCancellationToken()
    {
        CancellationTokenSource = new CancellationTokenSource();
        CancellationToken = CancellationTokenSource.Token;
    }

    protected PipeServiceBase()
    {

    }

    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public async Task HandleConnectionAsync()
    {
        if (PipeStream == null)
            return;

        StreamString = new StreamString(PipeStream);

        _ = SendConnectionPacket();

        while (PipeStream.IsConnected && !(CancellationToken?.IsCancellationRequested ?? true))
        {
            string message;
            try
            {
                message = await StreamString.ReadStringAsync();

                if (string.IsNullOrEmpty(message))
                {
                    continue;
                }

                // Not awaited: a handler can run for minutes. It runs on this thread up to its
                // first await, so a handler can keep the order of arrival there.
                _ = RunStringMessageActions(message);
            }
            catch (EndOfStreamException)
            {
                continue;
            }

            Logger?.CreateDebugLog(nameof(PipeServiceBase), $"Received message: {RedactSecrets(message)}");
        }
        Logger?.CreateInfoLog(nameof(PipeServiceBase), $"{CancellationToken?.IsCancellationRequested}, {PipeStream.IsConnected.ToString()}");
    }

    // The authentication GUID must not end up in log files.
    private static string RedactSecrets(string message) =>
        System.Text.RegularExpressions.Regex.Replace(message, "(GUID=)[^&]*", "$1***",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    // Catches and logs its own errors, so a caller can start it without waiting for it.
    public async Task SendMessage(string message)
    {
        try
        {
            await SendMessageAsync(message);
        }
        catch (Exception ex)
        {
            Logger?.CreateWarningLog(nameof(PipeServiceBase), $"Cannot send a message: {ex.Message}");
        }
    }
    public async Task<bool> SendMessageAsync(string message)
    {
        return await SendStringMessageToPipe(message);
    }

    /// <summary>
    /// Lets a server hold messages back, for example until the client is authorized.
    /// </summary>
    protected virtual bool CanSend(string message) => true;

    protected virtual async Task SendConnectionPacket()
    {
        if (PipeStream == null)
            return;

        await SendMessageAsync(ServiceMessageModel.ConnectionSuccessful().ToString());
    }

    // Catches and logs its own errors; the read loop does not wait for it.
    private async Task RunStringMessageActions(string message)
    {
        try
        {
            var model = PipeModelConvertor.ConvertBack(message);
            if (model != null)
                await RunMessageActions(model);
        }
        catch (Exception ex)
        {
            Logger?.CreateWarningLog(nameof(PipeServiceBase), "Exception " + ex.Message);
        }
    }

    protected virtual async Task RunMessageActions(IPipeMessage model)
    {
        if (model is ServiceMessageModel serviceMessageModel &&
            serviceMessageModel.MessageType == ServiceMessageIds.AuthOK)
        {
            Connected?.Invoke();
        }

        await Task.CompletedTask;
    }

    private async Task<bool> SendStringMessageToPipe(string message)
    {
        Logger?.CreateDebugLog(nameof(PipeServiceBase), $"Message \"{RedactSecrets(message)}\" added to send queue");

        await _sendLock.WaitAsync();

        try
        {
            // Checked under the lock: the client can change while the message waits, and the
            // listener clears the streams when the client leaves.
            if (!CanSend(message))
                return false;

            var pipeStream = PipeStream;
            var streamString = StreamString;
            if (pipeStream == null || !pipeStream.IsConnected || streamString == null)
            {
                Logger?.CreateInfoLog(nameof(PipeServiceBase), "Cannot send any message. Connect PIPE first!");
                return false;
            }

            await streamString.WriteStringAsync(message, default);
            return true;
        }
        catch (Exception ex)
        {
            Logger?.CreateWarningLog(nameof(PipeServiceBase), $"Message \"{RedactSecrets(message)}\" cannot be sent: {ex.Message}");
            return false;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    protected void NotifyConnected() => Connected?.Invoke();
    protected void NotifyDisconnected() => Disconnected?.Invoke();

    public virtual void Dispose()
    {
        Disconnected?.Invoke();
        try
        {
            if (PipeStream?.IsConnected == true)
                PipeStream.WaitForPipeDrain();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // The other side has already closed the pipe; there is nothing left to drain.
        }
        PipeStream?.Dispose();
        StreamString = null;

        if (CancellationTokenSource != null)
        {
            CancellationTokenSource.Cancel();
            CancellationTokenSource.Dispose();
            CancellationTokenSource = null;
            CancellationToken = null;
        }
    }
}
