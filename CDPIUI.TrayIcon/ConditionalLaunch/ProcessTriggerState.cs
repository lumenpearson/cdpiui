namespace CDPIUI.TrayIcon.ConditionalLaunch;

/// <summary>
/// The last seen state of the process of one process trigger, and its pending delayed run.
/// </summary>
internal sealed class ProcessTriggerState(bool lastIsRunning)
{
    public bool LastIsRunning { get; set; } = lastIsRunning;
    public CancellationTokenSource? Pending { get; set; }

    public void CancelPending()
    {
        Pending?.Cancel();
        Pending = null;
    }
}
