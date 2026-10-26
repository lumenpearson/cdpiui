using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace CDPIUI.Shared.Basic;

/// <summary>
/// Converts setting values to the type name and the text stored in the settings file, and back.
/// </summary>
internal static class SettingsValueCodec
{
    /// <summary>
    /// Type name and text of a value set by group name.
    /// </summary>
    // Unlike EncodeForGroupPath, a List<string> is recognized by T: a null list is stored as an
    // empty list, and a list passed as another T is stored by its ToString().
    public static (string Type, string? Value) EncodeForGroup<T>(T value)
    {
        if (value is int)
            return ("int", value.ToString());
        if (value is double d)
            return ("double", FormatDouble(d));
        if (value is bool)
            return ("bool", value.ToString().ToLowerInvariant());
        if (value is string)
            return ("string", value as string);
        if (value is DateTime dateTime)
            return (nameof(DateTime), FormatDateTime(dateTime));
        if (typeof(T) == typeof(List<string>))
        {
            string valueString = JoinList((List<string>?)(object?)value ?? []);

            Debug.WriteLine(valueString);
            return (nameof(List<string>), valueString);
        }
        return (typeof(T).Name, value?.ToString() ?? string.Empty);
    }

    /// <summary>
    /// Type name and text of a value set by group path.
    /// </summary>
    public static (string Type, string Value) EncodeForGroupPath<T>(T value)
    {
        switch (value)
        {
            case int i:
                return ("int", i.ToString());
            case double d:
                return ("double", FormatDouble(d));
            case bool b:
                return ("bool", b.ToString().ToLowerInvariant());
            case string s:
                return ("string", s);
            case List<string> lst:
                return (nameof(List<string>), JoinList(lst));
            case DateTime dateTime:
                return (nameof(DateTime), FormatDateTime(dateTime));
            default:
                return (typeof(T).Name, value?.ToString() ?? string.Empty);
        }
    }

    public static T? Decode<T>(string? value, string? type)
    {
        if (typeof(T) == typeof(int) && type == "int" && int.TryParse(value, out var iv))
            return (T)(object)iv;
        if (typeof(T) == typeof(double) && type == "double" && TryParseDouble(value, out var dv))
            return (T)(object)dv;
        if (typeof(T) == typeof(bool) && type == "bool" && bool.TryParse(value, out var bv))
            return (T)(object)bv;
        if (typeof(T) == typeof(string) && type == "string")
            return (T)(object)value;

        if (typeof(T) == typeof(List<string>) && type == nameof(List<string>))
            return string.IsNullOrEmpty(value) ? (T)(object)new List<string>() : (T)(object)SplitList(value);

        if (typeof(T) == typeof(DateTime) && type == nameof(DateTime) && TryParseDateTime(value, out var dtv))
            return (T)(object)dtv;

        throw new InvalidCastException($"A setting of type '{type}' cannot be read as {typeof(T).Name}.");
    }

    // Items are separated by ';'. A ';' or '%' inside an item is stored as %3B or %25, so file
    // names with ';' survive; values written before contain neither sequence and read the same.
    private static string JoinList(IEnumerable<string> items) =>
        string.Join(";", items.Select(item => (item ?? string.Empty).Replace("%", "%25").Replace(";", "%3B")));

    private static List<string> SplitList(string value) =>
        value.Split(';')
            .Select(item => Regex.Replace(item, "%(3B|25)", match =>
                string.Equals(match.Groups[1].Value, "3B", StringComparison.OrdinalIgnoreCase) ? ";" : "%",
                RegexOptions.IgnoreCase))
            .ToList();

    public static string FormatDouble(double value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    public static string FormatDateTime(DateTime value) =>
        value.ToString("o", CultureInfo.InvariantCulture);

    // Values written by older versions used the current culture, so it is tried after the invariant form.
    public static bool TryParseDouble(string? value, out double result) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result)
        || double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out result);

    public static bool TryParseDateTime(string? value, out DateTime result) =>
        DateTime.TryParseExact(value, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out result)
        || DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out result)
        || DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
}
