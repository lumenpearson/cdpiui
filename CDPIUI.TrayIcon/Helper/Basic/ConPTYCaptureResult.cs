namespace CDPIUI.TrayIcon.Helper;

public sealed class ConPTYCaptureResult
{
    public string Output { get; init; } = string.Empty;
    public int ExitCode { get; init; }
    public bool TimedOut { get; init; }
}
