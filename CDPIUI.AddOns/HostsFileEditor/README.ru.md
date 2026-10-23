[English](README.md) | **Русский**

# Дополнение HostsFileEditor

## Назначение

Дополнение HostsFileEditor добавляет блок доменов CDPI UI в файл hosts Windows и удаляет его. GUI работает без прав администратора. Поэтому дополнение не изменяет файл hosts само. Оно запускает отдельный инструмент `EditHostFile.exe` с правами администратора и ждёт его код выхода.

`EditHostFile.exe` содержит всю логику изменения файла hosts: управляемый блок, резервную копию, кодировку и безопасную запись. См. [EditHostFile/README.ru.md](../../EditHostFile/README.ru.md).

## Основной тип

`HostsFileEditService` (`HostsFileEditorService.cs`, пространство имён `CDPIUI.AddOns.HostsFileEditor`) — класс только со статическими методами.

| Метод | Аргумент инструмента | Назначение |
|---|---|---|
| `AddDomains()` | `/add` | Добавляет блок CDPI UI и записывает `hosts.bak` |
| `RemoveDomains()` | `/remove` | Удаляет блок CDPI UI |
| `RestoreDomains()` | `/recover` | Удаляет блок CDPI UI и удаляет `hosts.bak` |

Каждый метод возвращает `Task<int>`: код выхода инструмента.

## Последовательность

1. Метод находит `EditHostFile.exe` в папке приложения (`Directories.CurrentDirectory`).
2. Если файл не существует, метод выбрасывает `ApplicationFilesDamagedException`.
3. Метод запускает инструмент с `UseShellExecute = true` и глаголом `runas`. Windows показывает запрос UAC.
4. Метод ждёт завершения процесса.
5. Метод возвращает код выхода. Если `Process.Start` возвращает `null`, метод возвращает `-1`.

Если пользователь отменяет запрос UAC, `Process.Start` выбрасывает исключение. Метод не перехватывает его. Вызывающий код должен его обработать.

Коды выхода инструмента:

| Код | Значение |
|---|---|
| `0` | Успех |
| `0x80070057` (`E_INVALIDARG`) | Нет известного аргумента |
| Другой | `HResult` исключения, которое остановило инструмент, или `1` |

## GUI

| Элемент | Расположение | Назначение |
|---|---|---|
| `EditHostFileWindow` | `CDPIUI/Windows/EditHostFileWindow.xaml(.cs)` | Окно инструмента |
| `Views.EditHostsFile.MainPage` | `CDPIUI/Views/EditHostsFile/MainPage.xaml(.cs)` | Кнопки добавления, удаления, восстановления и открытия |
| `HostsFileEditHelper` | `CDPIUI/Helper/AddOns/HostsFileEditHelper.cs` | Открывает файл hosts в текстовом редакторе |

Окно открывают следующие элементы:

- карточка файла hosts на странице инструментов (`CDPIUI/Views/UtilsPage.xaml.cs`);
- маршрут `cdpiui://Tools/Hosts` (`CDPIUI/Commands/CommandUriMapper.cs`).

Страница преобразует код выхода или исключение в текст ошибки с помощью `ErrorsHelper.Convertor.GetPrettyErrorCode("HOSTEDIT", ...)`. Результат `HOSTEDIT_SUCCESS (0x00000000)` означает успех. Страница не показывает ошибку для результата, который начинается с `HOSTEDIT_OPERATION_CANCELLED_BY_USER`. Для остальных результатов страница показывает диалог ошибки.

Карточка «Открыть» вызывает `HostsFileEditHelper.EditHostsFile`. Вспомогательный класс открывает `%WINDIR%\System32\drivers\etc\hosts` с запросом UAC. Приложение по умолчанию — Блокнот. Выбор приложения управляется группой настроек `FILEOPENACTIONS`. Если `isDialogShown` равно `false` или если `doNotRemindAgain` равно `false`, `EditSitelistAskApplicationContentDialog` сначала спрашивает, какое приложение использовать.

Кнопка справки открывает `cdpiui://Help/Utils/EditHostsFileUtility/`.

## Настройки

Страница хранит состояние в настройках CDPI UI, группа `HOSTS`:

| Ключ | Тип | Устанавливается в `true` | Устанавливается в `false` |
|---|---|---|---|
| `isFileEdited` | `bool` | После успешного `/add` | После успешного `/remove` или `/recover` |
| `isBackupAvailable` | `bool` | После успешного `/add` | После успешного `/recover` |

Страница использует эти значения, чтобы включать карточки:

- «Добавить» включена, когда `isFileEdited` равно `false`.
- «Удалить» включена, когда `isFileEdited` равно `true`.
- «Восстановить» включена, когда `isBackupAvailable` равно `true`.

## Ограничения

- Страница не читает файл hosts, чтобы узнать его состояние. Она использует только настройки `HOSTS`. Изменение файла hosts вне CDPI UI не изменяет эти настройки. Например, очистка при удалении MSI выполняет `EditHostFile.exe /remove`, но не изменяет настройки.
- Каждая операция показывает запрос UAC.
- Дополнение не проверяет подпись `EditHostFile.exe`. В установке MSI изменять папку установки могут только администраторы. Портативная копия находится вместе с `CDPIUI.exe` и треем в папке, которую пользователь может изменять. Поэтому проверка одного файла её не защищает. Запрос UAC показывает путь к файлу.
- У дополнения нет тестов в `CDPIUI.Tests`.
