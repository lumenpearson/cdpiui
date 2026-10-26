[English](README.md) | **Русский**

# CDPIUI.Core

`CDPIUI.Core` — библиотека классов .NET 8. Она содержит логику процесса главного окна (GUI),
которая не использует WinUI: компоненты, пресеты, Магазин, клиент pipe и настройки.
Библиотека не запускает процессы компонентов. Их запускает процесс трея с правами администратора
(`CDPIUI.TrayIcon`), когда библиотека отправляет запрос через pipe.

## Проекты, которые используют эту библиотеку

| Проект | Ссылка |
|---|---|
| `CDPIUI` (GUI) | `CDPIUI/CDPIUI.csproj` |
| `CDPIUI.AddOns` | `CDPIUI.AddOns/CDPIUI.AddOns.csproj` |
| `CDPIUI.Tests` | `CDPIUI.Tests/CDPIUI.Tests.csproj`; внутренние типы видны этому проекту (`InternalsVisibleTo`) |

`CDPIUI.TrayIcon` не использует `CDPIUI.Core`. Оба процесса используют `CDPIUI.Shared`.
Библиотека использует пакеты `Microsoft.Data.Sqlite`, `Newtonsoft.Json` и `Semver`.
Она также закрепляет `SQLitePCLRaw.lib.e_sqlite3` на версии 2.1.14. Версия, которую приносит `Microsoft.Data.Sqlite`, содержит известную уязвимость SQLite.

## Общие правила

- Большинство служб — ленивые синглтоны со статическим свойством `Instance` и блокировкой.
  Примеры: `SettingsManager`, `StoreHelper`, `DatabaseHelper` и `ComponentTasksManager`.
- Пространство имён `CDPIUI.Core.System` скрывает пространство имён `System` в коде внутри `CDPIUI.Core`.
  Используйте `global::System...`, когда короткое имя неоднозначно, например
  `global::System.IO.Directory.Exists` в `ComponentServices/Helpers/ComponentHelper.cs`.
- `ConfigurationService` находится в пространстве имён `CDPIUI.Core.ComponentServices.Configuration`.
  Его файл находится в папке `ComponentServices/Helpers/Configuration`.
- Операции, которые могут завершиться ошибкой, обычно возвращают `OperationResultModel<T>` (из `CDPIUI.Shared`).
  Не передавайте исключения в UI, когда есть модель результата.

## Корневые файлы

| Файл | Назначение |
|---|---|
| `Application.cs` | `ApplicationInfo`: язык UI (`Localization`, по умолчанию `en-US`) и `Version`. `Version` — версия файла входной сборки, читается один раз. |
| `CoreEvents.cs` | `CoreEvents`: событие `CriticalCoreExceptionHappens` (последняя ошибка хранится в `LastCriticalError`) и событие `RestartRequired`. `IsRestartPending` остаётся `true`, пока UI не вызовет `AcknowledgeRestart`. |
| `SettingsManager.cs` | `SettingsManager`: XML-настройки в `Settings/Settings.xml`. Отсутствующий ключ получает значение по умолчанию из `Template/Settings/Settings.xml` в папке приложения. |
| `State.cs` | `State`: тип установки, `IsApplicationBuildAsSingleFile` (портативная) и `IsApplicationBuildAsMsi`. См. «Тип установки». |

Настройки задаются группой и ключом, например `GetValue<string>("PROXY", "proxyType")`.
Настройки компонентов используют путь группы, например `["CONFIGS", componentId]`.

### Тип установки

`State` сообщает тип установки:

- Конфигурация `SingleFileRelease` определяет `SINGLEFILE`. Тогда установка всегда портативная.
- Конфигурация `MsiRelease` определяет `MSIFILE`. Тогда установка всегда MSI-установка.
- Без этих символов (например, `Release` или `Debug`) `State` определяет тип во время работы, один раз.
  MSI записывает свою папку установки в значение реестра `HKLM\Software\Storik4\CDPI UI\InstallLocation` (64-битное представление, компонент `InstallLocationRegistration` в `NewSetup/Product.wxs`).
  Если это значение равно папке приложения (без учёта регистра), установка — MSI-установка. Иначе она портативная.

Workflow релиза упаковывает одну и ту же сборку `Release` и в MSI, и в портативный ZIP. Поэтому проверка во время работы необходима.
Трей использует ту же проверку реестра (`CDPIUI.TrayIcon/Helper/Utils.cs`).
Тип установки определяет папку данных, файл обновления (`patch.cdpipatch` или `patch.msi`) и тип файла Магазина для обновления приложения.

## Папки данных

`Data/Directories.cs` предоставляет все пути. Используйте эти свойства. Не составляйте пути вручную.

| Свойство | Путь |
|---|---|
| `CurrentDirectory` | Папка исполняемого файла приложения. |
| `DataDirectory` | `CurrentDirectory`, если процесс может писать в неё и установка не MSI-установка (`State.IsApplicationBuildAsMsi`). Иначе `%LOCALAPPDATA%\CDPIUI`. |
| `SettingsFilePath` | `<DataDirectory>\Settings\Settings.xml` |
| `TemplateSettingsFilePath` | `<CurrentDirectory>\Template\Settings\Settings.xml` |
| `StoreItemsDirectory` | `<DataDirectory>\Store\Items`. У каждого установленного элемента есть папка `Items\<itemId>`. |
| `StoreLocalUserItemDirectory` | `Store\Items\LocalUserStorage` (пользовательские пресеты, с подпапками `List`, `Bin` и `Loc`). |
| `StoreLocalCacheDirectory` | `Store\Cache\Local`. Содержит базу данных элементов `storedata.db`. |
| `StoreRepoCacheDirectory` | `Store\Cache\Repo`. Содержит распакованный репозиторий Магазина. |
| `TempFilesDirectory` | `<DataDirectory>\TempFiles` |
| `DownloadManagerDirectory` | `TempFiles\Downloads` |
| `ELUADirectory` | `<CurrentDirectory>\ELUA` |

Если `DataDirectory` не удаётся определить, библиотека использует `%LOCALAPPDATA%\CDPIUI` и вызывает
`CriticalCoreExceptionHappens`.

## Basic

- `Logger`: синглтон лога (`LoggerBase` из `CDPIUI.Shared`) в `DataDirectory`.
  Отладочные сборки пишут все уровни. Остальные сборки используют настройку `DEBUG/logLevel` (по умолчанию `WARN`).
- `ErrorsHelper.Convertor` (`CustomErrorConvertor`): преобразует исключения в коды ошибок и `ErrorModel`.
  `HttpRequestException` с кодом состояния даёт `UNEXPECTED_STATUS_CODE`.
- `IniDocument`: читает и изменяет файл INI (`config.ini` GoodCheck). Сохраняет комментарии, пустые строки и порядок ключей и изменяет только строки тех значений, которые задаются.

## Communication

GUI — клиент pipe. Процесс трея — сервер pipe.

- `PipeClientService`: подключается к pipe, имя которого указано в файле сеанса `.session\pipe.key` в папке приложения. Трей создаёт новое имя pipe при каждом запуске.
  - Ждёт 2 секунды. Если процесс трея запущен, ждёт ещё 15 секунд. Во время ожидания снова читает файл сеанса каждые 0,5 секунды.
  - Если подключения нет, запускает `CDPIUI_TrayIcon.exe` с правами администратора (`runas`) и завершает процесс GUI.
    Трей получает `--autorun`, если `APPEARANCE/hideToTrayOnStartup` равно `true`. Иначе он получает `--show-ui`.
    Если у GUI есть аргументы активации или аргумент миграции GoodbyeDPI, трей получает `--show-ui` и эти аргументы.
  - Закрывает подключение и завершает GUI, если сервер — не ожидаемый `CDPIUI_TrayIcon.exe` (`PipePeerVerifier`).
  - Перед каждой попыткой подключения читает имя pipe и ключ pipe вместе и отправляет ключ в пакете авторизации (см. [Pipe](../CDPIUI.Shared/Pipe/README.ru.md#файл-сеанса)).
  - Изменение в группе настроек `CONFIGS/<componentId>` отправляет `ProcessIdStartupArgsChanged` и состояние настройки компонента.
- `PipeHelper`: статические методы, которые отправляют типизированные пакеты (например, настройки, обновление, уведомления, ConPTY, MSI, прокси, условный запуск).
- `CoreCommandsHandler`: обрабатывает пакеты от трея (service, ConPTY, настройки, MSI, приложение, условный запуск).
  Действия условного запуска отправляют `SendConditionalLaunchResult` с признаком успеха или текстом ошибки.
- `ConPtyHelpCaptureClient`: просит трей запустить исполняемый файл и возвращает его вывод справки (`CaptureHelpAsync`).

## ComponentServices

### Основные типы

- `ComponentTasksManager`: `TasksManageService<ProcessService>` с одной задачей для каждого установленного компонента.
  `UpdateTaskList` останавливает все задачи, заново строит список из базы данных и снова запускает задачи, которые работали раньше.
- `ProcessService`: состояние одного компонента: состояние работы, буферы вывода, последняя ошибка и события.
  Хранит вывод компонента размером до 2 Mi символов. Если вывод длиннее, хранит последнюю половину.
- `ComponentItemsLoaderHelper`: список объектов `ComponentHelper` для установленных компонентов, у которых есть исполняемый файл.
  `Init` заменяет весь список. Он не изменяет список на месте.
- `ComponentHelper`: путь к исполняемому файлу, папка и параметры запуска одного компонента.
  Исполняемый файл — `<Directory>\<Executable>.exe` из базы данных. Если его нет, помощник пробует `Store\Items\<id>`.

### Как запускается задача компонента

1. Трей отправляет `CONPTYMessageIds.GetStartupString` с `componentId`.
   Для автозапуска он отправляет `GetAllStartupStrings`. Условный запуск может отправить `StartComponent` или `ToggleComponent`.
2. `CoreCommandsHandler` вызывает `ComponentTasksManager.CreateAndRunNewTask(id)`.
   Если задача уже есть, менеджер сначала останавливает её (`StopProcessId`).
3. `ProcessService.StartProcess` читает выбранный пресет и составляет строку запуска (см. «Пресеты»).
4. Если исполняемого файла нет или строка запуска пуста, возникает исключение. Тогда задача показывает ошибку.
5. `SetupProxy` отправляет пакеты прокси. Для ByeDPI, NoDPI и SpoofDPI он также может заменить адрес и порт (см. «Proxy»).
6. Для NoDPI служба один раз добавляет `--quiet`.
7. Служба отправляет `CONPTYMessageIds.StartProcessId` с `componentId`, `exePath` и `args`.
8. Трей запускает процесс и отправляет `MarkProcessIdAsStarted`, `ProcessIdNewOutput` и `MarkProcessIdAsStopped`.

`RunAllPreferredActions` запускает каждый компонент, у которого `CONFIGS/<id>/usedForAutorun` равно `true`.

### Пресеты (`ConfigItem`)

Набор пресетов — это элемент Магазина типа `configlist`. Его папка содержит:

- `init.json` (`ConfigInitItem`): `toggleListAvailable` и `localized_strings_directory` (код языка → файл, например `"EN"`).
- Один файл `.json` для каждого пресета (`ConfigItem`). Все файлы `.json`, кроме `init.json`, — пресеты.

Важные поля `ConfigItem`:

| Поле | Содержимое |
|---|---|
| `target` | Первое значение — id компонента. Если загрузчик получает `$ANY` в качестве цели, он загружает все пресеты. |
| `name` | Отображаемое имя. Каждый `$WORD(key)` в имени заменяется на `key` из файла локализации набора. |
| `startup_string` | Командная строка с переменными `%NAME%` и функциями LScript. |
| `variables` | Список `"%NAME%=value"`. Значение может быть выражением LScript, например `$LOCALCONDITION(...)`. |
| `jparams` | Логические переключатели, которые читает `$LOCALCONDITION`. |
| `commaVars`, `availableCommaVarsValues` | Переменные со списком возможных значений. |
| `configMaker` | Данные редактора. Запуск компонента их не читает. Их читают `UsedFiles`, `ExpandVariables` и `ExpandFileReference`. |

Выбранный пресет хранится в настройках `CONFIGS/<componentId>/configId` (id набора) и `configFile` (имя файла).
Компонент Zapret2 также загружает пресеты Zapret (`CSZTBN012`). У этих пресетов `IsLegacy = true`.

`ConfigurationService.GetStartupParametersByConfigItem` составляет строку запуска в таком порядке:

1. Если `jparams`, `variables` или `startup_string` равно `null`, возвращает пустую строку.
2. `GetReadyToUseVariables` выполняет `LScriptCore.ExecuteScript` для значения каждой переменной (`callItemId` — id набора).
   Значение, которое начинается с `$GETCURRENTDIR` или `$GETSRDIR`, остаётся как есть. Шаг 5 раскрывает его и заключает путь в кавычки.
   Имена не учитывают регистр. Если имя встречается дважды, используется последнее значение.
3. `ReplaceVariables` заменяет `%NAME%` (буквы, цифры, `_`, `.`, `-`). Значения могут ссылаться на другие переменные.
   Выполняет не более 16 проходов и останавливается на 64 КиБ. Неизвестные имена остаются как есть.
4. `ReplaceCommaVariables` заменяет `commaVars` по тем же правилам.
5. `LScriptCore.ExecuteScriptUnsafe` раскрывает функции LScript во всей строке (`quotePaths: true`).
6. Для устаревшего пресета `Zapret2LegacyConfigService` преобразует результат для winws2.

Преобразование устаревших пресетов хранит кэш в `Store\Items\__ConvertedZPRT`. Настройка
`ZAPRET2/validateLegacyCacheHashes` (по умолчанию `true`) заставляет его сравнивать хеши файлов перед использованием кэша.

Другие типы для работы с конфигурацией:

- `ConfigItem.ExpandVariables`, `ExpandFileReference`, `ResolveFilePath` и `UsedFiles`: раскрывают переменные и псевдонимы `preset://` без LScript. Их используют дополнения (например, ConfigShare).
- `ConfigCommandLine`: разбивает командные строки на токены и расставляет кавычки. Не выполняет их.
- `ConfigFileReferences`: находит и переписывает файловые аргументы в командной строке.
- `ConfigPackMakeHelper.CreateConfigPack`: копирует пресеты, используемые файлы и файлы локализации в набор. Записывает ZIP-файл или регистрирует набор в базе данных (`autoImport`).
- `ConfigurationService.LoadConfigFromFile`: читает пресет `.json`, старый пресет с `custom_parameters` или файл `.bat`/`.cmd`.
- `Autocorrector.FindAutoCorrectPath`: ищет файл пресета в наборе, в компоненте и в других установленных элементах.

## Features

- `ApplicationAutorunManager`: отправляет трею `AddToAutorun` или `RemoveFromAutorun` и устанавливает `SYSTEM/autorun`.
- `ApplicationUpdate`: проверяет релиз приложения на GitHub или GitLab. После того как Магазин установит элемент приложения, отправляет трею `BeginApplicationUpdate` с `patch.cdpipatch` (портативная установка) или `patch.msi` (MSI).
- `FileSystemLinksManager`: создаёт символические или жёсткие ссылки для файлов элементов. Сначала создаёт ссылку с временным именем.

## JSON

`JSONConvertor` загружает, сериализует и десериализует JSON с помощью Newtonsoft.Json.

## LScript

LScript — небольшой язык выражений в метаданных Магазина, переменных пресетов и строках запуска.
См. [LScript/README.ru.md](LScript/README.ru.md).

## Proxy

- `ProxyHelper`: `ProxySetupTypes` (`None`, `AllSystem`, `ProxiFyre`, `NoActions`, `AsInConfig`) и `ProxyLikeComponents` (ByeDPI, NoDPI, SpoofDPI).
  `ReplaceArgsForProxy` удаляет параметры адреса и порта и добавляет значения `PROXY/IPAddress` и `PROXY/port`.
  `SetupProxy` использует его, только если `PROXY/proxyType` равно `AllSystem`, `ProxiFyre` или `NoActions`.
  `IsLoopbackAddress` и `IsValidPort` проверяют ввод пользователя.
- `WindowsNativeProxySettingsService`: читает и записывает значения прокси Windows Internet Settings в реестре.

`ProcessService.SetupProxy` использует `PROXY/proxyType`. Для `ProxiFyre` нужно дополнение `ASPEWK002`.

## Security

`CertificateCheck.CheckCatalog` проверяет подписанный каталог `catalog.cat` для папки.
Использует `CatalogVerifier` из `CDPIUI.Shared` и `Secret.TrustedCertificates`.

## Store

`StoreHelper` — фасад Магазина. GUI использует этот класс. Остальные службы Магазина внутренние.
Формат каталога, типы загрузки и проверки для каждого типа описаны в [Store/README.ru.md](Store/README.ru.md).

| Тип | Назначение |
|---|---|
| `StoreHelper` | Установка, обновление, удаление и проверка обновлений. События прогресса и ошибок. |
| `Queue/QueueManagerService` | Очередь FIFO. Устанавливает по одному элементу за раз. Также хранит список элементов с ошибкой. |
| `Repository/RepositoryLoaderService` | Скачивает репозиторий Магазина (GitHub или GitLab) и читает категории, элементы и готовые комплекты. |
| `Database/DatabaseHelper` | Таблица SQLite `Items` в `storedata.db`. Одна строка для каждого установленного элемента (`DatabaseStoreItem`). Все запросы, включая `RestorePaths`, читают таблицу под одной блокировкой. При запуске GUI вызывает `RestorePaths`. Если GUI не может восстановить элемент, он заново регистрирует пользовательский элемент или элемент приложения. Для других элементов `UnregisterMissingItem` удаляет строку, если папки элемента нет (`CDPIUI/Helper/Database/DatabaseInitializationService.cs`). |
| `Network/APIWorker`, `RequestWorker`, `DownloadWorker` | Запросы к API релизов, загрузки по HTTPS, распаковка архивов и запуск MSI. `RequestWorker` отправляет токен Магазина только на хосты GitHub и GitLab. |
| `MSI/MsiInstallerService`, `MsiInstallerQueueManager` | Отправляют MSI трею для установки и ждут результата (не более 30 минут). |
| `Application/ApplicationUpdateService` | Готовит патч приложения (`patch.cdpipatch`) и его обязательные патчи. Читает версию каждого обязательного патча из его собственного `requirements.json` и пропускает версии, которые не новее установленной версии или не старше основного патча. Располагает слои по порядку версий, начиная с самой старой. См. [.github/RELEASES.ru.md](../.github/RELEASES.ru.md#gui-side). |
| `LocalItemsInstallerHelper` | Читает локальные файлы наборов и добавляет их в очередь. |
| `StoreItemIds` | Проверяет id элементов (`^[A-Za-z0-9_-]{1,64}$`) и папки элементов. |
| `VersionHelper` (`VersionComparier.cs`) | Сравнивает строки версий. |
| `Data/HardcodedItemIds` | Фиксированные id компонентов и дополнений. |

### Кэш репозитория Магазина

`LoadAllStoreDatabase` скачивает архив репозитория, если кэша нет.
Он также скачивает архив, если синхронизация принудительная и с последней синхронизации (`STORE/lastSyncTime`) прошёл один день или больше.
Архив распаковывается в новую папку `Repo.new-<GUID>`. Библиотека заменяет `Store\Cache\Repo` только после полной распаковки.
Если GitHub недоступен, библиотека пробует зеркало GitLab.

### Как устанавливается или обновляется элемент

1. `AddItemToQueue` добавляет элемент с новым id операции. Элемент может быть в очереди только один раз.
2. `InstallItemWorker` проверяет id элемента.
3. Если элемент установлен, библиотека останавливает компонент и службу его драйвера. Она запоминает состояние работы.
4. Библиотека создаёт `Store\Items\<id>`. Старое содержимое удаляется, только если `cleanDirectoryBeforeInstalling` равно `true`.
5. Онлайн-элемент скачивается по HTTPS в `TempFiles\Downloads`. Затем библиотека распаковывает или копирует его в папку элемента.
6. Локальный набор перемещается на место (см. «Промежуточная папка и откат»).
7. `AddItemToDatabase` выполняет `before_install_actions`, записывает строку в базу данных и выполняет `after_install_actions`.
   Оба списка действий получают папку элемента как `CurrentDirectory`. Библиотека скачивает запросы `DOWNLOAD=` из обоих списков (см. [LScript/README.ru.md](LScript/README.ru.md)).
   Для локального набора библиотека сохраняет значок `$DYNAMICIMAGE(...)` как полный путь в папке элемента (`GetIconPath`).
8. Если компонент работал до обновления, библиотека снова запускает его.
9. `ItemActionsStopped` заставляет `ComponentTasksManager` заново загрузить список компонентов.

Если первая установка завершилась ошибкой, библиотека удаляет элемент. Если обновление завершилось ошибкой, строка базы данных установленной версии остаётся.

### Промежуточная папка и откат

| Содержимое | Поведение |
|---|---|
| Репозиторий Магазина | Распаковывается в `Repo.new-<GUID>`, затем перемещается на место. |
| Подписанный архив (`signedZip`, `.cdpisignedpack`) | Распаковывается в `<target>.verify-<GUID>`. Библиотека перемещает файлы на место, только если каталог действителен. |
| Локальный набор | Старая папка перемещается в `<folder>.previous-<GUID>`. Если перемещение нового набора не удалось, старая папка возвращается. |
| Онлайн-архив без подписи | Распаковывается прямо в папку элемента. Отката нет. |

ВНИМАНИЕ: Не рассчитывайте на откат для онлайн-обновлений. Неудачное онлайн-обновление может оставить новые файлы рядом со старыми.

Архив без подписи объединяет файлы `.txt`: библиотека добавляет новые строки в существующий файл и не заменяет его.
Набор без подписи (не `.cdpisignedpack`) не может содержать `.exe`, `.dll`, `.sys`, `.msi`, скрипты или ссылки.
Для такого набора библиотека игнорирует `ExecutableFile`, `BeforeInstallActions` и `AfterInstallActions`.

## System

- `ZipService` (внутренний): распаковка ZIP с фильтром корневой папки (`$ANY` соответствует одному сегменту), шаблонами пропуска и проверкой каталога.
- `ZipService` и загрузка репозитория Магазина используют `ArchivePaths` из `CDPIUI.Shared`: пути записей должны оставаться в целевой папке, архив должен укладываться в ограничения размера, а ссылки ниже целевой папки отклоняются.
- `BatchFileEncoding`: читает файлы `.bat` в UTF-8, UTF-16 или в кириллической кодовой странице.
- `ShellHelper`: открывает папки и файлы и запускает приложения.

## Тесты

Выполните `dotnet test CDPIUI.Tests/CDPIUI.Tests.csproj`. Тесты этой библиотеки находятся в `CDPIUI.Tests/Core`.
