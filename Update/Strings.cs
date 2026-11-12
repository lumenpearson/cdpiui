using Update.Localization;

namespace Update;

internal static class Strings
{
    internal static string Language { get; set; } = LocalePreference.Resolve(null);

    public static string WindowTitle => ReswLocalizer.Get("Update", nameof(WindowTitle), Language);
    public static string Heading => ReswLocalizer.Get("Update", nameof(Heading), Language);
    public static string CancelButton => ReswLocalizer.Get("Update", nameof(CancelButton), Language);
    public static string CloseButton => ReswLocalizer.Get("Update", nameof(CloseButton), Language);
    public static string Preparing => ReswLocalizer.Get("Update", nameof(Preparing), Language);
    public static string Unpacking => ReswLocalizer.Get("Update", nameof(Unpacking), Language);
    public static string CopyingFiles => ReswLocalizer.Get("Update", nameof(CopyingFiles), Language);
    public static string Finalizing => ReswLocalizer.Get("Update", nameof(Finalizing), Language);
    public static string Complete => ReswLocalizer.Get("Update", nameof(Complete), Language);
    public static string ExceptionHappens => ReswLocalizer.Get("Update", nameof(ExceptionHappens), Language);
    public static string ErrorTitle => ReswLocalizer.Get("Update", nameof(ErrorTitle), Language);
    public static string ErrorMessage => ReswLocalizer.Get("Update", nameof(ErrorMessage), Language);
    public static string StartedWithoutArgumentsTitle => ReswLocalizer.Get("Update", nameof(StartedWithoutArgumentsTitle), Language);
    public static string StartedWithoutArgumentsMessage => ReswLocalizer.Get("Update", nameof(StartedWithoutArgumentsMessage), Language);
}
