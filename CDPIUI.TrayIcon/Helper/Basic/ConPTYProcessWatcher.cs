using static CDPIUI.TrayIcon.Helper.Basic.ConPTYNativeMethods;

namespace CDPIUI.TrayIcon.Helper.Basic;

/// <summary>
/// Ends the run of a pseudo console process when the process exits.
/// </summary>
internal static class ConPTYProcessWatcher
{
    // Cancels the source of its own run: the field may already hold the source of a newer run.
    public static async Task CancelOnExitAsync(PROCESS_INFORMATION pi, CancellationTokenSource cancellationTokenSource)
    {
        while (!cancellationTokenSource.IsCancellationRequested)
        {
            if (!ProcessAliveCheck(pi))
            {
                TryCancel(cancellationTokenSource);
                break;
            }
        }
        await Task.CompletedTask;
    }

    public static void TryCancel(CancellationTokenSource? cancellationTokenSource)
    {
        try
        {
            cancellationTokenSource?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A newer run replaced and disposed it; that run ended this process already.
        }
    }

    private static bool ProcessAliveCheck(PROCESS_INFORMATION pi)
    {
        switch (WaitForSingleObject(pi.hProcess, 2000))
        {
            case WAIT_OBJECT_0:
                return false;

            case WAIT_TIMEOUT:
                return true;
            default:
                // WAIT_FAILED: the handle is closed. Reporting it as alive spun the check
                // loop without any wait.
                return false;
        }
    }
}
