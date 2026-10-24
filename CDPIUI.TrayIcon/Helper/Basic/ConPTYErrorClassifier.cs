using CDPIUI.Shared.PrettyErrorConversionService;

namespace CDPIUI.TrayIcon.Helper.Basic;

/// <summary>
/// Finds the error a component reports in its console output.
/// </summary>
internal static class ConPTYErrorClassifier
{
    // Checked in this order and the first match of a line wins: with the dictionary every
    // matching key overwrote the previous one, so "Error opening filter" became UNKNOWN
    // through the generic "error" key.
    private static readonly (string Text, PrettyErrorCode Code)[] ErrorMappings =
    {
        ("Error opening filter", PrettyErrorCode.FILTER_OPEN_ERROR),
        ("unknown option", PrettyErrorCode.PARAMETER_ERROR),
        ("hostlists load failed", PrettyErrorCode.HOSTLIST_LOAD_ERROR),
        ("must specify port filter", PrettyErrorCode.PORT_FILTER_ERROR),
        ("must specify port or/and partial raw filter", PrettyErrorCode.PORT_FILTER_WRONG_VALUE_ERROR),
        ("ERROR:", PrettyErrorCode.UNKNOWN),
        ("Component not installed correctly", PrettyErrorCode.COMPONENT_INSTALL_ERROR),
        ("error", PrettyErrorCode.UNKNOWN),
        ("invalid value", PrettyErrorCode.INVALID_VALUE_ERROR),
        ("nvalid value", PrettyErrorCode.INVALID_VALUE_ERROR),
        ("option requires an argument", PrettyErrorCode.INVALID_VALUE_ERROR),
        ("--debug=0|1|syslog|@<filename>", PrettyErrorCode.PARAMETER_ERROR),
        ("already running", PrettyErrorCode.ALREADY_RUNNING_WARN),
        ("could not read", PrettyErrorCode.FILE_READ_ERROR),
        ("flag provided but not defined:", PrettyErrorCode.PARAMETER_ERROR),
        ("cannot create", PrettyErrorCode.ACCESS_DENIED),
        ("cannot access", PrettyErrorCode.ACCESS_DENIED)
    };

    public static PrettyErrorCode? FindError(string output)
    {
        PrettyErrorCode? generic = null;
        foreach (var (text, code) in ErrorMappings)
        {
            if (!output.Contains(text, StringComparison.OrdinalIgnoreCase))
                continue;
            if (code != PrettyErrorCode.UNKNOWN)
                return code;
            generic ??= code;
        }
        return generic;
    }

    /// <summary>
    /// The error to keep after <paramref name="output"/> was read, given the error found so far.
    /// </summary>
    public static PrettyErrorCode Merge(PrettyErrorCode lastError, string output)
    {
        // A specific error stays; a later generic match does not overwrite it.
        PrettyErrorCode? found = FindError(output);
        if (found != null && (lastError == PrettyErrorCode.SUCCESS || found != PrettyErrorCode.UNKNOWN))
            return found.Value;
        return lastError;
    }
}
