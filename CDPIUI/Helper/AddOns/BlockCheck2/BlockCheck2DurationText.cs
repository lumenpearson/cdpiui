using System;
using WinUI3Localizer;

namespace CDPIUI.Helper.AddOns.BlockCheck2;

/// <summary>
/// Localized texts of the BlockCheck2 remaining and maximum run times.
/// </summary>
internal sealed class BlockCheck2DurationText
{
    private readonly ILocalizer _localizer;

    public BlockCheck2DurationText(ILocalizer localizer)
    {
        _localizer = localizer;
    }

    /// <summary>
    /// Formats a remaining time rounded up to whole minutes. Without an estimate, the text of
    /// <paramref name="calculatingKey"/> is shown.
    /// </summary>
    public string FormatRemaining(TimeSpan? remaining, string calculatingKey)
    {
        if (!remaining.HasValue)
        {
            return Text(calculatingKey);
        }

        if (remaining.Value < TimeSpan.FromMinutes(1))
        {
            return Text("BlockCheck2RemainingTimeUnderMinute");
        }

        int totalMinutes = Math.Max(1, (int)Math.Ceiling(remaining.Value.TotalMinutes));
        if (totalMinutes < 60)
        {
            return string.Format(Text("BlockCheck2RemainingTimeMinutesFormat"), totalMinutes);
        }

        return string.Format(
            Text("BlockCheck2RemainingTimeHoursFormat"),
            totalMinutes / 60,
            totalMinutes % 60);
    }

    public string FormatDuration(TimeSpan duration)
    {
        int totalMinutes = Math.Max(1, (int)Math.Ceiling(duration.TotalMinutes));
        int days = totalMinutes / (24 * 60);
        int hours = totalMinutes / 60 % 24;
        int minutes = totalMinutes % 60;
        if (days > 0)
        {
            return string.Format(Text("BlockCheck2DurationDaysFormat"), days, hours, minutes);
        }
        if (hours > 0)
        {
            return string.Format(Text("BlockCheck2DurationHoursFormat"), hours, minutes);
        }
        return string.Format(Text("BlockCheck2DurationMinutesFormat"), minutes);
    }

    private string Text(string key) => _localizer.GetLocalizedString(key);
}
