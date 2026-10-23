using CDPIUI.AddOns.ConfigImport;
using CDPIUI.Helper.UserExperience;
using System;
using System.Collections.Generic;
using WinUI3Localizer;

namespace CDPIUI.Helper.AddOns.ConfigImport;

/// <summary>
/// Shows the issues and the error codes of the config import in the language of the UI.
/// The text comes from the <c>ConfigImportIssue_&lt;CODE&gt;</c> resource. Without a resource, the English message is shown.
/// </summary>
internal static class ConfigImportIssueText
{
    private const string ResourcePrefix = "ConfigImportIssue_";

    // The English messages of these codes contain a value that the localized text also shows.
    private static readonly Dictionary<string, string> Patterns = new(StringComparer.Ordinal)
    {
        { "MISSING_FILE_REPLACEMENT_NOT_FOUND", CodedMessageLocalizer.AfterColon },
        { "INVALID_RESOURCE_PATH", CodedMessageLocalizer.AfterColon },
        { "REFERENCED_FILE_OUTSIDE_SOURCE", CodedMessageLocalizer.AfterColon },
        { "REFERENCED_FILE_NOT_FOUND", CodedMessageLocalizer.AfterColon },
        { "UNSUPPORTED_FORMAT", CodedMessageLocalizer.SingleQuoted },
        { "UNRESOLVED_VARIABLE", CodedMessageLocalizer.SingleQuoted },
        { "UNRESOLVED_BATCH_VARIABLE", CodedMessageLocalizer.SingleQuoted },
        { "INCLUDED_CONFIG_NOT_FOUND", CodedMessageLocalizer.SingleQuoted },
        { "IMPORT_FAILED", CodedMessageLocalizer.Whole },
        { "BATCH_READ_FAILED", CodedMessageLocalizer.Whole },
        { "TEXT_CONFIG_READ_FAILED", CodedMessageLocalizer.Whole },
        { "CALLED_SCRIPT_READ_FAILED", CodedMessageLocalizer.Whole },
        { "IMPORT_INSTALL_FAILED", CodedMessageLocalizer.Whole },
    };

    internal static string GetMessage(ConfigImportIssue issue)
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

    /// <summary>
    /// Formats the error code of <see cref="ConfigImportInstallResult"/>: "CODE" or "CODE: details".
    /// </summary>
    internal static string GetInstallError(string errorCode)
    {
        ILocalizer localizer = Localizer.Get();
        if (string.IsNullOrWhiteSpace(errorCode))
            return localizer.GetLocalizedString("ConfigImportUnknownError");

        int separator = errorCode.IndexOf(": ", StringComparison.Ordinal);
        string code = separator > 0 ? errorCode[..separator] : errorCode;
        string details = separator > 0 ? errorCode[(separator + 2)..] : string.Empty;

        string message = code.StartsWith("SHARE_", StringComparison.Ordinal)
            ? localizer.GetLocalizedString("ConfigShareError_" + code)
            : GetMessage(code, details);
        if (string.IsNullOrWhiteSpace(message) || message == details)
            return errorCode;

        // The code and the details stay available for a bug report.
        bool detailsShown = details.Length > 0 && message.Contains(details, StringComparison.Ordinal);
        return $"{message}\n{(detailsShown ? code : errorCode)}";
    }
}
