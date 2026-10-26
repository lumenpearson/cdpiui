using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Execution;

/// <summary>
/// The issues and failed attempts a BlockCheck scan reports.
/// </summary>
internal static class BlockCheckScanIssues
{
    public static BlockCheckIssue Error(string code, string message, string? subjectId = null) =>
        new(BlockCheckIssueSeverity.Error, code, message, subjectId);

    public static BlockCheckIssue Warning(string code, string message, string? subjectId = null) =>
        new(BlockCheckIssueSeverity.Warning, code, message, subjectId);

    public static BlockCheckIssue Canceled() =>
        Warning(
            "SCAN_CANCELED",
            "The scan was stopped by the user. The report contains only checks completed before cancellation.");

    public static ProbeAttempt FailedAttempt(string failureCode, string diagnostic) => new()
    {
        Success = false,
        TimeToFirstByteMs = -1,
        ExitCode = -1,
        FailureCode = failureCode,
        Diagnostic = diagnostic,
    };
}
