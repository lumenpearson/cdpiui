[English](README.md) | **Русский**

# CDPIUI.Shared

`CDPIUI.Shared` — это библиотека `netstandard2.1` (C# 12, nullable включён).
Она содержит код, который используют вместе главное окно (GUI), ядро и трей.

На неё ссылаются следующие проекты:

- `CDPIUI` (GUI)
- `CDPIUI.Core`
- `CDPIUI.AddOns`
- `CDPIUI.TrayIcon`
- `CDPIUI.Tests`

Правила для всего кода этой библиотеки:

- Код должен компилироваться для `netstandard2.1`. Папка `System/` добавляет атрибуты, которые нужны членам `required` из C# 11.
- Этот код использует трей, который работает с правами администратора. Считайте каждый путь и файл, полученный от GUI, данными от менее доверенного процесса.
- Ссылки на пакеты: `Microsoft.CSharp` и `System.Security.Cryptography.Pkcs`.

Тесты для этой библиотеки находятся в `CDPIUI.Tests/Shared/`.
Протокол pipe описан в [Pipe/README.ru.md](Pipe/README.ru.md).

## Файлы в корне

| Файл | Содержимое |
|---|---|
| `SharedConstants.cs` | Имена репозиториев Магазина, id элементов Магазина, URL обновлений, `DatabaseFileName`, URI-схема `cdpiui` (`Schema`), `IsPreview` |
| `Utils.cs` (`SharedUtils`) | `GenerateNewId`, `IsOsSupportedNewGlyph`, `GetRandomHexNumber` |

`GetRandomHexNumber` использует `RandomNumberGenerator`. Используйте его для секретных значений, а не `System.Random`.

## Basic

| Тип | Назначение |
|---|---|
| `XMLSettingsService` | Читает и записывает `Settings.xml`. Его используют GUI и трей. |
| `CommandLine/GuiActivationArguments` | Кодирует аргументы GUI как `--gui-args:`, чтобы трей мог запустить GUI с теми же аргументами. |
| `CommandLine/CommandLineTokens` | Разбивает командную строку компонента на аргументы так же, как командная строка Windows. Его использует `ConfigCommandLine` в Core. |
| `Filesystem/ArchivePaths` | Проверяет записи архива: путь должен оставаться в целевой папке, архив должен укладываться в ограничения (20000 записей, 1 ГиБ, степень сжатия 200 для записей от 1 МиБ), и ни одна папка или файл ниже целевой папки не может быть ссылкой. `ExtractToFileBounded` останавливается, если запись даёт больше данных, чем указано в её заголовке. |
| `Filesystem/TrustedPathValidator` | Проверяет, что путь находится в разрешённой корневой папке и что ни одна часть пути ниже корня не является точкой повторного анализа (reparse point). |
| `Filesystem/ApplicationPathMatcher` | Находит процессы и драйверы приложения при удалении. |
| `Filesystem/DirectoriesManager` | Возвращает папку исполняемого файла, если в неё можно писать. Иначе, или если вызывающий код задаёт `forceAppData` (установка из MSI), возвращает `%LOCALAPPDATA%\CDPIUI`. |
| `Filesystem/FileSystemService` | Расширения файлов, временные имена, `WriteAllTextAtomic`, размеры файлов и папок. |

### XMLSettingsService

У файла есть корневой элемент `Settings`, элементы `Group` с атрибутом `Name` и элементы `Setting` с `Key`, `Type` и `Value`.

Загрузка:

- Обработка DTD запрещена, потому что трей с правами администратора читает файл, который может изменить пользователь.
- Если файла нет, создаётся новый пустой документ, и сервис его сохраняет.
- Если файл не читается, загрузка выполняется из `Settings.xml.bak`. Сервис сохраняет копию повреждённого файла как `Settings.xml.corrupt-<GUID>`.
- Если резервная копия тоже не читается, `HasUnrecoverableLoadError` равен `true`, и сервис записывает `Settings.xml.recovery-notice`.

Чтение:

- `GetValue` и `GetValueOrDefault` записывают значение по умолчанию в файл, если ключа нет.
- Значение другого типа возвращает значение по умолчанию. Исключение не возникает.
- Значения по умолчанию: `false`, `"NaN"`, `0`, `10.0`, пустой список. Подклассы могут переопределить `GetDefaultValueForKey`.

Запись:

- Каждый `SetValue` заново читает файл с диска, применяет одно изменение и сохраняет файл. Поэтому процесс со старой копией не перезаписывает изменение другого процесса.
- Файл `Settings.xml.lock` с `FileShare.None` блокирует запись.
- Сервис записывает временный файл и заменяет им `Settings.xml`. Предыдущий файл становится `Settings.xml.bak`.
- Поддерживаемые типы: `int`, `double`, `bool`, `string`, `DateTime`, `List<string>`. `double` и `DateTime` используют инвариантную культуру.
- Элементы `List<string>` разделяются символом `;`. Сервис записывает `;` как `%3B`, а `%` как `%25`.
- `SetValue(group, ...)` вызывает `PropertyChanged`. `SetValue(groupPath, ...)` вызывает `EnumPropertyChanged`.
- `Reload` не вызывает события.

## ComponentsTask

| Тип | Назначение |
|---|---|
| `IProcessService` | Интерфейс менеджера процесса компонента. Трей реализует его в `ProcessManager`. |
| `TaskModel<T>` | Задача: `Id`, `ProcessManager`, `IsSetupComplete`. |
| `TasksManageService<T>` | Список задач. Добавляет, запускает, останавливает и удаляет задачи и вызывает `TaskListUpdated`, `TaskStateUpdated`, `TaskSetupStateUpdated`. |
| `OutputCleanupHelper` | Удаляет escape-последовательности из вывода псевдоконсоли. |

Правила:

- `TasksManageService` использует `SemaphoreSlim` для списка. Используйте `GetTasksSnapshot`, чтобы читать список из нескольких потоков.
- `CreateAndRunNewTask` останавливает существующую задачу перед тем, как запустить её снова.
- `OutputCleanupHelper` удаляет только последовательности, которые начинаются с ESC. Используйте `TakeIncompleteTail`, чтобы сохранить последовательность, обрезанную в конце фрагмента.

## ConditionalLaunch

`ConditionalTaskModels.cs` содержит формат файла задачи для функции условного запуска.

- `ConditionalTask` — это XML (`.cdpitask`), версия 3. Версия 1 хранила один элемент `Trigger`. `Normalize` переносит его в `Triggers`.
- Типы триггеров: `HotKey`, `ProcessStarted`, `ProcessStopped`.
- Типы действий: см. `ConditionalActionType`.
- `ConditionalTaskFileService.GetTasksDirectoryFromSettingsFile` возвращает `<data directory>\ConditionalTasks`. Папка данных — это родительская папка для папки `Settings`.
- `LoadDirectory` пропускает недействительные файлы и файлы с повторяющимся `Id`. Файлы сортируются по имени.
- `Validate` требует GUID в `Id`, имя и приоритет от -1 до 1. Также требует один или несколько триггеров, одно или несколько действий и задержку от 0 до 86400 секунд.
- `Save` и `Export` записывают временный файл и затем заменяют им целевой файл.

## Exceptions

Собственные исключения, которые `ErrorConvertor` преобразует в `PrettyErrorCode`.

| Папка | Типы |
|---|---|
| корень | `ApplicationFilesDamagedException`, `AsyncOperationException`, `MsiInstallException`, `NewestVersionAlreadyInstalledException`, `UnknownException`, `UnknownFileFormatException` |
| `Catalog/` | `CatalogInvalid`, `CatalogNoSignature`, `CertificateNotTrusted` |
| `Database/` | `AddonNotInstalledException`, `ApplicationItemRegistrationException`, `DatabaseUserItemRegistrationException` |
| `Settings/` | `SettingNotFoundException`, `SettingTypeNotSupported` |
| `Interface/` | `ICustomException` |

Исключение, которое реализует `ICustomException`, возвращает свой собственный `PrettyErrorCode`.

## Extensions

| Тип | Назначение |
|---|---|
| `EnumExtension` | `ToEnum<T>(string)` (без учёта регистра, выбрасывает исключение для неизвестного значения) и `ToEnum<T>(string?, T defaultValue)` |
| `DictionaryExtension` | `GetKeyByValue`, `AddRange` (добавляет только новые элементы) |
| `StringExtension` | `SerializeTuples`, `DeserializeTuples`, `FirstCharToUpper` |
| `LogicalComparer` | Сортирует элементы `INamedModel` с помощью `StrCmpLogicalW` |

## Logger

`LoggerBase` записывает файлы журнала. У каждого процесса есть свой подкласс с `Instance`:

- Трей: `CDPIUI.TrayIcon/Helper/Basic/Logger.cs`. Корневая папка — папка трея.
- GUI и ядро: `CDPIUI.Core/Basic/Logger.cs`. Корневая папка — `Directories.DataDirectory`.

Файлы и ротация:

- Файл — `<root>\Logs\<sender>.log`. Сервис заменяет символы, недопустимые в имени файла, на `_`.
- Когда файл больше 5 МиБ, сервис копирует его в `<sender>.old.log` и очищает.
- Файл открывается с `FileShare.ReadWrite | FileShare.Delete`, потому что GUI и трей могут писать в один и тот же файл.
- Первая строка нового файла — `LoggerBase is ready-to-work <level>`.

Уровни (`LogSelevirity`): `DEBG`, `INFO`, `WARN`, `CRIT`. `CreateErrorLog` записывает `CRIT`.
Журнал записывает только сообщения минимального уровня или выше. Каждое сообщение также передаётся в `Debug.WriteLine`.

ПРЕДУПРЕЖДЕНИЕ: Не записывайте секреты (токены, ключ pipe) в журнал.

## Migration

`GoodbyeDpiMigrationActivation` читает и создаёт аргумент `--gdpiui-migration=`. GDPIUI-Updater использует его, чтобы запустить миграцию из GoodbyeDPI UI.

- Значение состоит из шести частей, разделённых `.`:
  1. Версия (`1`).
  2. Путь к архиву (Base64 URL).
  3. Хеш SHA-256 (шестнадцатеричный, в нижнем регистре).
  4. Id миграции (GUID, формат `N`).
  5. Имя pipe для ответа (Base64 URL).
  6. Токен сеанса (от 32 до 64 байт, Base64 URL).
- Архив должен быть файлом `.zip`. Имя pipe должно начинаться с `GDPIUI-Updater-`.
- Трей передаёт `RawArgument` в GUI без изменений.

## Models

| Тип | Назначение |
|---|---|
| `OperationResultModel<T>` | Результат с `ErrorModel` |
| `UnprocessedOperationResultModel<T>` | Результат с `Exception` |
| `EmptyResult` | Результат без значения |
| `IOperationResultModel`, `INamedModel` | Интерфейсы |

У каждого результата есть `Success`, `Result`, `ErrorHappens` и `Error`.
Используйте `SuccessResult`, `UnSuccessResult` (неудача, без ошибки) и `FailureResult` (неудача с ошибкой).

## Pipe

См. [Pipe/README.ru.md](Pipe/README.ru.md).

## PrettyErrorConversionService

`ErrorConvertor` преобразует исключение в `PrettyErrorCode` и в текст для пользователя.

- `MapExceptionToCode` проверяет исключение и каждое внутреннее исключение. Он использует тип исключения, ошибки сокетов, коды состояния HTTP и значения HRESULT.
- Коды Win32 и соответствующий HRESULT `0x8007xxxx` дают один и тот же код.
- `GetPrettyErrorCode(prefix, ex, logger)` возвращает `<PREFIX>_<CODE>[_<HTTP status>] (0x<HRESULT>)`. Часть HRESULT есть, только когда он известен. Пример: `SAMPLE_PREFIX_IO_FILE_NOT_FOUND`.
- `GetErrorModel` возвращает `ErrorModel` с `ErrorCode`, `Object`, `StatusCode`, `Exception` и `HResult`.
- Каждый метод записывает ошибку в переданный ему журнал.

Префикс должен быть в верхнем регистре с `_` между словами, например `SAMPLE_PREFIX`.
Чтобы добавить код, добавьте его в `PrettyErrorCode.cs` и в сопоставление в `ErrorConvertor.cs`.
Трей использует подкласс `CustomErrorConvertor`, который всегда пишет в журнал трея.

<a id="secrets"></a>
## Секреты

`Secrets/Secret.cs` содержит секреты сборки. Git игнорирует этот файл.
Его нужно создать до сборки решения.

1. Для локальной сборки выполните `pwsh .github/scripts/create_secret_stub.ps1`. Скрипт создаёт `Secret.cs` с пустыми токенами и пустым списком `TrustedCertificates`. Существующий файл скрипт не изменяет.
2. Либо создайте `public static class Secret` в пространстве имён `CDPIUI.Shared.Secrets`. Скопируйте только члены `SecretExample` из `SecretExample.cs`.

Релизный workflow записывает `Secret.cs` с помощью `.github/scripts/create_release_secret.ps1` из секретов репозитория. См. [.github/RELEASES.ru.md](../.github/RELEASES.ru.md).

`SecretExample.cs` исключён из сборки в `CDPIUI.Shared.csproj`. Интерфейс `ISecret` в нём описывает члены:

| Член | Результат пустого значения |
|---|---|
| `GitHubToken`, `GitLabToken` | Магазин недоступен. |
| `ProxyURL`, `ProxyKey` | Нет обратного прокси Магазина. |
| `TrustedCertificates` | Подписанные пакеты, подписанные патчи и обновления приложения нельзя проверить. |

`TrustedCertificates` содержит SHA-1 отпечатки сертификатов, которые могут подписывать каталоги и `Setup.msi`.

Приложение публично, поэтому каждое значение в `Secret.cs` тоже публично. Используйте только такие токены Магазина, которые ничего не могут изменить. `create_release_secret.ps1` отклоняет токен GitHub с областями (scopes) и токен GitLab с областями, кроме `read_api`, `read_repository` и `read_registry`. GUI отправляет токен Магазина только в прямых запросах на хосты GitHub и GitLab (`RequestWorker`). Он не отправляет токен на прокси Магазина. Прокси использует собственный токен. Старый `Secret.cs` может содержать `AuthGuid`. Приложение не использует этот член.

ВНИМАНИЕ: Не добавляйте `Secret.cs` в коммит. Не используйте значения заглушки в релизной сборке.

## Security

`CatalogVerifier` проверяет подписанный `catalog.cat` и файлы его папки.

1. Подпись декодируется как CMS. Сертификат каждого подписывающего лица должен быть в списке доверенных отпечатков. Сертификаты самоподписаны, поэтому цепочка не строится.
2. Каждая подпись должна быть действительной (`CheckSignature(verifySignatureOnly: true)`).
3. `powershell.exe` из системной папки выполняет `Test-FileCatalog` с `-FilesToSkip catalog.cat` и именами файлов, которые передаёт вызывающий код (`filesToSkip`). Пути и имена передаются через переменные окружения.
4. Результат должен быть `Valid` в течение 2 минут.

Результат — значение `CatalogVerification`: `Success`, `NoSignature`, `NotTrustedSignature`, `FilesDoNotMatch` или `Failure`.

Сертификаты закреплены по отпечатку, а цепочка, отзыв и срок действия не проверяются. Так сделано намеренно: сертификаты самоподписанные, а проверка срока действия отклонила бы все пакеты, подписанные до его окончания. Чтобы заменить сертификат, добавьте новый отпечаток в `TrustedCertificates`, выпустите эту сборку и только потом подписывайте новым сертификатом. Чтобы отозвать сертификат, удалите его отпечаток и выпустите сборку.

`FileWritingOptions` находит параметры компонента, которые записывают файл (`--debug=@<file>`, `--wf-save`, `--hostlist-auto`, `--hostlist-auto-debug`), и проверяет, что файл находится в разрешённых папках. Он также может читать файлы аргументов (`@<file>`). Трей использует его перед каждым запуском компонента, а `ConfigShare` — для общих пресетов.

`ElevatedLauncherCommand` даёт имя файла `CDPIUI_ElevatedLauncher.exe`, имена его целей (`tray`, `hosts`) и его командную строку. GUI и задача автозапуска трея запускают трей и `EditHostFile.exe` с правами администратора только через программу запуска. См. [CDPIUI.ElevatedLauncher/README.ru.md](../CDPIUI.ElevatedLauncher/README.ru.md).

## System

Полифилы для `netstandard2.1`:

- `System/Runtime/CompilerServices/RequiredMemberAttribute.cs`
- `System/Runtime/CompilerServices/CompilerFeatureRequiredAttribute.cs`
- `System/Diagnostic/CodeAnalysis/SetsRequiredMembersAttribute.cs`

Не удаляйте их. Они нужны членам `required` в этой библиотеке.
