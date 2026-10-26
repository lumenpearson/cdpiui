using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Reporting;

/// <summary>
/// Rejects an imported report whose schema, collections, counts or items are invalid before
/// anything of it is used. The checks run in a fixed order and each group throws its own
/// <see cref="InvalidDataException"/> message.
/// </summary>
internal static class BlockCheckReportValidator
{
    private const int MaximumTargets = 20_000;
    private const int MaximumProbes = 200_000;
    private const int MaximumProfiles = 20_000;
    private const int MaximumProbeAttempts = 1_000;

    public static void Validate(BlockCheckReport report)
    {
        if (report.SchemaVersion != 2)
        {
            throw new InvalidDataException(
                $"Unsupported BlockCheck2 report schema {report.SchemaVersion}. Expected schema 2.");
        }
        if (report.Targets is null || report.Probes is null || report.Profiles is null ||
            report.ValidationAttempts is null || report.Issues is null)
        {
            throw new InvalidDataException("The BlockCheck2 report contains null collections.");
        }
        if (report.Targets.Count > MaximumTargets || report.Probes.Count > MaximumProbes ||
            report.Profiles.Count > MaximumProfiles)
        {
            throw new InvalidDataException("The BlockCheck2 report contains unreasonable item counts.");
        }
        if (report.Targets.Any(target => !IsValidTarget(target)) ||
            report.Targets.Select(target => target.Id).Distinct(StringComparer.Ordinal).Count() !=
            report.Targets.Count)
        {
            throw new InvalidDataException("The BlockCheck2 report contains invalid or duplicate targets.");
        }
        HashSet<string> targetIds = report.Targets
            .Select(target => target.Id)
            .ToHashSet(StringComparer.Ordinal);
        if (report.Probes.Any(probe => !IsValidProbe(probe, targetIds)))
        {
            throw new InvalidDataException("The BlockCheck2 report contains invalid probe evidence.");
        }
        if (report.Profiles.Any(profile => !IsValidProfile(profile, targetIds)))
        {
            throw new InvalidDataException("The BlockCheck2 report contains invalid preset profiles.");
        }
        if (report.Issues.Any(issue => !IsValidIssue(issue)) ||
            report.ValidationAttempts.Any(attempt => !IsValidAttempt(attempt, targetIds)))
        {
            throw new InvalidDataException("The BlockCheck2 report contains invalid diagnostics.");
        }
    }

    private static bool IsValidTarget(BlockCheckReportTarget? target) =>
        target is not null &&
        !string.IsNullOrWhiteSpace(target.Id) &&
        BlockCheckTarget.IsValidHost(target.Host) &&
        target.Port is >= 1 and <= 65535 &&
        Enum.IsDefined(target.Protocol) &&
        Enum.IsDefined(target.IpVersion) &&
        Enum.IsDefined(target.Transport);

    private static bool IsValidProbe(BlockCheckReportProbe? probe, IReadOnlySet<string> targetIds) =>
        probe is not null &&
        targetIds.Contains(probe.TargetId) &&
        !string.IsNullOrWhiteSpace(probe.Kind) &&
        probe.AttemptCount is >= 0 and <= MaximumProbeAttempts &&
        probe.SuccessCount >= 0 &&
        probe.SuccessCount <= probe.AttemptCount &&
        probe.HttpStatusCodes is not null &&
        probe.FailureCodes is not null;

    private static bool IsValidProfile(BlockCheckReportProfile? profile, IReadOnlySet<string> targetIds) =>
        profile is not null &&
        !string.IsNullOrWhiteSpace(profile.Name) &&
        !string.IsNullOrWhiteSpace(profile.PrimaryStrategyId) &&
        profile.Domains is not null &&
        profile.HostListPaths is not null &&
        profile.FallbackStrategyIds is not null &&
        profile.TargetIds is not null &&
        profile.TargetIds.All(targetIds.Contains);

    private static bool IsValidIssue(BlockCheckIssue? issue) =>
        issue is not null &&
        !string.IsNullOrWhiteSpace(issue.Code) &&
        Enum.IsDefined(issue.Severity);

    private static bool IsValidAttempt(BlockCheckReportValidationAttempt? attempt, IReadOnlySet<string> targetIds) =>
        attempt is not null &&
        attempt.CandidateNumber >= 0 &&
        attempt.FailedTargetIds is not null &&
        attempt.ExcludedStrategyIds is not null &&
        attempt.FailedTargetIds.All(targetIds.Contains);
}
