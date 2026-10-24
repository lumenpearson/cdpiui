using System;
using System.Collections.Generic;
using System.Linq;
using CDPIUI.AddOns.ConfigImport;
using CDPIUI.AddOns.ConfigShare;

namespace CDPIUI.Helper.AddOns.ConfigImport;

/// <summary>
/// Chooses the component a configuration file is for and converts the file for it.
/// </summary>
internal sealed class ConfigImportFileAnalyzer
{
    private readonly ConfigImportService _importService;

    public ConfigImportFileAnalyzer(ConfigImportService importService)
    {
        _importService = importService;
    }

    /// <param name="requestedTargetId">The component the import was opened for; used when the file does not show its component.</param>
    public AnalyzedConfigImport Analyze(
        string filePath,
        IReadOnlyList<ConfigImportTarget> targets,
        string requestedTargetId)
    {
        if (ConfigShareService.IsSupported(filePath))
        {
            ConfigImportResult shared = _importService.Import(filePath);
            return new AnalyzedConfigImport(shared, targets.Any(target => target.ComponentId == shared.Target.ComponentId));
        }
        IReadOnlyList<ConfigImportTarget> matches = _importService.FindMatchingTargets(filePath, targets);
        ConfigImportTarget target = matches.FirstOrDefault()
            ?? FindRequestedTarget(targets, requestedTargetId)
            ?? targets.FirstOrDefault()
            ?? new ConfigImportTarget(string.Empty, string.Empty, string.Empty, null);
        return new AnalyzedConfigImport(
            _importService.Import(filePath, target),
            matches.Count > 0);
    }

    private static ConfigImportTarget FindRequestedTarget(
        IReadOnlyList<ConfigImportTarget> targets,
        string requestedTargetId) =>
        targets.FirstOrDefault(target =>
            string.Equals(target.ComponentId, requestedTargetId, StringComparison.OrdinalIgnoreCase));
}
