namespace CDPIUI.Helper.CreateConfigHelper;

// A zapret2 --in-range/--out-range value: "<lower>-<upper>" or "<lower><<upper>" (exclusive upper bound).
internal readonly record struct Zapret2Range(
    Zapret2RangeBoundary? Lower,
    Zapret2RangeBoundary? Upper,
    bool UpperExclusive)
{
    public static bool TryParse(string value, out Zapret2Range range)
    {
        range = default;
        int separatorIndex = value.IndexOf('<');
        bool upperExclusive = separatorIndex >= 0;
        if (separatorIndex < 0)
        {
            separatorIndex = value.IndexOf('-');
        }
        if (separatorIndex < 0)
        {
            return false;
        }

        string lowerText = value[..separatorIndex];
        string upperText = value[(separatorIndex + 1)..];
        if (!TryParseBoundary(lowerText, out Zapret2RangeBoundary? lower) ||
            !TryParseBoundary(upperText, out Zapret2RangeBoundary? upper) ||
            lower is null && upper is null)
        {
            return false;
        }
        range = new Zapret2Range(lower, upper, upperExclusive);
        return true;
    }

    private static bool TryParseBoundary(
        string value,
        out Zapret2RangeBoundary? boundary)
    {
        boundary = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }
        char mode = char.ToLowerInvariant(value[0]);
        if (mode is not ('n' or 'd' or 'b' or 's' or 'p') ||
            !long.TryParse(value[1..], out long number) ||
            number < 0)
        {
            return false;
        }
        boundary = new Zapret2RangeBoundary(mode, number);
        return true;
    }
}
