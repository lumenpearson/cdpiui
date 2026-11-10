using Update.Localization;

namespace Update;

internal static class Strings
{
    internal static string Language { get; set; } = LocalePreference.Resolve(null);

    public static string WindowTitle => JsonLocalizer.Get("Update", nameof(WindowTitle), Language);
    public static string Heading => JsonLocalizer.Get("Update", nameof(Heading), Language);
    public static string CancelButton => JsonLocalizer.Get("Update", nameof(CancelButton), Language);
    public static string CloseButton => JsonLocalizer.Get("Update", nameof(CloseButton), Language);
    public static string Preparing => JsonLocalizer.Get("Update", nameof(Preparing), Language);
    public static string Unpacking => JsonLocalizer.Get("Update", nameof(Unpacking), Language);
    public static string CopyingFiles => JsonLocalizer.Get("Update", nameof(CopyingFiles), Language);
    public static string Finalizing => JsonLocalizer.Get("Update", nameof(Finalizing), Language);
    public static string Complete => JsonLocalizer.Get("Update", nameof(Complete), Language);
    public static string ExceptionHappens => JsonLocalizer.Get("Update", nameof(ExceptionHappens), Language);
    public static string ErrorTitle => JsonLocalizer.Get("Update", nameof(ErrorTitle), Language);
    public static string ErrorMessage => JsonLocalizer.Get("Update", nameof(ErrorMessage), Language);
    public static string StartedWithoutArgumentsTitle => JsonLocalizer.Get("Update", nameof(StartedWithoutArgumentsTitle), Language);
    public static string StartedWithoutArgumentsMessage => JsonLocalizer.Get("Update", nameof(StartedWithoutArgumentsMessage), Language);
}
