using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.AddOns.BlockCheck2.Reporting;

namespace CDPIUI.Tests.AddOns;

public sealed class BlockCheckReportTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "cdpiui-report-" + Guid.NewGuid().ToString("N"));
    private static readonly BlockCheckReportSerializer Serializer = new();
    private static readonly BlockCheckReportImportService Importer = new();

    public BlockCheckReportTests() => Directory.CreateDirectory(directory);

    public void Dispose() => Directory.Delete(directory, true);

    private static BlockCheckReportTarget Target(
        string id, string host, string path, BlockCheckProtocol protocol,
        BlockCheckIpVersion ip = BlockCheckIpVersion.IPv4, int? port = null)
    {
        BlockCheckTransport transport = protocol == BlockCheckProtocol.Quic ? BlockCheckTransport.Udp : BlockCheckTransport.Tcp;
        int actualPort = port ?? (protocol == BlockCheckProtocol.Http ? 80 : 443);
        return new BlockCheckReportTarget(id, host, path, protocol, ip, transport, actualPort, $"{host}|{ip}|{transport}|{actualPort}");
    }

    private static BlockCheckReportProbe Probe(
        string kind, string strategy, string target, int attempts, int successes,
        double? median = null, int[]? statuses = null, string[]? failures = null) =>
        new(kind, 0, strategy, target, attempts, successes,
            attempts == 0 ? 0 : (double)successes / attempts, median, median, statuses ?? [], failures ?? []);

    private static BlockCheckReportProfile Profile(
        string name, string primary, string[] targetIds, string layer7 = "tls", int port = 443,
        string[]? domains = null, string[]? lists = null, string[]? fallbacks = null, bool bestEffort = false) =>
        new(name, BlockCheckIpVersion.IPv4, BlockCheckTransport.Tcp, port, layer7,
            domains ?? [], lists ?? [], primary, fallbacks ?? [], targetIds, bestEffort);

    private static BlockCheckReport Report(
        IReadOnlyList<BlockCheckReportTarget>? targets = null,
        IReadOnlyList<BlockCheckReportProbe>? probes = null,
        IReadOnlyList<BlockCheckReportProfile>? profiles = null,
        IReadOnlyList<BlockCheckIssue>? issues = null,
        IReadOnlyList<BlockCheckReportValidationAttempt>? attempts = null,
        string catalog = "2026.08.11-standard",
        string preset = "--filter-tcp=443 --lua-desync=fake",
        int schema = 2) => new()
    {
        SchemaVersion = schema,
        CreatedAtUtc = new DateTimeOffset(2026, 10, 1, 12, 30, 45, TimeSpan.Zero),
        CatalogVersion = catalog,
        RunPreset = BlockCheckRunPreset.Balanced,
        Success = true,
        WasCanceled = false,
        IsBestEffort = true,
        Targets = targets ?? [Target("t1", "example.com", "/", BlockCheckProtocol.Tls12)],
        Probes = probes ?? [],
        Profiles = profiles ?? [],
        Issues = issues ?? [],
        ValidationAttempts = attempts ?? [],
        PresetArguments = preset,
    };

    private static BlockCheckReport FullReport() => Report(
        targets:
        [
            Target("t1", "example.com", "/", BlockCheckProtocol.Http),
            Target("t2", "example.org", "/a?b=1", BlockCheckProtocol.Tls12, BlockCheckIpVersion.IPv6, 8443),
            Target("t3", "example.net", "/", BlockCheckProtocol.Quic),
            Target("t4", "example.com", "/", BlockCheckProtocol.TlsAuto),
            Target("t5", "example.com", "/", BlockCheckProtocol.Tls13),
        ],
        probes:
        [
            Probe("baseline", "", "t1", 3, 0, failures: ["timeout", "connection-failed"]),
            Probe("strategy", "tls-multisplit-1", "t4", 4, 3, 123.4, [200, 301]),
            Probe("strategy", "http-fake-md5", "t3", 2, 2, 50),
        ],
        profiles:
        [
            Profile("TLS", "tls-multisplit-1", ["t4", "t5"], domains: ["example.com"], fallbacks: ["http-fake-md5", "unknown-fallback"], bestEffort: true),
            Profile("HTTP", "http-fake-md5", ["t1"], "http", 80),
        ],
        issues:
        [
            new BlockCheckIssue(BlockCheckIssueSeverity.Warning, "SLOW", "It was slow: really.", "t1"),
            new BlockCheckIssue(BlockCheckIssueSeverity.Info, "DONE", "Done."),
        ]);

    [Fact]
    public void TextReportIsParsedBack()
    {
        string text = Serializer.SerializeText(FullReport());

        BlockCheckReport parsed = BlockCheckTextReportParser.Parse(text);

        Assert.Equal(2, parsed.SchemaVersion);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 12, 30, 45, TimeSpan.Zero), parsed.CreatedAtUtc);
        Assert.Equal("2026.08.11-standard", parsed.CatalogVersion);
        Assert.Equal(BlockCheckRunPreset.Balanced, parsed.RunPreset);
        Assert.True(parsed.Success);
        Assert.False(parsed.WasCanceled);
        Assert.True(parsed.IsBestEffort);
        Assert.Equal("--filter-tcp=443 --lua-desync=fake", parsed.PresetArguments);

        Assert.Equal(
            ["example.com|/|Http|IPv4|Tcp|80", "example.org|/a?b=1|Tls12|IPv6|Tcp|8443",
             "example.net|/|Quic|IPv4|Udp|443", "example.com|/|TlsAuto|IPv4|Tcp|443",
             "example.com|/|Tls13|IPv4|Tcp|443"],
            parsed.Targets.Select(t => $"{t.Host}|{t.Path}|{t.Protocol}|{t.IpVersion}|{t.Transport}|{t.Port}"));
        Assert.Equal("imported-target-00001", parsed.Targets[0].Id);
        Assert.Equal("example.com|IPv4|Tcp|80|http", parsed.Targets[0].RuntimeRoute);

        Assert.Equal(3, parsed.Probes.Count);
        BlockCheckReportProbe baseline = parsed.Probes[0];
        Assert.Equal("baseline", baseline.Kind);
        Assert.Equal("", baseline.StrategyId);
        Assert.Equal("imported-target-00001", baseline.TargetId);
        Assert.Equal((3, 0, 0d), (baseline.AttemptCount, baseline.SuccessCount, baseline.SuccessRate));
        Assert.Null(baseline.MedianTimeToFirstByteMs);
        Assert.Equal(["timeout", "connection-failed"], baseline.FailureCodes);
        BlockCheckReportProbe strategy = parsed.Probes[1];
        Assert.Equal("tls-multisplit-1", strategy.StrategyId);
        Assert.Equal("imported-target-00004", strategy.TargetId);
        Assert.Equal(123d, strategy.MedianTimeToFirstByteMs);
        Assert.Equal([200, 301], strategy.HttpStatusCodes);
        Assert.Equal(0.75d, strategy.SuccessRate);

        Assert.Equal(2, parsed.Profiles.Count);
        BlockCheckReportProfile tls = parsed.Profiles[0];
        Assert.Equal("TLS", tls.Name);
        Assert.True(tls.IsBestEffort);
        Assert.Equal(["example.com"], tls.Domains);
        Assert.Equal("tls-multisplit-1", tls.PrimaryStrategyId);
        Assert.Equal(["http-fake-md5", "unknown-fallback"], tls.FallbackStrategyIds);
        Assert.Equal(["imported-target-00004", "imported-target-00005"], tls.TargetIds);
        Assert.Equal(["imported-target-00001"], parsed.Profiles[1].TargetIds);

        Assert.Equal(
            [("Warning", "SLOW", "It was slow: really.", "t1"), ("Info", "DONE", "Done.", (string?)null)],
            parsed.Issues.Select(i => (i.Severity.ToString(), i.Code, i.Message, i.SubjectId)));
    }

    [Fact]
    public void MinimalTextReportHasEmptySections()
    {
        string text = Serializer.SerializeText(Report(targets: [], preset: ""));

        BlockCheckReport parsed = BlockCheckTextReportParser.Parse(text.Replace("\r\n", "\r"));

        Assert.Empty(parsed.Targets);
        Assert.Empty(parsed.Probes);
        Assert.Empty(parsed.Profiles);
        Assert.Empty(parsed.Issues);
        Assert.Equal("", parsed.PresetArguments);
    }

    [Fact]
    public void SiteListsInTextReportAddAnInferredScopeWarning()
    {
        BlockCheckReport report = Report(profiles:
            [Profile("P", "tls-multisplit-1", ["t1"], lists: ["C:/lists/a.txt", "C:/lists/b.txt"])]);

        BlockCheckReport parsed = BlockCheckTextReportParser.Parse(Serializer.SerializeText(report));

        Assert.Equal(["C:/lists/a.txt", "C:/lists/b.txt"], parsed.Profiles[0].HostListPaths);
        Assert.Equal("REPORT_TEXT_SCOPE_INFERRED", Assert.Single(parsed.Issues).Code);
    }

    [Fact]
    public void TargetsWithPathsDifferingOnlyInCaseStaySeparate()
    {
        BlockCheckReport report = Report(
            targets:
            [
                Target("t1", "example.com", "/A", BlockCheckProtocol.Tls12),
                Target("t2", "example.com", "/a", BlockCheckProtocol.Tls12),
            ],
            probes: [Probe("baseline", "", "t1", 1, 1), Probe("baseline", "", "t2", 1, 0)]);

        BlockCheckReport parsed = BlockCheckTextReportParser.Parse(Serializer.SerializeText(report));

        Assert.Equal(["/A", "/a"], parsed.Targets.Select(target => target.Path));
        Assert.Equal(
            ["imported-target-00001", "imported-target-00002"],
            parsed.Probes.Select(probe => probe.TargetId));
    }

    [Fact]
    public void TargetsWithTheSameTextAreMerged()
    {
        // "example.com" and "example.com:443" are two targets with the same route.
        BlockCheckReport report = Report(
            targets:
            [
                Target("t1", "example.com", "/", BlockCheckProtocol.Tls12),
                Target("t2", "example.com", "/", BlockCheckProtocol.Tls12, port: 443),
            ],
            probes: [Probe("baseline", "", "t1", 1, 1), Probe("baseline", "", "t2", 1, 0)]);

        BlockCheckReport parsed = BlockCheckTextReportParser.Parse(Serializer.SerializeText(report));

        Assert.Equal("imported-target-00001", Assert.Single(parsed.Targets).Id);
        Assert.All(parsed.Probes, probe => Assert.Equal("imported-target-00001", probe.TargetId));
    }

    [Fact]
    public void OldTextReportWithoutCanceledHeaderIsAccepted()
    {
        string text = Serializer.SerializeText(Report()).Replace("Canceled: False", "");

        Assert.False(BlockCheckTextReportParser.Parse(text).WasCanceled);
    }

    [Theory]
    [InlineData("Created (UTC): 2026-10-01T12:30:45.0000000+00:00", "Created (UTC): yesterday")]
    [InlineData("Catalog: 2026.08.11-standard", "Katalog: x")]
    [InlineData("Mode: Balanced", "Mode: Turbo")]
    [InlineData("Success: True", "Success: yes")]
    [InlineData("Canceled: False", "Canceled: 0")]
    [InlineData("Best effort: True", "Best effort:")]
    [InlineData("  Connection: TLS 1.2 · IPv4 · TCP:443", "  Connect: TLS 1.2")]
    [InlineData("  Connection: TLS 1.2 · IPv4 · TCP:443", "  Connection: SMTP · IPv4 · TCP:25")]
    [InlineData("  Connection: TLS 1.2 · IPv4 · TCP:443", "  Connection: TLS 1.2 · IPv4 · TCP")]
    [InlineData("- https://example.com/", "- https://10.0.0.1/")]
    [InlineData("Primary: tls-multisplit-1", "Primary: ")]
    [InlineData("tls/Tcp/IPv4:443", "tls/Tcp/IPv5:443")]
    [InlineData("tls/Tcp/IPv4:443", "tls/Sctp/IPv4:443")]
    [InlineData("- strategy tls-multisplit-1 / https://example.com/", "- strategy tls-multisplit-1 / https://example.org/")]
    public void BrokenTextReportsAreRejected(string original, string replacement)
    {
        BlockCheckReport report = Report(
            probes: [Probe("strategy", "tls-multisplit-1", "t1", 1, 1)],
            profiles: [Profile("P", "tls-multisplit-1", ["t1"])]);
        string text = Serializer.SerializeText(report);
        Assert.Contains(original, text);
        text = text.Replace(original, replacement);

        Assert.Throws<InvalidDataException>(() => BlockCheckTextReportParser.Parse(text));
    }

    [Fact]
    public void OtherTextFileIsRejected()
    {
        Assert.Throws<InvalidDataException>(() => BlockCheckTextReportParser.Parse("Some other file\nCatalog: x"));
    }

    [Fact]
    public void BlankTextIsAnArgumentError()
    {
        Assert.ThrowsAny<ArgumentException>(() => BlockCheckTextReportParser.Parse(" "));
    }

    [Fact]
    public void ImportRestoresProfilesAndReportsUnknownParts()
    {
        BlockCheckReportImportResult result = Importer.Import(Report(
            targets: [Target("t1", "example.com", "/", BlockCheckProtocol.Tls12)],
            profiles:
            [
                Profile("Known", "tls-multisplit-1", ["t1"], fallbacks: ["http-fake-md5", "missing-fallback"],
                    lists: [Path.Combine(directory, "missing.txt")]),
                Profile("Unknown", "missing-primary", ["t1"]),
            ],
            probes:
            [
                Probe("baseline", "", "t1", 2, 0, failures: ["timeout"]),
                Probe("strategy", "tls-multisplit-1", "t1", 3, 2, 40, [200]),
                Probe("other", "x", "t1", 1, 1),
            ],
            catalog: "old-catalog"));

        Assert.Equal(
            ["REPORT_CATALOG_VERSION_MISMATCH", "REPORT_SITE_LIST_MISSING", "REPORT_STRATEGY_UNKNOWN", "REPORT_STRATEGY_UNKNOWN"],
            result.Issues.Select(issue => issue.Code));
        Assert.Contains(result.Issues, issue => issue.SubjectId == "missing-fallback");
        Assert.Contains(result.Issues, issue => issue.SubjectId == "missing-primary");

        var profile = Assert.Single(result.Session.RunResult.Synthesis!.Profiles);
        Assert.Equal("Known", profile.Name);
        Assert.Equal("tls-multisplit-1", profile.Primary.Id);
        Assert.Equal(["http-fake-md5"], profile.Fallbacks.Select(strategy => strategy.Id));

        var baseline = Assert.Single(result.Session.RunResult.Scan.BaselineResults);
        Assert.Equal(2, baseline.Attempts.Count);
        Assert.All(baseline.Attempts, attempt => Assert.Equal("timeout", attempt.FailureCode));
        var probe = Assert.Single(result.Session.RunResult.Scan.ProbeResults);
        Assert.Equal([true, true, false], probe.Attempts.Select(attempt => attempt.Success));
        Assert.Equal("imported-probe-failed", probe.Attempts[2].FailureCode);
        Assert.Equal(200, probe.Attempts[0].HttpStatusCode);

        BlockCheckTarget target = Assert.Single(result.Session.Targets);
        Assert.Equal(443, target.Port);
        Assert.Equal([Path.Combine(directory, "missing.txt")], target.HostListPaths);
        Assert.Equal(result.Issues.Count, result.Report.Issues.Count);
    }

    [Fact]
    public void InvalidReportsAreRejected()
    {
        var target = Target("t1", "example.com", "/", BlockCheckProtocol.Tls12);
        BlockCheckReport[] reports =
        [
            Report(schema: 1),
            new BlockCheckReport { Targets = null!, Profiles = [], Probes = [], Issues = [], ValidationAttempts = [] },
            new BlockCheckReport { Probes = null! },
            new BlockCheckReport { Profiles = null! },
            new BlockCheckReport { Issues = null! },
            new BlockCheckReport { ValidationAttempts = null! },
            Report(targets: [null!]),
            Report(targets: [target with { Id = " " }]),
            Report(targets: [target with { Host = "bad host" }]),
            Report(targets: [target with { Port = 0 }]),
            Report(targets: [target with { Port = 65536 }]),
            Report(targets: [target with { Protocol = (BlockCheckProtocol)9 }]),
            Report(targets: [target with { IpVersion = (BlockCheckIpVersion)9 }]),
            Report(targets: [target with { Transport = (BlockCheckTransport)9 }]),
            Report(targets: [target, target]),
            Report(targets: Enumerable.Range(0, 20_001).Select(i => target with { Id = $"t{i}" }).ToArray()),
            Report(probes: [null!]),
            Report(probes: [Probe("baseline", "", "t2", 1, 1)]),
            Report(probes: [Probe(" ", "", "t1", 1, 1)]),
            Report(probes: [Probe("baseline", "", "t1", -1, 0)]),
            Report(probes: [Probe("baseline", "", "t1", 1001, 0)]),
            Report(probes: [Probe("baseline", "", "t1", 1, -1)]),
            Report(probes: [Probe("baseline", "", "t1", 1, 2)]),
            Report(probes: [Probe("baseline", "", "t1", 1, 1) with { HttpStatusCodes = null! }]),
            Report(probes: [Probe("baseline", "", "t1", 1, 1) with { FailureCodes = null! }]),
            Report(profiles: [null!]),
            Report(profiles: [Profile(" ", "p", ["t1"])]),
            Report(profiles: [Profile("P", " ", ["t1"])]),
            Report(profiles: [Profile("P", "p", ["t2"])]),
            Report(profiles: [Profile("P", "p", ["t1"]) with { Domains = null! }]),
            Report(profiles: [Profile("P", "p", ["t1"]) with { HostListPaths = null! }]),
            Report(profiles: [Profile("P", "p", ["t1"]) with { FallbackStrategyIds = null! }]),
            Report(profiles: [Profile("P", "p", ["t1"]) with { TargetIds = null! }]),
            Report(issues: [null!]),
            Report(issues: [new BlockCheckIssue(BlockCheckIssueSeverity.Info, " ", "m")]),
            Report(issues: [new BlockCheckIssue((BlockCheckIssueSeverity)9, "C", "m")]),
            Report(attempts: [null!]),
            Report(attempts: [new BlockCheckReportValidationAttempt(-1, true, false, [], [])]),
            Report(attempts: [new BlockCheckReportValidationAttempt(1, true, false, null!, [])]),
            Report(attempts: [new BlockCheckReportValidationAttempt(1, true, false, [], null!)]),
            Report(attempts: [new BlockCheckReportValidationAttempt(1, false, false, ["t2"], [])]),
            Report(preset: "--wf-tcp=443 --wf-save=C:\\Windows\\evil.sys"),
        ];

        for (int index = 0; index < reports.Length; index++)
        {
            BlockCheckReport report = reports[index];
            var exception = Record.Exception(() => Importer.Import(report));
            Assert.True(exception is InvalidDataException, $"Report #{index} gave {exception?.GetType().Name ?? "no error"}.");
        }
    }

    [Fact]
    public void ValidAttemptIsAccepted()
    {
        var result = Importer.Import(Report(attempts: [new BlockCheckReportValidationAttempt(0, false, true, ["t1"], ["x"])]));

        Assert.Empty(result.Issues);
        Assert.Equal([0], result.Report.ValidationAttempts.Select(attempt => attempt.CandidateNumber));
    }

    [Fact]
    public async Task TextReportScopeIsRecoveredFromSiteLists()
    {
        string listA = Path.Combine(directory, "a.txt");
        string listB = Path.Combine(directory, "b.txt");
        File.WriteAllLines(listA, ["\uFEFF# comment", "", "^Example.COM.", "https://other.org/path", "not a host!"]);
        BlockCheckReport report = Report(
            targets:
            [
                Target("t1", "www.example.com", "/", BlockCheckProtocol.Tls12),
                Target("t2", "other.org", "/", BlockCheckProtocol.Tls12),
                Target("t3", "unrelated.net", "/", BlockCheckProtocol.Tls12),
            ],
            profiles: [Profile("P", "tls-multisplit-1", ["t1"], lists: [listA, listB])]);
        string file = Path.Combine(directory, "report.txt");
        await File.WriteAllTextAsync(file, Serializer.SerializeText(report));

        BlockCheckReportImportResult result = await Importer.LoadAsync(file);

        Assert.Equal([[listA], [listA], []], result.Session.Targets.Select(target => target.HostListPaths.ToArray()));
        Assert.Contains(result.Issues, issue => issue.Code == "REPORT_SITE_LIST_MISSING" && issue.SubjectId == listB);
        Assert.Contains(result.Report.Issues, issue => issue.Code == "REPORT_TEXT_SCOPE_INFERRED");
    }

    [Fact]
    public async Task JsonReportIsLoaded()
    {
        string file = Path.Combine(directory, "report.json");
        await File.WriteAllTextAsync(file, "  \n" + Serializer.SerializeJson(FullReport()));

        BlockCheckReportImportResult result = await Importer.LoadJsonAsync(file);

        Assert.Equal(5, result.Session.Targets.Count);
        Assert.Equal(["t4", "t5"], result.Report.Profiles[0].TargetIds);
        Assert.Equal(BlockCheckRunPreset.Balanced, result.Report.RunPreset);
    }

    [Fact]
    public async Task BadFilesAreRejected()
    {
        string empty = Path.Combine(directory, "empty.json");
        string broken = Path.Combine(directory, "broken.json");
        await File.WriteAllTextAsync(empty, "");
        await File.WriteAllTextAsync(broken, "{ \"schemaVersion\": ");

        await Assert.ThrowsAsync<FileNotFoundException>(() => Importer.LoadAsync(Path.Combine(directory, "none.json")));
        await Assert.ThrowsAsync<InvalidDataException>(() => Importer.LoadAsync(empty));
        await Assert.ThrowsAsync<InvalidDataException>(() => Importer.LoadAsync(broken));
        Assert.Throws<InvalidDataException>(() => Serializer.DeserializeJson("null"));
    }
}
