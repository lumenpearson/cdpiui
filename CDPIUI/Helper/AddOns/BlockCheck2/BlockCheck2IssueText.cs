using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.Helper.UserExperience;
using System;
using System.Collections.Generic;

namespace CDPIUI.Helper.AddOns.BlockCheck2;

/// <summary>
/// Shows the BlockCheck2 issues in the language of the UI.
/// The text comes from the <c>BlockCheck2Issue_&lt;CODE&gt;</c> resource. Without a resource, the English message is shown.
/// </summary>
internal static class BlockCheck2IssueText
{
    private const string ResourcePrefix = "BlockCheck2Issue_";

    // The English messages of these codes contain values that the localized text also shows.
    private static readonly Dictionary<string, string> Patterns = new(StringComparer.Ordinal)
    {
        { "DNS_TARGETS_IGNORED", @"^Ignored (\d+) target\(s\) for (\d+) host\(s\) because the selected DNS resolver returned no address: (.*?)\. This is often" },
        { "BEST_EFFORT_CONFIG", @"for (\d+) target\(s\)" },
        { "NO_EFFECTIVE_STRATEGY", @"for the (\d+) target\(s\)" },
        { "TARGETS_UNCOVERED", @"covers (\d+) test target\(s\)" },
        { "MANUAL_SCOPE_PARTIAL", @"passed (\d+) of (\d+) site" },
        { "PRESET_REPAIRED", @"after (\d+) repair" },
        { "TARGET_INPUT_LIMIT_EXCEEDED", @"expands to (\d+) targets; the configured limit is (\d+)" },
        { "REPORT_CATALOG_VERSION_MISMATCH", @"catalog '([^']*)', while this CDPIUI build contains '([^']*)'" },
        { "SCAN_STRATEGIES_DO_NOT_START", @"^The last (\d+) strategies could not be started, so the scan was stopped: (.*)$" },
        { "CURL_NOT_AVAILABLE", CodedMessageLocalizer.AfterColon },
        { "SITE_LIST_NOT_FOUND", CodedMessageLocalizer.AfterColon },
        { "SITE_LIST_PATH_INVALID", CodedMessageLocalizer.AfterColon },
        { "SITE_LIST_READ_FAILED", CodedMessageLocalizer.AfterColon },
        { "SITE_LIST_INDIVIDUAL_DIRECTORY_FAILED", CodedMessageLocalizer.AfterColon },
        { "SITE_LIST_INDIVIDUAL_WRITE_FAILED", CodedMessageLocalizer.AfterColon },
        { "SCAN_BASELINE_FAILED", CodedMessageLocalizer.AfterColon },
        { "SCAN_COMPLETE_FAILED", CodedMessageLocalizer.AfterColon },
        { "SCAN_PREPARE_FAILED", CodedMessageLocalizer.AfterColon },
        { "SCAN_STOP_FAILED", CodedMessageLocalizer.AfterColon },
        { "SCAN_STRATEGY_CONFIG_FAILED", CodedMessageLocalizer.AfterColon },
        { "SCAN_STRATEGY_FAILED", CodedMessageLocalizer.AfterColon },
        { "SCAN_TARGET_PROBE_FAILED", CodedMessageLocalizer.AfterColon },
        { "MANUAL_TEST_PREPARE_FAILED", CodedMessageLocalizer.AfterColon },
        { "MANUAL_TEST_RESTORE_FAILED", CodedMessageLocalizer.AfterColon },
        { "MANUAL_TEST_STOP_FAILED", CodedMessageLocalizer.AfterColon },
        { "PRESET_VALIDATION_COMPLETE_FAILED", CodedMessageLocalizer.AfterColon },
        { "PRESET_VALIDATION_PREPARE_FAILED", CodedMessageLocalizer.AfterColon },
        { "PRESET_VALIDATION_PROBE_FAILED", CodedMessageLocalizer.AfterColon },
        { "PRESET_VALIDATION_START_FAILED", CodedMessageLocalizer.AfterColon },
        { "PRESET_VALIDATION_STOP_FAILED", CodedMessageLocalizer.AfterColon },
        { "MANUAL_TEST_FAILED", CodedMessageLocalizer.AfterColon },
        { "ACTION_ARGUMENT_NAME_INVALID", @"#(\d+)" },
        { "ACTION_ARGUMENT_VALUE_UNSAFE", @"#(\d+)" },
        { "ACTION_PAYLOAD_INVALID", @"#(\d+)" },
        { "ACTION_RANGE_INVALID", @"#(\d+)" },
        { "ACTION_ARGUMENT_RESERVED", CodedMessageLocalizer.SingleQuoted },
        { "BLOB_CONFLICT", CodedMessageLocalizer.SingleQuoted },
        { "LUA_FUNCTION_NOT_ALLOWED", CodedMessageLocalizer.SingleQuoted },
        { "UNSUPPORTED_CATALOG_SCHEMA", @"^Catalog schema (.+?) is not supported; expected (.+)\.$" },
    };

    internal static string GetMessage(BlockCheckIssue issue)
    {
        if (issue == null)
            return string.Empty;
        return GetMessage(issue.Code, issue.Message);
    }

    internal static string GetMessage(string code, string englishMessage)
    {
        if (string.IsNullOrWhiteSpace(code))
            return englishMessage ?? string.Empty;
        Patterns.TryGetValue(code, out string pattern);
        return CodedMessageLocalizer.Localize(ResourcePrefix + code, englishMessage, pattern);
    }

    /// <summary>Returns a copy of the issue with the localized message, for lists that bind to the message.</summary>
    internal static BlockCheckIssue Localize(BlockCheckIssue issue) =>
        issue == null ? null : issue with { Message = GetMessage(issue) };
}
