using EditHostFile.Localization;

namespace EditHostFile;

internal static class Strings
{
    internal static string Language { get; set; } = LocalePreference.Resolve(null);

    public static string WindowTitle => JsonLocalizer.Get("EditHostFile", nameof(WindowTitle), Language);
    public static string GettingReady => JsonLocalizer.Get("EditHostFile", nameof(GettingReady), Language);
    public static string UnknownCommand => JsonLocalizer.Get("EditHostFile", nameof(UnknownCommand), Language);
    public static string WorkFailed => JsonLocalizer.Get("EditHostFile", nameof(WorkFailed), Language);
    public static string WorkFinished => JsonLocalizer.Get("EditHostFile", nameof(WorkFinished), Language);
    public static string CreatingBackup => JsonLocalizer.Get("EditHostFile", nameof(CreatingBackup), Language);
    public static string RestoringFile => JsonLocalizer.Get("EditHostFile", nameof(RestoringFile), Language);
    public static string RemovingLines => JsonLocalizer.Get("EditHostFile", nameof(RemovingLines), Language);
    public static string AddingLines => JsonLocalizer.Get("EditHostFile", nameof(AddingLines), Language);
    public static string AddingLinesComplete => JsonLocalizer.Get("EditHostFile", nameof(AddingLinesComplete), Language);
}
