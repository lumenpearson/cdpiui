using CDPIUI.Core.Basic;

namespace CDPIUI.Core.Store.MSI
{
    public class MsiInstallerService
    {
        public enum MsiState
        {
            GettingReady,
            Installing,
            Complete,
            CompleteRestartRequest,
            ExceptionHappens
        }
        public class MsiCallback
        {
            public string? operationId;
            public MsiState State;
        }

        // Set by Run; OnResponse comes only after Run sent the request.
        private TaskCompletionSource<string>? _tcs;
        public Action<MsiCallback>? callbackAction;
        private readonly string _operationId;
        private readonly string _filename;
        private MsiState _state;

        private static readonly TimeSpan InstallTimeout = TimeSpan.FromMinutes(30);

        public MsiInstallerService(string operationId, string filename) 
        {
            _operationId = operationId;
            _filename = filename;
        }

        public async Task<MsiCallback> Run(CancellationToken ct = default)
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _tcs = tcs;

            // The task finished only with an answer of the tray: a lost answer or a hanging
            // msiexec kept the Store busy forever, and a cancellation left msiexec running.
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(InstallTimeout);
            using (cts.Token.Register(() => tcs.TrySetCanceled()))
            {
                MsiInstallerQueueManager.Instance.SendMsiInstallMessage(_operationId, _filename, this);
                try
                {
                    await tcs.Task.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    MsiInstallerQueueManager.Instance.RemoveMsiInstallerModel(_operationId, notify: true);
                    if (ct.IsCancellationRequested)
                        throw;
                    Logger.Instance.CreateErrorLog(nameof(MsiInstallerService), $"The installation of {_filename} did not finish in {InstallTimeout.TotalMinutes} minutes.");
                    _state = MsiState.ExceptionHappens;
                }
                return new MsiCallback()
                {
                    operationId = _operationId,
                    State = _state,
                };
            }
            
        }

        public void OnResponse(MsiState state)
        {
            callbackAction?.Invoke(new()
            {
                operationId = _operationId,
                State = state
            });
            Logger.Instance.CreateDebugLog(nameof(MsiInstallerService), $"STATE {state}");
            _state  = state;

            if (state == MsiState.Complete || state == MsiState.CompleteRestartRequest || state == MsiState.ExceptionHappens)
                _tcs?.TrySetResult(string.Empty);
        }
    }
}
