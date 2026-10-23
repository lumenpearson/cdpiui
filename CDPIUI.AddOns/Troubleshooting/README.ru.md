[English](README.md) | **Русский**

# Дополнение «Устранение неполадок»

## Назначение

Дополнение «Устранение неполадок» находит частые причины проблем на компьютере и в Магазине. Некоторые из них оно может исправить. Вся логика находится в `TroubleshootingService` (`TroubleshootingService.cs`). У дополнения две проверки:

- базовая проверка: службы Windows, сетевые настройки и программы, которые могут мешать обходу DPI;
- проверка Магазина: доступ к репозиториям Магазина и право записи в папки Магазина.

## Основные типы

| Тип | Функция |
|---|---|
| `TroubleshootingService` | Синглтон (`Instance`). Выполняет проверки и исправления. |
| `RunBasicDialogStates` | Шаги базовой проверки |
| `StoreCheckStates` | Шаги проверки Магазина |
| `DiagnosticStateModel` | Правильное значение шага и ключи его отображаемых текстов |
| `FixResultModel` | Результат исправления: `IsFixed` и `ErrorCode` |
| `BasicDiagnosticStateModelPairs`, `StoreDiagnosticStateModelPairs` | Статические таблицы соответствия шага и его `DiagnosticStateModel` |

Перед каждой проверкой сервис вызывает `CurrentStateChanged` с текущим шагом.

## Базовая проверка

`RunBasicDiagnostic` сначала останавливает все задачи компонентов. Затем отправляет трею запрос `StopService` (`ProcessService.StopService`) и не ждёт результата. Трей останавливает задачи GoodbyeDPI, Zapret и Zapret2 и выполняет `sc stop WinDivert` и `sc delete WinDivert`. Затем сервис ждёт 1 секунду и выполняет проверки ниже. Он возвращает таблицу, в которой каждому шагу соответствует значение `bool`.

| Шаг | Проверка | Правильное значение |
|---|---|---|
| `CheckBFE` | Служба `BFE` работает | `true` |
| `CheckProxy` | Прокси Windows включён (`WindowsNativeProxySettingsService.IsProxyEnabled`) | `false` |
| `CheckNetsh` | `netsh.exe` есть в текущей папке или в `PATH` | `true` |
| `CheckTimestamps` | PowerShell `(Get-NetTCPSetting -SettingName Internet).Timestamps` возвращает `Enabled` | `true` |
| `CheckAdGuard` | Работает процесс `adguardvpnsvc`, `adguardvpn` или `adguardsvc` | `false` |
| `CheckKiller` | Работает служба, в имени которой есть `Killer` | `false` |
| `CheckIntelConnectivityNetwork` | Работает служба, в имени или отображаемом имени которой есть `Intel` и `Connectivity Network` | `false` |
| `CheckCheckPointServices` | Работает служба, в имени которой есть `TracSrvWrapper` или `EPWD` | `false` |
| `CheckSmartByte` | Работает служба, в имени которой есть `SmartByte` | `false` |
| `CheckVPNs` | Работает служба, в имени которой есть `vpn` | `false` |
| `CheckDNS` | Хотя бы у одного сетевого интерфейса есть флаги DoH (`DohFlags > 0` в `HKLM:System\CurrentControlSet\Services\Dnscache\InterfaceSpecificParameters`) | `true` |
| `CheckWinDivert` | Работает служба, в имени которой есть `windivert` | `false` |
| `CheckAnotherComponentsServices` | Существует служба, в имени которой есть `GoodbyeDPI`, `discordfix_zapret`, `winws1` или `winws2` | `false` |
| `CheckAnotherComponents` | Работает процесс `winws`, `winws2` или `goodbyedpi` | `false` |

Проверки имён служб не учитывают регистр. Служба считается «работающей», если её состояние — `Running`, `StartPending` или `StopPending`. Сервис читает список служб один раз за каждый запуск проверки.

Вызовы PowerShell используют `powershell.exe -NoProfile -NonInteractive -Command`. Сервис останавливает программу через 30 секунд.

## Проверка Магазина

`RunStoreDiagnostic` тоже сначала останавливает все задачи компонентов и отправляет запрос `StopService`. Затем выполняет следующие проверки:

| Шаг | Проверка | Правильное значение |
|---|---|---|
| `CheckGitHubRepo` | `StoreHelper.TryLoadDatabaseForVersionControl(GitHub)` | `true` |
| `CheckGitLabRepo` | `StoreHelper.TryLoadDatabaseForVersionControl(GitLab)` | `true` |
| `CheckWriteAccess` | Право записи в `Directories.StoreDirectory` | `true` |
| `CheckWriteAccessIntoItemsFolder` | Право записи в `Directories.StoreItemsDirectory` | `true` |
| `CheckWriteAccessIntoRepoFolder` | Право записи в `Directories.StoreRepoCacheDirectory` | `true` |
| `CheckWriteAccessIntoDatabaseFolder` | Право записи в `Directories.StoreLocalCacheDirectory` | `true` |

Проверка записи создаёт временный файл со случайным именем и параметром `DeleteOnClose`.

## Исправления

`FixAllBasicErrors` и `FixAllStoreErrors` пытаются исправить каждый шаг, значение которого отличается от правильного. Все остальные шаги возвращают `IsFixed = false` и `INFO_CANNOT_FIX`.

| Шаг | Исправление |
|---|---|
| `CheckTimestamps` | Выполняет `netsh interface tcp set global timestamps=enabled`. Исправление успешно, если код выхода равен 0. |
| `CheckWinDivert` | Отправляет запрос `StopService` через `ProcessService.StopService`. Затем проверяет каждые 500 мс, не более 20 раз, пока служба не перестанет работать. |
| `CheckWriteAccess`, `CheckWriteAccessIntoItemsFolder`, `CheckWriteAccessIntoRepoFolder`, `CheckWriteAccessIntoDatabaseFolder` | Отправляет трею сообщение pipe `GrantAccessRequest` для папки (`PipeHelper.SendGrantAccessPacket`) |

Неудачное исправление возвращает `ERR_WIN32_UNKNOWN`.

Трей изменяет доступ только для путей в Магазине (`TrustedPaths.IsInStore`). Он отправляет ответ `GrantAccessResponse`. `CDPIUI/Commands/CommandsHandler.cs` передаёт ответ в `TroubleshootingService.OnGrantAccessCompleted`. Если ответ не приходит за 30 секунд, исправление возвращает `false`.

## Точки входа в GUI

| Точка входа | Расположение |
|---|---|
| Окно | `CDPIUI/Windows/TroubleshootingWindow.xaml(.cs)` |
| Начальная страница с двумя проверками | `CDPIUI/Views/Troubleshooting/MainPage.xaml.cs` |
| Ход выполнения, результаты и исправления | `CDPIUI/Views/Troubleshooting/WorkPage.xaml.cs` |
| Страница инструментов | `CDPIUI/Views/UtilsPage.xaml.cs` |
| Окно ошибки компонента | `CDPIUI/Windows/Messages/ComponentErrorWindow.xaml` (ссылка `cdpiui://Tools/Troubleshooting`) |
| Диалог ошибки загрузки из Магазина | `CDPIUI/Controls/Dialogs/Store/DownloadErrorActionsContentDialog.xaml.cs` |

Маршруты в `CDPIUI/Commands/CommandUriMapper.cs`:

```text
cdpiui://Tools/Troubleshooting
cdpiui://Tools/Troubleshooting/BasicCheck
cdpiui://Tools/Troubleshooting/StoreCheck
```

`BasicCheck` открывает `WorkPage` с действием `BeginBasicCheck`. `StoreCheck` открывает её с `BeginStoreRepoCheck`. `WorkPage` показывает каждый результат с ключами отображаемых текстов из `DiagnosticStateModel`.

## Ограничения

- Обе проверки останавливают все компоненты. После проверки компоненты не запускаются снова автоматически.
- Большинство проверок имён ищут подстроку в имени службы. Например, `CheckVPNs` находит любую работающую службу, в имени которой есть `vpn`.
- `CheckAnotherComponentsServices` проверяет, что служба существует, а не что она работает.
- Исправление есть только у шагов timestamps, WinDivert и доступа к папкам Магазина.
- В операционной системе, отличной от Windows, проверки служб возвращают `true`.
- У дополнения нет настроек и нет тестов в `CDPIUI.Tests`.
