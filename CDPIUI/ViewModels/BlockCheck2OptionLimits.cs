using System;

namespace CDPIUI.ViewModels;

/// <summary>
/// Default values and accepted ranges of the BlockCheck2 advanced options.
/// </summary>
internal static class BlockCheck2OptionLimits
{
    public const double DefaultConnectTimeoutSeconds = 5d;
    public const double DefaultRequestTimeoutSeconds = 10d;
    public const double DefaultStartupGraceSeconds = 0.75d;

    private const int MinimumRequestsPerDomain = 1;
    private const int MaximumRequestsPerDomain = 20;

    public static bool IsValidConnectTimeout(double value) =>
        double.IsFinite(value) && value is >= 1d and <= 60d;

    public static bool IsValidRequestTimeout(double value, double connectTimeout) =>
        double.IsFinite(value) && value is >= 2d and <= 180d && value >= connectTimeout;

    public static bool IsValidStartupGrace(double value) =>
        double.IsFinite(value) && value is >= 0.25d and <= 10d;

    public static bool IsValidRequestsPerDomain(double value) =>
        double.IsFinite(value) &&
        value is >= MinimumRequestsPerDomain and <= MaximumRequestsPerDomain &&
        Math.Abs(value - Math.Round(value)) < 0.000001d;
}
