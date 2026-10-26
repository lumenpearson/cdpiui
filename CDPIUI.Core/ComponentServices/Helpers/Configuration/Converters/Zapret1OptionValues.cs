using System.Globalization;

namespace CDPIUI.Core.ComponentServices.Helpers.Configuration.Converters;

/// <summary>
/// Reads and normalizes the values of the Zapret1 options of one profile. The options are
/// keyed by name; an option given several times keeps all its values in order.
/// </summary>
internal static class Zapret1OptionValues
{
    public static string? OptionalValue(
        Dictionary<string, List<string?>> options,
        string name,
        string? defaultValue)
    {
        if (!options.TryGetValue(name, out List<string?>? values) || values.Count == 0)
        {
            return null;
        }

        return values[^1] ?? defaultValue;
    }

    public static string? LastValue(Dictionary<string, List<string?>> options, string name)
    {
        return options.TryGetValue(name, out List<string?>? values) && values.Count > 0
            ? values[^1]
            : null;
    }

    public static bool IsEnabled(Dictionary<string, List<string?>> options, string name)
    {
        if (!options.TryGetValue(name, out List<string?>? values) || values.Count == 0)
        {
            return false;
        }

        string? value = values[^1];
        return value == null ||
               value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsZero(string value)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return long.TryParse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long hex) &&
                   hex == 0;
        }

        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long number) &&
               number == 0;
    }

    public static string? NormalizeRangeCounter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 2)
        {
            return null;
        }

        char mode = char.ToLowerInvariant(value[0]);
        if (mode is not ('n' or 'd' or 's'))
        {
            return null;
        }

        return long.TryParse(value[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            ? char.ToLowerInvariant(value[0]) + value[1..]
            : null;
    }

    public static string? NormalizeTcpFlags(string? value)
    {
        return value?.Replace("PSH", "PUSH", StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<string> SplitCsv(string value)
    {
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
