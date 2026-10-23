[English](README.md) | **Русский**

# Протокол pipe

Главное окно (GUI) (`CDPIUI.exe`) и трей (`CDPIUI_TrayIcon.exe`) обмениваются сообщениями через один именованный канал (pipe).
Трей — сервер pipe. GUI — клиент pipe.

| Часть | Файл |
|---|---|
| Общий базовый класс, кадрирование, модели сообщений | `CDPIUI.Shared/Pipe/` |
| Сервер (трей) | `CDPIUI.TrayIcon/Helper/Basic/ConnectionHelper.cs` (`PipeServer`), `PipeAccessPolicy.cs` |
| Обработчики и отправители трея | `CDPIUI.TrayIcon/Helper/Basic/CommandsHandler.cs`, `CDPIUI.TrayIcon/Helper/PipeHelper.cs` |
| Клиент (GUI) | `CDPIUI.Core/Communication/PipeClientService.cs` |
| Обработчики и отправители GUI | `CDPIUI.Core/Communication/CoreCommandsHandler.cs`, `CDPIUI.Core/Communication/PipeHelper.cs`, `CDPIUI/Commands/CommandsHandler.cs` |

## Pipe и подключение

- Имя pipe — `SharedConstants.PipeName`: `{203ABE1D-01A2-47C0-B33D-8C8F7934CF4F}_CDPIUI`.
- Сервер принимает одного клиента за раз (`_maxServerInstances = 1`).
- Клиент подключается с `TokenImpersonationLevel.Identification`.
- После отключения сервер создаёт новый экземпляр pipe и ждёт следующего клиента.

GUI подключается в `PipeClientService.Start`:

1. GUI пытается подключиться в течение 2 секунд.
2. Если процесс `CDPIUI_TrayIcon` запущен, GUI пытается снова в течение 15 секунд.
3. Если подключения нет, GUI запускает `CDPIUI_TrayIcon.exe` с `runas` и завершает свой процесс.
4. Затем трей снова запускает GUI (см. [CDPIUI.TrayIcon/README.ru.md](../../CDPIUI.TrayIcon/README.ru.md)).

## Безопасность

### Доступ к pipe

`PipeAccessPolicy.CreatePipeSecurity` задаёт такой список доступа:

| Учётная запись | Доступ |
|---|---|
| Пользователь трея (владелец) | `FullControl` |
| `LocalSystem` | `FullControl` |
| Пользователь рабочего стола, если это не пользователь трея | `ReadWrite` |
| `Network` | `FullControl`, запрет |

Пользователь рабочего стола — владелец процесса окна оболочки (`GetShellWindow`).
Это правило нужно, когда трей работает с правами администратора под другой учётной записью администратора.

### Проверка исполняемого файла другой стороны

Каждая сторона проверяет исполняемый файл другого процесса (`PipePeerVerifier`):

- Сервер вызывает `IsClientExpected`. Ожидаемый клиент — `CDPIUI.exe` в папке трея.
- Клиент вызывает `IsServerExpected`. Ожидаемый сервер — `CDPIUI_TrayIcon.exe` в папке GUI.
- Сборки Release сравнивают полные пути к образам.
- Сборки `DEBUG` сравнивают только имена файлов, потому что у GUI и трея разные выходные папки.

Сервер отключает клиента, который не прошёл проверку.
Клиент завершает свой процесс, если сервер не прошёл проверку.

### Авторизация

Трей создаёт новый случайный ключ (32 байта в шестнадцатеричном виде) при каждом запуске (`PipeSessionKeyWriter`). Он записывает ключ в `<application folder>\.session\pipe.key` до запуска сервера pipe (`PipeSessionKey`). У файла защищённый список доступа: SYSTEM, Administrators и пользователь трея имеют полный доступ, пользователь рабочего стола может его читать. Другие пользователи не могут прочитать ключ. В установке MSI создавать файлы в папке установки могут только администраторы, поэтому никто не может подложить туда ключ до трея.

1. После подключения сервер отправляет `Service/ConnectOK`.
2. Клиент читает `pipe.key` и отправляет `Service/RequestAuth` с ключом `GUID`. Значение — этот ключ. Клиент читает файл заново при каждом подключении, потому что у перезапущенного трея новый ключ.
3. Если ключ трея не пуст и значение равно ему, сервер отправляет `Service/AuthOK`. Сравнение занимает одинаковое время для любого неверного значения.
4. Во всех остальных случаях сервер отправляет `Service/AuthFAIL` и отключает клиента.

Правила:

- Если трей не может записать файл ключа, ключ пуст. Пустой ключ не авторизует ни одного клиента.
- Сервер отключает клиента, если клиент не авторизован через 15 секунд после подключения.
- Неудачный `RequestAuth` всегда отключает клиента, даже после более раннего `AuthOK`. Клиент не может повторить попытку в том же подключении.
- До авторизации сервер отправляет только сообщения `Service` (`PipeServer.CanSend`).
- До авторизации сервер обрабатывает только `RequestAuth`. Все остальные сообщения он игнорирует и пишет предупреждение в лог.
- В записях лога о полученных, поставленных в очередь и неудачных сообщениях значение `GUID` заменяется на `***` (`PipeServiceBase.RedactSecrets`).

## Кадрирование

`StreamString` записывает каждое сообщение как один кадр:

- Текст — UTF-16 LE (`Encoding.Unicode`).
- Перед текстом сообщения короче 0xFFFF байт стоит длина: 2 байта, big-endian.
- У более длинного сообщения стоит маркер `0xFF 0xFF`, затем длина: 4 байта, big-endian.
- Максимальная длина сообщения — 16 МиБ. Более длинное сообщение вызывает `InvalidDataException`.

`PipeServiceBase.SendMessageAsync` использует блокировку, поэтому за раз записывается только один кадр.

## Модель сообщения

Сообщение — это URI:

```text
cdpiui://<Target>/<MessageType>?<key>=<value>&<key>=<value>&
```

- `<Target>` — имя из `PipeMessageTargetIds`.
- `<MessageType>` — имя из enum сообщений цели, например `CONPTYMessageIds`.
- `MessageBaseModel<T>.ToString` записывает имя значения enum, а не целое число. `IntMessageType` в URI не используется.
- Данные — это `NameValueCollection`. Ключи и значения экранируются через `Uri.EscapeDataString`.
- У сообщения без данных нет части `?`.

`PipeModelConvertor.ConvertBack` читает сообщение:

- Хост и путь разбираются через `Enum.Parse(..., ignoreCase: true)`. `Uri` переводит хост в нижний регистр.
- Строка запроса разбирается через `HttpUtility.ParseQueryString`.
- Неизвестная цель или неизвестный тип вызывают исключение. `PipeServiceBase` пишет его в лог и игнорирует сообщение.
- Значение `null` отправляется как пустая строка. Получатель получает `""`, а не `null`.

Используйте `PipePayloadCodec` (URL-безопасный Base64 от UTF-8) для двоичных данных и для путей, которые не должны измениться при передаче.
Его используют `CaptureHelpOutput` и сообщения с выводом справки.

## Семейства сообщений

Направление: «GUI → трей» или «трей → GUI». В GUI сообщения обрабатывают `CoreCommandsHandler` и `CDPIUI/Commands/CommandsHandler.cs`.

### Service

| Тип | Направление | Данные |
|---|---|---|
| `ConnectOK` | трей → GUI | нет |
| `RequestAuth` | GUI → трей | `GUID` |
| `AuthOK` | трей → GUI | нет. GUI вызывает событие `Connected`. |
| `AuthFAIL` | трей → GUI | нет. GUI пишет ошибку в лог. |

### Presentation

| Тип | Направление | Данные |
|---|---|---|
| `ShowWindow` | трей → GUI | `windowName` и необязательные ключи окна, например `page`, `id`, `isUpdateRequested`, `action`, `helpUrl` |

### CONPTY

Процессы компонентов. `componentId` — id компонента, например `CSZTBN012`.

| Тип | Направление | Данные | Действие |
|---|---|---|---|
| `StartProcessId` | GUI → трей | `componentId`, `exePath`, `args` | Запускает компонент через ConPTY. |
| `StopProcessId` | GUI → трей | `componentId` | Останавливает компонент. |
| `RestartProcessId` | GUI → трей | `componentId` | Перезапускает компонент. |
| `StopService` | GUI → трей | нет | Останавливает компоненты WinDivert, затем останавливает и удаляет службу `WinDivert`. |
| `GetProcessIdFullOutput` | GUI → трей | `componentId` | Трей отправляет `ProcessIdFullOutput` для задачи `componentId`. Если `componentId` пуст, трей отправляет его для всех задач. |
| `GetAllProcessStates` | GUI → трей | нет | Трей отправляет состояние и имя процесса всех задач. |
| `ProcessIdStartupArgsChanged` | GUI → трей | `componentId` | При следующем запуске трей запросит у GUI новую строку запуска. |
| `CaptureHelpOutput` | GUI → трей | `componentId`, `requestId`, `exePath` (`PipePayloadCodec`) | Запускает `<exe> --help` на 10 секунд. |
| `GetProcessIdState` | GUI → трей | `componentId` | Трей отправляет состояние и имя процесса задачи `componentId`. Если `componentId` пуст, трей отправляет их для всех задач. |
| `GetStartupString` | трей → GUI | `componentId` | GUI запускает задачу. |
| `GetAllStartupStrings` | трей → GUI | `componentId` (пустой) | GUI запускает все компоненты автозапуска. |
| `CleanOutputForId` | трей → GUI | `componentId` | GUI очищает вывод. |
| `MarkProcessIdAsStarted` | трей → GUI | `componentId` | |
| `MarkProcessIdAsStopped` | трей → GUI | `componentId`; при ошибке также `errorHappens`, `errorMessage`, `errorObject`, `stateSnapshot` | При `stateSnapshot=true` GUI не открывает окно ошибки. |
| `ChangeProcessIdExecutable` | трей → GUI | `componentId`, `processName` | |
| `ProcessIdNewOutput` | трей → GUI | `componentId`, `output` | |
| `ProcessIdFullOutput` | трей → GUI | `componentId`, `output` | |
| `HelpOutputChunk` | трей → GUI | `requestId`, `chunkIndex`, `output` (`PipePayloadCodec`) | Размер фрагмента — 12 КиБ UTF-8. |
| `HelpOutputCompleted` | трей → GUI | `requestId`, `totalChunks`, `exitCode`, `timedOut`, `error` (`PipePayloadCodec`) | GUI проверяет, что пришли все фрагменты. |

### GoodCheck

| Тип | Направление | Данные |
|---|---|---|
| `Start` | GUI → трей | `operationId`, `exeFileName`, `args` |
| `Stop` | GUI → трей | нет |
| `Running` | трей → GUI | `operationId`, `error` |
| `Died` | трей → GUI | `operationId`, `error` |
| `DiedViaError` | трей → GUI | `operationId`, `error` |

GUI читает `operationId` как целое число.

### Utils

| Тип | Направление | Данные |
|---|---|---|
| `GrantAccessRequest` | GUI → трей | `file`: папка внутри `Store` |
| `GrantAccessResponse` | трей → GUI | `result`: `True` или `False` |

### Settings

| Тип | Направление | Данные |
|---|---|---|
| `AddToAutorun` | GUI → трей | нет |
| `RemoveFromAutorun` | GUI → трей | нет |
| `ReloadSettings` | GUI → трей | нет |
| `ComponentSetupFinished` | GUI → трей | `componentId` |
| `ComponentSetupNotFinished` | GUI → трей | `componentId` |
| `ComponentNotInstalled` | GUI → трей | `componentId`. Трей останавливает и удаляет задачу. |
| `AutorunFalse` | трей → GUI | нет. GUI устанавливает `SYSTEM/autorun` в `false`. |

### Update

| Тип | Направление | Данные |
|---|---|---|
| `BeginApplicationUpdate` | GUI → трей | `filePath`: пакет обновления (`.msi` или ZIP) |
| `UpdatesAreAvailable` | GUI → трей | нет. Трей показывает уведомление. |
| `CheckForUpdates` | трей → GUI | нет |

### MSIInstallation

| Тип | Направление | Данные |
|---|---|---|
| `Begin` | GUI → трей | `operationId`, `fileName` |
| `Kill` | GUI → трей | `operationId` |
| `SetOperationStatus` | трей → GUI | `operationId`, `state`: имя из `MsiState` |
| `RemoveOperationId` | трей → GUI | `operationId`, `state` (пустой) |

Значения `MsiState`: `GettingReady`, `Installing`, `Complete`, `CompleteRestartRequest`, `ExceptionHappens`.
Этот enum есть в `CDPIUI.TrayIcon/Helper/MsiInstallerHelper.cs` и в `CDPIUI.Core/Store/MSI/MsiInstallerService.cs`. Имена должны совпадать.

### Proxy

Все сообщения идут от GUI к трею.

| Тип | Данные |
|---|---|
| `Init` | `componentId`, `proxyFirePath` |
| `Setup` | `componentId`, `proxyType` (`AllSystem` или `ProxiFyre`), `ip`, `port` |
| `Clean` | `componentId` |

### CompatibilityCheck

| Тип | Направление | Данные |
|---|---|---|
| `Begin` | трей → GUI | нет |

### Notifications

Все сообщения идут от GUI к трею. Трей показывает всплывающее уведомление.

| Тип | Данные |
|---|---|
| `ProxySetupRequired` | `componentName` |
| `CompatibilityCheckAssistant` | `componentName` |

### Application

| Тип | Направление | Данные |
|---|---|---|
| `HardRestart` | GUI → трей | нет. Трей закрывает GUI, снова запускает его и завершает работу. |
| `CloseApplicationUI` | трей → GUI | нет. GUI завершает свой процесс. |

### ConditionalLaunch

| Тип | Направление | Данные |
|---|---|---|
| `ExecuteAction` | трей → GUI | `operationId`, `actionType` и параметры действия |
| `ActionCompleted` | GUI → трей | `operationId`, `success`, `error` |
| `ReloadTasks` | GUI → трей | `tasksDirectory` |
| `ExecuteTask` | GUI → трей | `taskId` |

Трей ждёт `ActionCompleted` 45 секунд.

<a id="messages-when-the-gui-is-not-connected"></a>
## Сообщения, когда GUI не подключён

Если отправка не удалась, `PipeHelper.TrySendMessage` в трее запускает GUI от имени пользователя рабочего стола.
Для всех целей сообщение передаётся в аргументе `--direct:<message>`.

- Для фоновых сообщений к целям `Service`, `CONPTY`, `Settings`, `Application`, `ConditionalLaunch`, `CompatibilityCheck` и `Update` трей также добавляет `--create-no-window`.
- Для остальных целей, например `Presentation`, трей не добавляет `--create-no-window`.

GUI обрабатывает `--direct:` через `CoreCommandsHandler`, затем через `CommandsHandler.HandleCommand`.
Трей не использует `----ms-protocol:`. GUI обрабатывает `----ms-protocol:` через `CommandUriMapper`, который принимает только публичные маршруты. Поэтому сообщение pipe в `----ms-protocol:`, например `ShowWindow`, не действует.

## Добавление сообщения

1. Добавьте значение в конец enum в `Pipe/Models/PipeMessageIds.cs`.
2. Для новой цели добавьте модель в `PipeMessageModels.cs` и ветку `case` в `PipeModelConvertor.ConvertBack`.
3. Добавьте отправитель в `PipeHelper` отправляющей стороны.
4. Добавьте обработчик в `CommandsHandler` (трей) или в `CoreCommandsHandler` / `CDPIUI/Commands/CommandsHandler.cs` (GUI).
5. В трее проверяйте каждый путь от GUI через `TrustedPaths`, прежде чем его использовать.

Тесты: `CDPIUI.Tests/Shared/PipeMessageModelTests.cs`, `StreamStringTests.cs`, `PipePayloadCodecTests.cs`.
