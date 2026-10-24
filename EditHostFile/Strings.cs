using System.Globalization;

namespace EditHostFile;

/// <summary>
/// Texts shown in the console window. Russian is used when the UI language of Windows is Russian.
/// </summary>
internal static class Strings
{
    private static bool IsRussian
    {
        get
        {
            try
            {
                return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru";
            }
            catch
            {
                // Without culture data the texts stay in English.
                return false;
            }
        }
    }

    private static string Pick(string english, string russian)
    {
        return IsRussian ? russian : english;
    }

    public static string WindowTitle
    {
        get { return Pick("CDPI UI: hosts file editor", "CDPI UI: редактор файла hosts"); }
    }

    public static string GettingReady
    {
        get { return Pick("Getting ready...", "Подготовка..."); }
    }

    public static string UnknownCommand
    {
        get { return Pick("Unknown command", "Неизвестная команда"); }
    }

    public static string WorkFailed
    {
        get { return Pick("Work failed: {0}", "Не удалось выполнить операцию: {0}"); }
    }

    public static string WorkFinished
    {
        get { return Pick("Work finished. Goodbye~", "Готово. До свидания!"); }
    }

    public static string CreatingBackup
    {
        get { return Pick("Creating backup for \"{0}\" in \"{1}\"", "Создание резервной копии «{0}» в «{1}»"); }
    }

    public static string RestoringFile
    {
        get { return Pick("Restoring \"{0}\" to its state without added lines", "Восстановление «{0}» без добавленных строк"); }
    }

    public static string RemovingLines
    {
        get { return Pick("Removing added lines in \"{0}\"", "Удаление добавленных строк из «{0}»"); }
    }

    public static string AddingLines
    {
        get { return Pick("Adding new lines in \"{0}\"", "Добавление новых строк в «{0}»"); }
    }

    public static string AddingLinesComplete
    {
        get { return Pick("Adding new lines in \"{0}\". Status: COMPLETE", "Добавление новых строк в «{0}». Состояние: ГОТОВО"); }
    }
}
