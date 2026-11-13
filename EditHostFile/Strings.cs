using EditHostFile.Localization;

namespace EditHostFile;

internal static class Strings
{
    internal static string Language { get; set; } = LocalePreference.Resolve(null);

    public static string WindowTitle => ReswLocalizer.Get("EditHostFile", nameof(WindowTitle), Language);
    public static string GettingReady => ReswLocalizer.Get("EditHostFile", nameof(GettingReady), Language);
    public static string UnknownCommand => ReswLocalizer.Get("EditHostFile", nameof(UnknownCommand), Language);
    public static string WorkFailed => ReswLocalizer.Get("EditHostFile", nameof(WorkFailed), Language);
    public static string WorkFinished => ReswLocalizer.Get("EditHostFile", nameof(WorkFinished), Language);
    public static string CreatingBackup => ReswLocalizer.Get("EditHostFile", nameof(CreatingBackup), Language);
    public static string RestoringFile => ReswLocalizer.Get("EditHostFile", nameof(RestoringFile), Language);
    public static string RemovingLines => ReswLocalizer.Get("EditHostFile", nameof(RemovingLines), Language);
    public static string AddingLines => ReswLocalizer.Get("EditHostFile", nameof(AddingLines), Language);
    public static string AddingLinesComplete => ReswLocalizer.Get("EditHostFile", nameof(AddingLinesComplete), Language);
}
