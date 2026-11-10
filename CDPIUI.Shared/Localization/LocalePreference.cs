#nullable enable
using System;
using System.Globalization;

#if UPDATE_TOOL
namespace Update.Localization;
#elif HOSTS_TOOL
namespace EditHostFile.Localization;
#else
namespace CDPIUI.Shared.Localization;
#endif

/// <summary>Uses the selected application language, then Windows UI language, then English.</summary>
public static class LocalePreference
{
    public static string Resolve(string? selected, string? windowsLanguage = null) =>
        Normalize(selected) ?? Normalize(windowsLanguage ?? CultureInfo.CurrentUICulture.Name) ?? "en-us";

    public static string Read(Func<string?> readPreference, string? windowsLanguage = null)
    {
        try { return Resolve(readPreference(), windowsLanguage); }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
        { return Resolve(null, windowsLanguage); }
    }

    private static string? Normalize(string? value)
    {
        value = value?.Trim().Replace('_', '-');
        if (string.Equals(value, "ru", StringComparison.OrdinalIgnoreCase) ||
            value?.StartsWith("ru-", StringComparison.OrdinalIgnoreCase) == true)
            return "ru";
        if (string.Equals(value, "en", StringComparison.OrdinalIgnoreCase) ||
            value?.StartsWith("en-", StringComparison.OrdinalIgnoreCase) == true)
            return "en-us";
        return null;
    }

    /// <summary>Only accepts a supported language ID; command line values never become paths.</summary>
    public static string FromArguments(string[] arguments)
    {
        for (int index = 0; index + 1 < arguments.Length; index++)
            if (arguments[index] == "--language")
                return Resolve(arguments[index + 1]);
        return Resolve(null);
    }
}
