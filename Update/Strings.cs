using System.Globalization;

namespace Update
{
    /// <summary>
    /// Texts of the update window. Russian is used when the UI language of Windows is Russian.
    /// Log lines stay in English.
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
            get { return Pick("Update Assistant", "Помощник обновления"); }
        }

        public static string Heading
        {
            get { return Pick("Patching application (CDPI UI)", "Обновление приложения (CDPI UI)"); }
        }

        public static string CancelButton
        {
            get { return Pick("Cancel", "Отмена"); }
        }

        public static string CloseButton
        {
            get { return Pick("Close", "Закрыть"); }
        }

        public static string Preparing
        {
            get { return Pick("Preparing...", "Подготовка..."); }
        }

        public static string Unpacking
        {
            get { return Pick("Unpacking...", "Распаковка..."); }
        }

        public static string CopyingFiles
        {
            get { return Pick("Copying files...", "Копирование файлов..."); }
        }

        public static string Finalizing
        {
            get { return Pick("Finalizing...", "Завершение..."); }
        }

        public static string Complete
        {
            get { return Pick("Complete", "Готово"); }
        }

        public static string ExceptionHappens
        {
            get { return Pick("Exception happens", "Произошла ошибка"); }
        }

        public static string ErrorTitle
        {
            get { return Pick("An error occurred", "Произошла ошибка"); }
        }

        public static string ErrorMessage
        {
            get
            {
                return Pick(
                    "An unexpected error occurred while updating the application. " +
                    "Additionally, an attempt to downgrade to the previous version failed.\n" +
                    "You may need to manually reinstall the application to resolve this issue.\n" +
                    "Please follow these steps:\n" +
                    "1. Uninstall the current version of the application.\n" +
                    "2. Download the latest version from GitHub.\n" +
                    "3. Install the downloaded version.\n" +
                    "If the problem persists, contact support.\n" +
                    "ERROR CODE: {0}",
                    "При обновлении приложения произошла непредвиденная ошибка. " +
                    "Вернуть предыдущую версию также не удалось.\n" +
                    "Возможно, приложение придётся переустановить вручную.\n" +
                    "Выполните следующие шаги:\n" +
                    "1. Удалите текущую версию приложения.\n" +
                    "2. Скачайте последнюю версию с GitHub.\n" +
                    "3. Установите скачанную версию.\n" +
                    "Если проблема не исчезнет, обратитесь в поддержку.\n" +
                    "КОД ОШИБКИ: {0}");
            }
        }

        public static string StartedWithoutArgumentsTitle
        {
            get { return Pick("CDPI UI Update", "Обновление CDPI UI"); }
        }

        public static string StartedWithoutArgumentsMessage
        {
            get
            {
                return Pick(
                    "Update.exe is started by CDPI UI to install an application update.",
                    "Update.exe запускается приложением CDPI UI для установки обновления.");
            }
        }
    }
}
