using System.Text;
using CDPIUI.AddOns.BlockCheck2.Catalog;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;

namespace CDPIUI.AddOns.BlockCheck2.Reporting;

public sealed class BlockCheckReportImportResult
{
    public BlockCheckReport Report { get; init; } = new();
    public BlockCheckResultSession Session { get; init; } = new();
    public IReadOnlyList<BlockCheckIssue> Issues { get; init; } = [];
}

public sealed class BlockCheckReportImportService
{
    private const long MaximumReportBytes = 64L * 1024L * 1024L;
    private readonly BlockCheckReportSerializer _serializer;

    public BlockCheckReportImportService(BlockCheckReportSerializer? serializer = null)
    {
        _serializer = serializer ?? new BlockCheckReportSerializer();
    }

    public async Task<BlockCheckReportImportResult> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        string fullPath = Path.GetFullPath(filePath);
        FileInfo file = new(fullPath);
        if (!file.Exists)
        {
            throw new FileNotFoundException("The BlockCheck2 report was not found.", fullPath);
        }
        if (file.Length <= 0 || file.Length > MaximumReportBytes)
        {
            throw new InvalidDataException("The BlockCheck2 report is empty or exceeds 64 MiB.");
        }

        string json = await File.ReadAllTextAsync(
                fullPath,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                cancellationToken)
            .ConfigureAwait(false);
        BlockCheckReport report = json.AsSpan().TrimStart().StartsWith("{".AsSpan(), StringComparison.Ordinal)
            ? _serializer.DeserializeJson(json)
            : BlockCheckTextReportParser.Parse(json);
        return Import(report);
    }

    public Task<BlockCheckReportImportResult> LoadJsonAsync(
        string filePath,
        CancellationToken cancellationToken = default) =>
        LoadAsync(filePath, cancellationToken);

    public BlockCheckReportImportResult Import(BlockCheckReport sourceReport)
    {
        ArgumentNullException.ThrowIfNull(sourceReport);
        BlockCheckReportValidator.Validate(sourceReport);

        StrategyCatalog catalog = StrategyCatalogLoader.LoadBuiltIn();
        List<BlockCheckIssue> importIssues = [];
        if (!string.Equals(
                sourceReport.CatalogVersion,
                catalog.CatalogVersion,
                StringComparison.OrdinalIgnoreCase))
        {
            importIssues.Add(Warning(
                "REPORT_CATALOG_VERSION_MISMATCH",
                $"The report uses catalog '{sourceReport.CatalogVersion}', while this CDPIUI build " +
                $"contains '{catalog.CatalogVersion}'. Unknown strategies cannot be edited or tested."));
        }

        BlockCheckTarget[] targets = RestoreTargets(sourceReport);
        AddMissingSiteListWarnings(sourceReport, importIssues);

        ProbeResult[] baseline = RestoreProbes(sourceReport, "baseline");
        ProbeResult[] strategyProbes = RestoreProbes(sourceReport, "strategy");
        IReadOnlyList<Zapret2ProfilePlan> profiles = RestoreProfiles(
            sourceReport,
            catalog,
            importIssues);

        EnsurePresetWritesNoFiles(sourceReport.PresetArguments);

        BlockCheckReport report = CopyWithIssues(sourceReport, sourceReport.Issues
            .Concat(importIssues)
            .DistinctBy(issue => (issue.Severity, issue.Code, issue.Message, issue.SubjectId))
            .ToArray());
        BlockCheckResultSession session = new()
        {
            Catalog = catalog,
            Targets = targets,
            RunResult = CreateRunResult(report, baseline, strategyProbes, profiles),
            Report = report,
            RunOptions = BlockCheckRunPresetFactory.Create(report.RunPreset),
        };

        return new BlockCheckReportImportResult
        {
            Report = report,
            Session = session,
            Issues = importIssues,
        };
    }

    private static BlockCheckTarget[] RestoreTargets(BlockCheckReport report)
    {
        Dictionary<string, IReadOnlyList<string>> hostListsByTarget =
            BlockCheckReportHostListScope.BuildByTarget(report);
        return report.Targets
            .Select(target => new BlockCheckTarget
            {
                Id = target.Id,
                Host = target.Host,
                Path = target.Path,
                Protocol = target.Protocol,
                IpVersion = target.IpVersion,
                CustomPort = target.Port,
                HostListPaths = hostListsByTarget[target.Id],
            })
            .ToArray();
    }

    private static void AddMissingSiteListWarnings(BlockCheckReport report, List<BlockCheckIssue> issues)
    {
        foreach (string missingPath in report.Profiles
                     .SelectMany(profile => profile.HostListPaths)
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .Where(path => !File.Exists(path)))
        {
            issues.Add(Warning(
                "REPORT_SITE_LIST_MISSING",
                "A site list referenced by the imported report does not exist on this computer. " +
                "The saved config can be inspected, but that list scope must be repaired before use.",
                missingPath));
        }
    }

    private static ProbeResult[] RestoreProbes(BlockCheckReport report, string kind) => report.Probes
        .Where(probe => string.Equals(probe.Kind, kind, StringComparison.OrdinalIgnoreCase))
        .Select(ToProbeResult)
        .ToArray();

    private static void EnsurePresetWritesNoFiles(string presetArguments)
    {
        // The preset of a report runs in the tray with administrator rights: a report from
        // someone else must not make the component write files. The synthesizer
        // never writes such options, so any of them means the report was edited.
        try
        {
            CDPIUI.AddOns.ConfigShare.ConfigShareService.EnsureNoOutsideWrites(
                presetArguments,
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        }
        catch (CDPIUI.AddOns.ConfigShare.ConfigShareException exception)
        {
            throw new InvalidDataException(exception.Message, exception);
        }
    }

    private static BlockCheckRunResult CreateRunResult(
        BlockCheckReport report,
        IReadOnlyList<ProbeResult> baseline,
        IReadOnlyList<ProbeResult> strategyProbes,
        IReadOnlyList<Zapret2ProfilePlan> profiles) => new()
    {
        PreflightIssues = report.Issues,
        Scan = new BlockCheckScanResult
        {
            BaselineResults = baseline,
            ProbeResults = strategyProbes,
            WasCanceled = report.WasCanceled,
        },
        Synthesis = new BlockCheckSynthesisResult
        {
            Profiles = profiles,
            Configuration = new Zapret2WriteResult
            {
                CommandLine = report.PresetArguments,
            },
            IsBestEffort = report.IsBestEffort,
        },
        WasCanceled = report.WasCanceled,
    };

    private static ProbeResult ToProbeResult(BlockCheckReportProbe probe)
    {
        int status = probe.HttpStatusCodes.FirstOrDefault();
        string failure = probe.FailureCodes.FirstOrDefault() ?? "imported-probe-failed";
        List<ProbeAttempt> attempts = [];
        for (int index = 0; index < probe.SuccessCount; index++)
        {
            attempts.Add(new ProbeAttempt
            {
                Success = true,
                TimeToFirstByteMs = probe.MedianTimeToFirstByteMs ?? 0d,
                HttpStatusCode = status,
            });
        }
        for (int index = probe.SuccessCount; index < probe.AttemptCount; index++)
        {
            attempts.Add(new ProbeAttempt
            {
                Success = false,
                TimeToFirstByteMs = -1d,
                ExitCode = -1,
                FailureCode = failure,
            });
        }
        return new ProbeResult
        {
            StrategyId = probe.StrategyId,
            TargetId = probe.TargetId,
            Attempts = attempts,
        };
    }

    private static IReadOnlyList<Zapret2ProfilePlan> RestoreProfiles(
        BlockCheckReport report,
        StrategyCatalog catalog,
        ICollection<BlockCheckIssue> issues)
    {
        Dictionary<string, StrategyDefinition> strategies = catalog.Strategies
            .ToDictionary(strategy => strategy.Id, StringComparer.OrdinalIgnoreCase);
        List<Zapret2ProfilePlan> result = [];
        foreach (BlockCheckReportProfile source in report.Profiles)
        {
            if (!strategies.TryGetValue(source.PrimaryStrategyId, out StrategyDefinition? primary))
            {
                issues.Add(Warning(
                    "REPORT_STRATEGY_UNKNOWN",
                    "A strategy referenced by the imported report is absent from the active catalog.",
                    source.PrimaryStrategyId));
                continue;
            }

            Zapret2ProfilePlan profile = new()
            {
                Name = source.Name,
                Filter = new Zapret2ProfileFilter
                {
                    IpVersion = source.IpVersion,
                    Transport = source.Transport,
                    Port = source.Port,
                    Layer7Protocol = source.Layer7Protocol,
                    Domains = source.Domains,
                    HostListPaths = source.HostListPaths,
                },
                Primary = primary,
                TargetIds = source.TargetIds.ToHashSet(StringComparer.Ordinal),
                IsBestEffort = source.IsBestEffort,
            };
            foreach (string fallbackId in source.FallbackStrategyIds)
            {
                if (strategies.TryGetValue(fallbackId, out StrategyDefinition? fallback))
                {
                    profile.Fallbacks.Add(fallback);
                }
                else
                {
                    issues.Add(Warning(
                        "REPORT_STRATEGY_UNKNOWN",
                        "A fallback strategy referenced by the imported report is absent from the active catalog.",
                        fallbackId));
                }
            }
            result.Add(profile);
        }
        return result;
    }

    private static BlockCheckReport CopyWithIssues(
        BlockCheckReport source,
        IReadOnlyList<BlockCheckIssue> issues) => new()
        {
            SchemaVersion = source.SchemaVersion,
            CreatedAtUtc = source.CreatedAtUtc,
            CatalogVersion = source.CatalogVersion,
            RunPreset = source.RunPreset,
            Success = source.Success,
            WasCanceled = source.WasCanceled,
            IsBestEffort = source.IsBestEffort,
            Targets = source.Targets,
            Probes = source.Probes,
            Profiles = source.Profiles,
            ValidationAttempts = source.ValidationAttempts,
            Issues = issues,
            PresetArguments = source.PresetArguments,
        };

    private static BlockCheckIssue Warning(string code, string message, string? subjectId = null) =>
        new(BlockCheckIssueSeverity.Warning, code, message, subjectId);
}
