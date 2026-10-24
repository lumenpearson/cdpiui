using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Reporting;

/// <summary>
/// Assigns the site lists of an imported report to its targets.
/// </summary>
internal static class BlockCheckReportHostListScope
{
    public static Dictionary<string, IReadOnlyList<string>> BuildByTarget(
        BlockCheckReport report)
    {
        bool inferredTextScope = report.Issues.Any(issue =>
            string.Equals(issue.Code, "REPORT_TEXT_SCOPE_INFERRED", StringComparison.Ordinal));
        if (!inferredTextScope)
        {
            return report.Targets.ToDictionary(
                target => target.Id,
                target => (IReadOnlyList<string>)report.Profiles
                    .Where(profile => profile.TargetIds.Contains(target.Id, StringComparer.Ordinal))
                    .SelectMany(profile => profile.HostListPaths)
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                StringComparer.Ordinal);
        }

        // The legacy text export omits profile target IDs. Recover the scope from
        // the referenced files when they are still available. A missing file is
        // intentionally not assigned to every target because that would make a
        // manually built config look precise while silently using the wrong list.
        Dictionary<string, HashSet<string>?> domainsByPath = new(StringComparer.OrdinalIgnoreCase);
        foreach (string path in report.Profiles
                     .SelectMany(profile => profile.HostListPaths)
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            domainsByPath[path] = TryReadHostList(path);
        }

        return report.Targets.ToDictionary(
            target => target.Id,
            target => (IReadOnlyList<string>)domainsByPath
                .Where(item => item.Value != null && HostMatchesList(target.Host, item.Value))
                .Select(item => item.Key)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            StringComparer.Ordinal);
    }

    private static HashSet<string>? TryReadHostList(string path)
    {
        try
        {
            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                return null;
            }

            HashSet<string> domains = new(StringComparer.OrdinalIgnoreCase);
            foreach (string source in File.ReadLines(fullPath))
            {
                string line = source.Trim().TrimStart('\uFEFF');
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }
                line = line.TrimStart('^').TrimEnd('.');
                if (Uri.TryCreate(line, UriKind.Absolute, out Uri? uri))
                {
                    line = uri.Host;
                }
                string domain = BlockCheckTarget.NormalizeHost(line);
                if (BlockCheckTarget.IsValidHost(domain))
                {
                    domains.Add(domain);
                }
            }
            return domains;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static bool HostMatchesList(string host, IReadOnlySet<string> domains)
    {
        string candidate = BlockCheckTarget.NormalizeHost(host);
        while (candidate.Length > 0)
        {
            if (domains.Contains(candidate))
            {
                return true;
            }
            int separator = candidate.IndexOf('.');
            candidate = separator < 0 ? string.Empty : candidate[(separator + 1)..];
        }
        return false;
    }
}
