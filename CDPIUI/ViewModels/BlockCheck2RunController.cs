#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;

namespace CDPIUI.ViewModels;

internal sealed class BlockCheck2RunController
{
    private readonly IBlockCheck2RunHost _host;
    private CancellationTokenSource? _cancellation;

    public BlockCheck2RunController(IBlockCheck2RunHost host) => _host = host;

    public async Task<BlockCheckRunResult?> StartAsync()
    {
        if (!_host.CanStart)
        {
            return null;
        }

        if (_host.LastReport != null)
        {
            _host.RenewGeneratedSiteListRoot();
        }
        _host.ResetResultState();
        _host.StartProgress();
        CancellationTokenSource? runCancellation = null;
        try
        {
            BlockCheckSessionPreparationResult prepared = _host.Prepare();
            _host.ApplyPrepared(prepared);
            if (!prepared.Success)
            {
                _host.SetState(BlockCheckSessionState.InputInvalid);
                return null;
            }

            runCancellation = new CancellationTokenSource();
            _cancellation = runCancellation;
            _host.SetState(BlockCheckSessionState.Running);
            BlockCheckRunResult result = await _host.RunPreparedAsync(prepared, runCancellation.Token);

            _host.ApplyRunResult(prepared, result);
            _host.CompleteDisplayedProgress();
            BlockCheckSessionState finalState = GetFinalState(result);
            // Publishing the final state permits a new run, so history must finish first.
            await _host.SaveReportToHistoryAsync(_host.LastReport!);
            _host.SetState(finalState);
            return result;
        }
        catch (OperationCanceledException)
        {
            _host.SetState(BlockCheckSessionState.Canceled);
            return null;
        }
        catch (Exception exception)
        {
            _host.SetOperationError(exception.Message);
            _host.SetState(BlockCheckSessionState.Failed);
            return null;
        }
        finally
        {
            _host.FinishProgress();
            runCancellation?.Dispose();
            if (ReferenceEquals(_cancellation, runCancellation))
            {
                _cancellation = null;
            }
            _host.CleanupGeneratedSiteListsIfUnviewable();
            _host.NotifyActionState();
        }
    }

    public void Cancel()
    {
        if (!_host.CanCancel || _cancellation == null)
        {
            return;
        }

        _host.SetState(BlockCheckSessionState.Canceling);
        _cancellation.Cancel();
    }

    private static BlockCheckSessionState GetFinalState(BlockCheckRunResult result) =>
        result.WasCanceled
            ? BlockCheckSessionState.Canceled
            : result.Success
                ? BlockCheckSessionState.Completed
                : result.Synthesis?.IsBestEffort == true
                    ? BlockCheckSessionState.CompletedWithWarnings
                    : BlockCheckSessionState.Failed;
}
