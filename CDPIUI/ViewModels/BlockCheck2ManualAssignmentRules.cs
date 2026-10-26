using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Reporting;

namespace CDPIUI.ViewModels;

/// <summary>
/// Matching rules of the manual preset builder: which targets a site-list assignment may use,
/// which evidence rows an assignment covers and how assignments and their scopes are keyed.
/// </summary>
internal static class BlockCheck2ManualAssignmentRules
{
    public static string AssignmentKey(BlockCheckManualAssignment assignment) =>
        $"{assignment.StrategyId}|{assignment.AnchorTargetId}|{assignment.ScopeKind}|{assignment.SiteListPath}";

    public static string ManualScopeKey(BlockCheck2ManualAssignmentItem item) =>
        $"{item.Target.IpVersion}|{item.Target.Transport}|{item.Target.Port}|{item.Target.Layer7Protocol}|" +
        (item.Assignment.ScopeKind == BlockCheckManualScopeKind.Site
            ? $"site:{BlockCheckTarget.NormalizeHost(item.Target.Host)}"
            : $"list:{Path.GetFullPath(item.Assignment.SiteListPath ?? string.Empty)}");

    public static string DescribeStrategySubject(
        BlockCheck2ManualAssignmentItem assignment,
        string strategyId) =>
        string.Equals(
            assignment.StrategyName,
            strategyId,
            StringComparison.CurrentCultureIgnoreCase)
            ? strategyId
            : $"{assignment.StrategyName} ({strategyId})";

    /// <summary>
    /// Whether <paramref name="target"/> comes from a site list and has the same host and
    /// connection as <paramref name="source"/>.
    /// </summary>
    public static bool IsCompatibleSiteListTarget(
        BlockCheckTarget target,
        BlockCheckTarget source,
        string normalizedSourceHost) =>
        target.HostListPaths.Count > 0 &&
        string.Equals(
            BlockCheckTarget.NormalizeHost(target.Host),
            normalizedSourceHost,
            StringComparison.OrdinalIgnoreCase) &&
        target.IpVersion == source.IpVersion &&
        target.Transport == source.Transport &&
        target.Port == source.Port &&
        string.Equals(
            target.Layer7Protocol,
            source.Layer7Protocol,
            StringComparison.Ordinal);

    public static bool AssignmentMatchesEvidence(
        BlockCheck2ManualAssignmentItem item,
        BlockCheck2StrategyEvidenceItem evidence) =>
        string.Equals(item.Assignment.StrategyId, evidence.StrategyId, StringComparison.OrdinalIgnoreCase) &&
        (item.Assignment.ScopeKind == BlockCheckManualScopeKind.Site
            ? string.Equals(
                BlockCheckTarget.NormalizeHost(item.Target.Host),
                BlockCheckTarget.NormalizeHost(evidence.Evidence.Target.Host),
                StringComparison.OrdinalIgnoreCase)
            : evidence.Evidence.Target.HostListPaths.Contains(
                  item.Assignment.SiteListPath ?? string.Empty,
                  StringComparer.OrdinalIgnoreCase) ||
              (string.Equals(
                   BlockCheckTarget.NormalizeHost(item.Target.Host),
                   BlockCheckTarget.NormalizeHost(evidence.Evidence.Target.Host),
                   StringComparison.OrdinalIgnoreCase) &&
               item.Target.IpVersion == evidence.Evidence.Target.IpVersion &&
               item.Target.Transport == evidence.Evidence.Target.Transport &&
               item.Target.Port == evidence.Evidence.Target.Port &&
               string.Equals(
                   item.Target.Layer7Protocol,
                   evidence.Evidence.Target.Layer7Protocol,
                   StringComparison.Ordinal)));

    /// <summary>
    /// The scopes an automatic profile covers: one per site list of the profile, anchored to a
    /// target of that list, or one per host when the profile has no site lists.
    /// </summary>
    public static IEnumerable<(BlockCheckTarget Target, BlockCheckManualScopeKind Scope, string? Path)>
        GetAutomaticScopes(BlockCheckReportProfile profile, BlockCheckTarget[] profileTargets) =>
        profile.HostListPaths.Count > 0
            ? profile.HostListPaths.Select(path => (
                profileTargets.FirstOrDefault(target => target.HostListPaths.Contains(
                    path,
                    StringComparer.OrdinalIgnoreCase)) ?? profileTargets.FirstOrDefault(),
                BlockCheckManualScopeKind.SiteList,
                (string?)path))
                .Where(scope => scope.Item1 != null)
                .Select(scope => (scope.Item1!, scope.Item2, scope.Item3))
            : profileTargets
                .DistinctBy(target => BlockCheckTarget.NormalizeHost(target.Host), StringComparer.OrdinalIgnoreCase)
                .Select(target => (target, BlockCheckManualScopeKind.Site, (string?)null));
}
