using CDPIUI.Shared;
using CDPIUI.Shared.Pipe;
using CDPIUI.Shared.Pipe.Models;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.RegularExpressions;

namespace CDPIUI.TrayIcon.Helper.Basic
{
    public class PipeServer : PipeServiceBase
    {
        private int _maxServerInstances = 1;
        private Task? _listenerTask;

        private bool IsAuthorized = false;

        private static PipeServer? _instance;
        private static readonly object _lock = new object();

        public static PipeServer Instance
        {
            get
            {
                lock (_lock)
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
            if (CancellationToken != null) throw new InvalidOperationException("Server already started");
            CreateCancellationToken();

            _listenerTask = Task.Run(() => ListenLoopAsync());
        }

        

        private async Task ListenLoopAsync()
        {
            while (!(CancellationToken?.IsCancellationRequested ?? true))
            {
                IsAuthorized = false;
                try
                {
                    var ps = PipeAccessPolicy.CreatePipeSecurity();

                    PipeStream = NamedPipeServerStreamAcl.Create(
                        SharedConstants.PipeName,
                        PipeDirection.InOut,
                        _maxServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous,
                        inBufferSize: 0,
                        outBufferSize: 0,
                        pipeSecurity: ps);
                }
                catch (Exception ex)
                {
                    if (IsAnotherTrayRunning())
                    {
                        Logger?.CreateDebugLog(nameof(PipeServer),
                            $"Terminating PID={Environment.ProcessId}: pipe creation failed and another tray instance is running. " +
                            $"Exception: {ex}");
                        Process.GetCurrentProcess().Kill();
                        return;
                    }

                    // Another process took the name while the pipe was being recreated. Clients check
                    // that the server is this tray, so waiting for the name to be released is enough.
                    Logger?.CreateWarningLog(nameof(PipeServer), $"Cannot create the pipe, retrying: {ex.Message}");
                    try { await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken ?? default); }
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
                        if (!IsAuthorized && ReferenceEquals(PipeStream, connectedStream) && connectedStream.IsConnected)
                        {
                            Logger?.CreateWarningLog(nameof(PipeServer), "The client did not authorize in time; disconnecting.");
                            try { connectedStream.Disconnect(); } catch (Exception) { }
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
            if (IsAuthorized)
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

        private static bool IsAnotherTrayRunning()
        {
            using var current = Process.GetCurrentProcess();
            Process[] trays = Process.GetProcessesByName(current.ProcessName);
            try
            {
                return trays.Any(process => process.Id != current.Id);
            }
            finally
            {
                foreach (var process in trays) process.Dispose();
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
                    // An empty key (the key file could not be written) must not let any client in.
                    string key = PipeSessionKeyWriter.Key;
                    if (!string.IsNullOrEmpty(key) &&
                        messageModel.MessageData != null && SecretEquals(messageModel.MessageData["GUID"], key))
                    {
                        IsAuthorized = true;
                        _ = SendMessageAsync(ServiceMessageModel.AuthSuccessful().ToString());
                    }
                    else
                    {
                        // A client with a wrong GUID could retry until the authorization timeout.
                        IsAuthorized = false;
                        await SendMessageAsync(ServiceMessageModel.AuthFailure().ToString());
                        Logger?.CreateWarningLog(nameof(PipeServer), "Authorization failed; disconnecting the client.");
                        try { (PipeStream as NamedPipeServerStream)?.Disconnect(); } catch (Exception) { }
                        return;
                    }
                }
            }

            if (!IsAuthorized)
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
                    if (_listenerTask != null) await _listenerTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
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
}
