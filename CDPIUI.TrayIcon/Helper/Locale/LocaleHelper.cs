using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI.TrayIcon.Helper
{
    public class LocaleHelper
    {
        public static readonly Dictionary<string, string> ComponentNames = new()
        {
            { "CSZTBN012", "Zapret Legacy" },
            { "CSZTBN062", "Zapret2" },
            { "CSGIVS036", "GoodbyeDPI" },
            { "CSBIHA024", "ByeDPI" },
            { "CSSIXC048", "SpoofDPI" },
            { "CSNIG9025", "NoDPI" },
            { "CSTYFL050", "TG WS Proxy" },
        };

        public static string GetLocalizedComponentName(string id)
        {
            if (ComponentNames.TryGetValue(id, out var name)) return name;
            return id;
        }

        public static Dictionary<string, string> English = new Dictionary<string, string>()
        {
            { "SettingsRecoveryWarning", "Settings.xml could not be read or recovered. Settings were reset to defaults. Open the app to review them." },
            { "UpdateFailure", "Application update failed.\nClick or tap here to open log" },
            { "TrayHide", "Application is running and minimized to tray now.\nClick or tap here to open main window" },
            { "Autorun", "Autorun" },
            { "AutorunERR", "Cannot add this application to autorun." },
            { "ProcRun", "Process is running now.\nClick or tap here to open pseudoconsole and view process output" },
            { "ProcStop", "Process is stopped now.\nClick or tap here to open pseudoconsole and view process output" },
            { "ProcERR", "Cannot start process." },
            { "Exit", "Exit" },
            { "ShowMainWindow", "Maximize app" },
            { "UpdateAvailable", "New application version is available.\nClick or tap here to open download page" },
            { "Start", "Start" },
            { "Stop", "Stop" },
            { "Utils", "Utils" },
            { "Pseudoconsole", "View process output" },
            { "MsiInstallerHelper", "MSI installer helper" },
            { "MsiInstallerHelperErr", "Cannot install application package {0}. Error code is: {1}" },
            { "ProxySetupAsk", "Component {0} requires additional configuration before it can be used.\nClick or tap here to configure it." },
            { "CompatibilityCheckAssistant", "Compatibility check assistant" },
            { "ConfigRequiredNewestVersionOfComponent", "One or more installed items require a newer version of {0}.\nClick or tap here to check updates for {0}" },
            { "Store", "Store" },
            { "Setup", "Edit component's settings" },
            { "ToggleState", "Toggle component state (Running/Stopped)" },
            { "AllStopped", "All components are stopped." },
            { "StartedAll", "All components are running now." },
            { "StartedNow", "Now started: {0}." },
            { "StartedNowS", "Now started: {0}." },
            { "ConditionalTaskNotificationTitle", "Conditional task \"{0}\"" },
            { "ConditionalLaunchNotificationTitle", "Conditional Launch" },
            { "ConditionalLaunchActionCompletedMessage", "Task \"{0}\" was executed." },
            { "TrayErrorTitle", "Notify icon" },
            { "TrayErrorMessage", "We're unable to create icon in system notify area. Click or tap here to view details.\n{0}" },
            { "ExecutableTrustTitle", "CDPI UI: start with administrator rights?" },
            { "ExecutableTrustQuestion", "CDPI UI is about to start this program with administrator rights:\n\n{0}\n\nThe program or a library next to it is new or changed since the last start.\n\nSHA-256: {1}\nSigned by: {2}\n\nAllow the start only if you installed or updated this component yourself. Another program could have changed these files.\n\nStart the program?" },
            { "ExecutableTrustNoSignature", "no signature" }
        };

        public static Dictionary<string, string> Russian = new Dictionary<string, string>()
        {
            { "SettingsRecoveryWarning", "Не удалось прочитать или восстановить Settings.xml. Настройки сброшены. Откройте приложение, чтобы проверить их." },
            { "UpdateFailure", "Не удалось обновить приложение.\nНажмите или коснитесь здесь, чтобы открыть журнал" },
            { "TrayHide", "Приложение запущено и свёрнуто в область уведомлений.\nНажмите или коснитесь здесь, чтобы открыть главное окно" },
            { "Autorun", "Автозапуск" },
            { "AutorunERR", "Не удалось добавить приложение в автозапуск." },
            { "ProcRun", "Процесс запущен.\nНажмите или коснитесь здесь, чтобы просмотреть вывод процесса" },
            { "ProcStop", "Процесс остановлен.\nНажмите или коснитесь здесь, чтобы просмотреть вывод процесса" },
            { "ProcERR", "Не удалось запустить процесс." },
            { "Exit", "Выход" },
            { "ShowMainWindow", "Восстановить" },
            { "UpdateAvailable", "Доступна новая версия приложения.\nНажмите или коснитесь здесь, чтобы открыть страницу загрузки" },
            { "Start", "Запустить" },
            { "Stop", "Остановить" },
            { "Utils", "Утилиты" },
            { "Pseudoconsole", "Показать вывод процесса" },
            { "MsiInstallerHelper", "Помощник установки MSI" },
            { "MsiInstallerHelperErr", "Не удалось установить пакет приложения {0}. Код ошибки: {1}" },
            { "ProxySetupAsk", "Компонент {0} требует дополнительной настройки перед началом работы.\nНажмите или коснитесь здесь, чтобы настроить его." },
            { "CompatibilityCheckAssistant", "Помощник проверки совместимости" },
            { "ConfigRequiredNewestVersionOfComponent", "Один или несколько установленных элементов требуют более новой версии {0}.\nНажмите или коснитесь здесь, чтобы проверить наличие обновлений для {0}" },
            { "Store", "Магазин" },
            { "Setup", "Изменить настройки компонента" },
            { "ToggleState", "Переключить состояние компонента (запущен/остановлен)" },
            { "AllStopped", "Все компоненты остановлены." },
            { "StartedAll", "Все установленные компоненты запущены." },
            { "StartedNow", "Сейчас запущен: {0}." },
            { "StartedNowS", "Сейчас запущены: {0}." },
            { "ConditionalTaskNotificationTitle", "Условная задача «{0}»" },
            { "ConditionalLaunchNotificationTitle", "Условный запуск" },
            { "ConditionalLaunchActionCompletedMessage", "Задача «{0}» выполнена." },
            { "TrayErrorTitle", "Значок в области уведомлений" },
            { "TrayErrorMessage", "Не удалось создать значок в области уведомлений. Нажмите или коснитесь здесь, чтобы узнать подробности.\n{0}" },
            { "ExecutableTrustTitle", "CDPI UI: запустить с правами администратора?" },
            { "ExecutableTrustQuestion", "CDPI UI собирается запустить эту программу с правами администратора:\n\n{0}\n\nПрограмма или библиотека рядом с ней появилась или изменилась после прошлого запуска.\n\nSHA-256: {1}\nПодпись: {2}\n\nРазрешите запуск, только если вы сами установили или обновили этот компонент. Эти файлы могла изменить другая программа.\n\nЗапустить программу?" },
            { "ExecutableTrustNoSignature", "нет подписи" }
        };

        public static string GetLocaleString(string key)
        {
            try
            {
                var culture = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                return culture switch
                {
                    "en" => English.ContainsKey(key) ? English[key] : key,
                    "ru" => Russian.ContainsKey(key) ? Russian[key] : (English.ContainsKey(key) ? English[key] : key),
                    _ => English.ContainsKey(key) ? English[key] : key,
                };
            }
            catch (Exception)
            {
                return key;
            }
        }
    }
}
