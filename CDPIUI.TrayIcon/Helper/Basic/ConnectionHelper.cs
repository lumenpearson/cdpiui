using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.RegularExpressions;
using CDPIUI.Shared.Pipe;
using CDPIUI.Shared.Pipe.Models;

namespace CDPIUI.TrayIcon.Helper.Basic;

public class PipeServer : PipeServiceBase
{
    private int _maxServerInstances = 1;
    private Task? _listenerTask;

    private bool _isAuthorized = false;

    // New at each start. The GUI finds the pipe only through the session file.
    private string _pipeName = PipeSession.CreatePipeName();
    private bool _isPipeNamePublished;

    private static PipeServer? _instance;
    private static readonly object SyncRoot = new object();

    public static PipeServer Instance
    {
        get
        {
            lock (SyncRoot)
            {
                _instance ??= new PipeServer();
                return _instance;
            }
        }
    }

    public PipeServer()
    {
        Logger = Basic.Logger.Instance;
    }

    public void Init() { }

    public void Start()
    {
        if (CancellationToken != null)
            throw new InvalidOperationException("Server already started");
        CreateCancellationToken();

        _listenerTask = Task.Run(() => ListenLoopAsync());
    }



    private async Task ListenLoopAsync()
    {
        while (!(CancellationToken?.IsCancellationRequested ?? true))
        {
            _isAuthorized = false;
            try
            {
                var ps = PipeAccessPolicy.CreatePipeSecurity();

                // FirstPipeInstance: if a pipe with this name exists, the creation fails. Thus the
                // tray never adds an instance to a pipe that another program created.
                PipeStream = NamedPipeServerStreamAcl.Create(
                    _pipeName,
                    PipeDirection.InOut,
                    _maxServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance,
                    inBufferSize: 0,
                    outBufferSize: 0,
                    pipeSecurity: ps);

                // Published only after the pipe exists, so no other program can create a pipe
                // with this name first.
                if (!_isPipeNamePublished)
                {
                    PipeSessionWriter.Publish(_pipeName);
                    _isPipeNamePublished = true;
                }
            }
            catch (Exception ex)
            {
                PipeStream?.Dispose();
                PipeStream = null;

                // The desktop user can read the published name. Thus a program of that user can
                // create a pipe with it while the tray creates the next instance. A new name is
                // unknown to that program until the tray has created the pipe and published it.
                _pipeName = PipeSession.CreatePipeName();
                _isPipeNamePublished = false;
                Logger?.CreateWarningLog(nameof(PipeServer), $"Cannot create or publish the pipe, retrying with a new name: {ex.Message}");
                try
                { await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken ?? default); }
                catch (OperationCanceledException) { break; }
                continue;
            }

            try
            {
                await ((NamedPipeServerStream)PipeStream).WaitForConnectionAsync(CancellationToken ?? default);

                int threadId = Thread.CurrentThread.ManagedThreadId;
                if (!PipePeerVerifier.IsClientExpected((NamedPipeServerStream)PipeStream, PipeAccessPolicy.ExpectedClientPath))
                {
                    Logger?.CreateWarningLog(nameof(PipeServer), "Rejected a pipe client that is not the application UI.");
                    continue;
                }

                Logger?.CreateDebugLog(nameof(PipeServer), $"Client connected on thread[{threadId}].");
                // The pipe takes one client at a time: a client that never authorizes would
                // keep the real GUI out.
                var connectedStream = (NamedPipeServerStream)PipeStream;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(AuthorizationTimeout);
                    if (!_isAuthorized && ReferenceEquals(PipeStream, connectedStream) && connectedStream.IsConnected)
                    {
                        Logger?.CreateWarningLog(nameof(PipeServer), "The client did not authorize in time; disconnecting.");
                        try
                        { connectedStream.Disconnect(); }
                        catch (Exception ex)
                        {
                            // The client can leave at the same time; the pipe is then already closed.
                            Logger?.CreateDebugLog(nameof(PipeServer), $"Cannot disconnect the client: {ex.Message}");
                        }
                    }
                });
                await HandleConnectionAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException ex)
            {
                Logger?.CreateErrorLog(nameof(PipeServer), $"Pipe accept or communication error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger?.CreateErrorLog(nameof(PipeServer), $"Pipe failure: {ex.Message}");
            }
            finally
            {
                if (PipeStream.IsConnected)
                {
                    ((NamedPipeServerStream)PipeStream).Disconnect();
                }

                NotifyDisconnected();
                PipeStream?.Dispose();
                PipeStream = null;
                StreamString = null;

                Logger?.CreateDebugLog(nameof(PipeServer), $"Client handler finished.");
            }
        }
    }

    private static readonly TimeSpan AuthorizationTimeout = TimeSpan.FromSeconds(15);

    // Before authorization only service messages (connection, authorization result) go out:
    // component output and paths are for the authorized GUI only.
    protected override bool CanSend(string message)
    {
        if (_isAuthorized)
            return true;
        try
        {
            return PipeModelConvertor.ConvertBack(message) is ServiceMessageModel;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // Compared in constant time, so the time of a wrong answer tells nothing about the secret.
    private static bool SecretEquals(string? value, string secret) =>
        value != null && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(value), System.Text.Encoding.UTF8.GetBytes(secret));

    protected override async Task RunMessageActions(IPipeMessage message)
    {
        if (message is ServiceMessageModel messageModel)
        {
            if (messageModel.MessageType == ServiceMessageIds.RequestAuth)
            {
                // An empty key must never let a client in.
                string key = PipeSessionWriter.Key;
                if (!string.IsNullOrEmpty(key) &&
                    messageModel.MessageData != null && SecretEquals(messageModel.MessageData["GUID"], key))
                {
                    _isAuthorized = true;
                    _ = SendMessageAsync(ServiceMessageModel.AuthSuccessful().ToString());
                }
                else
                {
                    // A client with a wrong GUID could retry until the authorization timeout.
                    _isAuthorized = false;
                    await SendMessageAsync(ServiceMessageModel.AuthFailure().ToString());
                    Logger?.CreateWarningLog(nameof(PipeServer), "Authorization failed; disconnecting the client.");
                    try
                    { (PipeStream as NamedPipeServerStream)?.Disconnect(); }
                    catch (Exception ex)
                    {
                        // The client can leave at the same time; the pipe is then already closed.
                        Logger?.CreateDebugLog(nameof(PipeServer), $"Cannot disconnect the client: {ex.Message}");
                    }
                    return;
                }
            }
        }

        if (!_isAuthorized)
        {
            Logger?.CreateWarningLog(nameof(PipeServer), "Authorization failed.");
            return;
        }

        await CommandsHandler.HandleCommandAsync(message);
    }

    // Overrides a void member, so it cannot return a Task; nothing may escape it.
    public override async void Dispose()
    {
        try
        {
            try
            {
                if (_listenerTask != null)
                    await _listenerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The listener stops by cancellation; this is the normal end.
            }
            finally
            {
                base.Dispose();
            }
        }
        catch (Exception ex)
        {
            Basic.Logger.Instance.CreateErrorLog(nameof(PipeServer), $"Pipe server dispose failed: {ex}");
        }
    }

}



public class ScriptHelper
{
    public static string[] GetArgsFromString(string scriptString)
    {
        Match match = Regex.Match(scriptString, @"\((.*?)\)$", RegexOptions.Singleline);
        if (match.Success)
        {
            return match.Groups[1].Value.Split("$SEPARATOR");
        }
        return [scriptString];
    }
}
