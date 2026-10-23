using System;
using System.Linq;
using System.Text.RegularExpressions;
using WinUI3Localizer;

namespace CDPIUI.Helper.UserExperience;

/// <summary>
/// Localizes a message that an add-on creates in English, by its code.
/// The resource text can contain {0}, {1}... for the groups of a pattern that is applied to the English message.
/// If the resource is missing or the values cannot be read, the English message is returned.
/// </summary>
internal static class CodedMessageLocalizer
{
    /// <summary>The text after the first ": ", for example a path or an exception message.</summary>
    internal const string AfterColon = @"^.*?: (.+)$";

    /// <summary>The first text in single quotes, for example a file extension or a variable name.</summary>
    internal const string SingleQuoted = @"'([^']*)'";

    /// <summary>The complete English message, for example an exception message.</summary>
    internal const string Whole = @"^(.+)$";

    internal static string Localize(string resourceKey, string englishMessage, string pattern = null)
    {
        englishMessage ??= string.Empty;

        string template;
        try
        {
            template = Localizer.Get().GetLocalizedString(resourceKey);
        }
        catch (Exception)
        {
            return englishMessage;
        }

        if (string.IsNullOrWhiteSpace(template) || template == resourceKey)
            return englishMessage;

        if (pattern == null)
            return template.Contains("{0}", StringComparison.Ordinal) ? englishMessage : template;

        Match match = Regex.Match(englishMessage, pattern, RegexOptions.Singleline | RegexOptions.CultureInvariant);
        if (!match.Success)
            return englishMessage;

        object[] values = match.Groups
            .Cast<Group>()
            .Skip(1)
            .Select(group => (object)group.Value)
            .ToArray();
        try
        {
            return string.Format(template, values);
        }
        catch (FormatException)
        {
            return englishMessage;
        }
    }
}
