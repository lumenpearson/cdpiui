[English](README.md) | **Русский**

# CDPIUI

`CDPIUI` — проект главного окна (GUI) на WinUI 3 (.NET 8, Windows App SDK). Он показывает окна приложения.
Трей `CDPIUI_TrayIcon.exe` поддерживает работу компонентов. GUI подключается к трею через именованный канал (pipe).
Логика, которой не нужен UI, находится в `CDPIUI.Core`, `CDPIUI.Shared` и `CDPIUI.AddOns`.

## Запуск

### Program.cs

`Program.Main` — точка входа. Компилятор XAML не добавляет в этот проект собственный метод `Main`.

1. `Main` вызывает `DecideRedirection`.
2. `DecideRedirection` регистрирует ключ `CDPI_GUIApp` через `AppInstance.FindOrRegisterForKey`.
3. Если ключ принадлежит текущему процессу, процесс сохраняет аргументы активации в `InitialActivationArguments`. Затем он запускает `App`.
4. Если ключ принадлежит другому процессу, `RedirectActivationTo` отправляет активацию этому процессу. Предел ожидания — 30 секунд.
5. Если запущенный экземпляр не принимает активацию, процесс снова пытается зарегистрировать ключ. Это происходит, когда запущенный экземпляр закрывается.

Запущенный экземпляр получает перенаправленную активацию в `OnActivated`. Активации, которые приходят до готовности потока UI, попадают в очередь.
После создания `App` метод `DispatchPendingActivations` передаёт их в `App.PreferRequestedActions` в потоке UI.

`Program` также находит аргумент миграции с GoodbyeDPI (`GoodbyeDpiMigrationActivation`). Пока миграция ожидает выполнения, приложение не открывает обычное стартовое окно.

### Запуск трея и `--gui-args:`

При запуске `PipeClientService` подключается к pipe трея. Предел ожидания — 2 секунды. Если процесс трея запущен, GUI ждёт ещё 15 секунд. Если подключиться не удаётся, он запускает `CDPIUI_TrayIcon.exe` с правами администратора. Затем процесс GUI завершается.

- GUI передаёт трею `--autorun`, если настройка `APPEARANCE/hideToTrayOnStartup` равна true. Иначе он передаёт `--show-ui`.
- Если у GUI есть собственные аргументы, он всегда передаёт `--show-ui` и аргумент `--gui-args:`. Аргумент миграции с GoodbyeDPI передаётся трею без кодирования.
- GUI кодирует свои аргументы через `GuiActivationArguments.Encode` (`CDPIUI.Shared/Basic/CommandLine`). Результат — один аргумент `--gui-args:`.
- Каждый аргумент кодируется в UTF-8 и Base64. Аргументы разделяет точка. Максимальная длина в закодированном виде — 16 КиБ. Более длинное значение не отправляется.
- Трей декодирует значение через `GuiActivationArguments.TryDecode`. Он снова запускает `CDPIUI.exe` с исходными аргументами.

Поэтому открытый файл, ссылка `cdpiui://` или команда `--direct:` не теряются, когда трей не запущен.

### `--direct:` и другие аргументы

Трей запускает GUI с `--direct:<pipe message>`, когда GUI не подключён. Это происходит для всех целей сообщений, например для сообщений `ShowWindow`.
Для фоновых действий трей также добавляет `--create-no-window`. Трей не использует `----ms-protocol:`.

`App.PipeConnectedActions` обрабатывает аргументы после подключения pipe:

| Аргумент или активация | Обработчик |
| ---------------------- | ------- |
| Аргумент миграции с GoodbyeDPI | `TryHandleMigrationActivationAsync`, открывает `WelcomeWindow` с `WelcomeWindow.MigrationWindowId` |
| Активация по файлу или путь к файлу | `ProcessFiles` |
| Активация по протоколу (`cdpiui://`) | `CommandsHandler.HandleCommandAsync` (только публичные маршруты) |
| `----ms-protocol:<uri>` | `CommandsHandler.HandleCommandAsync` (только публичные маршруты) |
| `--direct:<message>` | `CoreCommandsHandler.HandleCommandAsync`, затем `CommandsHandler.HandleCommand` (сообщения pipe принимаются) |

`ProcessFiles` принимает `.cdpisignedpack`, `.cdpiconfigpack`, `.cdpipatch`, `.cdpitask` и `.cdpiconfig`. Он обрабатывает только первый поддерживаемый файл.

Если ни один аргумент не открывает окно, `OpenStartupWindowAsync` открывает `WelcomeWindow` или `ModernMainWindow`. `WelcomeWindow` открывается, пока настройка `WELCOMEWIZARD/Shown` не станет true.

Перенаправленная активация попадает в `App.PreferRequestedActions`. Он так же обрабатывает ссылки протокола, файлы, аргумент миграции и `--direct:`.

### App.xaml.cs

Конструктор `App` выполняет такие шаги:

1. Регистрирует обработчики необработанных исключений. Исключение XAML записывается в журнал и показывается в `ErrorContentDialog`. Приложение не закрывается.
2. Подключает `PipeClientService.MessageReceived` к `CommandsHandler.HandleCommand`.
3. Вызывает `CoreErrorsLookupService.Init`.
4. Применяет тему из `APPEARANCE/Theme`.
5. Вызывает `PipeClientService.Init` и подписывается на `Connected`.
6. Подписывается на `ApplicationTaskMonitor.StoreStateChanged`.
7. Вызывает `GetReadyFeatures`.

`GetReadyFeatures` запускает `GetReadyFeaturesAsync` и сохраняет задачу в `_localizerReady`. Задача восстанавливает базу данных (`DatabaseInitializationService.QuickRestore`), создаёт локализатор и регистрирует сопоставления файлов и протокол `cdpiui` (`FileAssociationService`).

`OnLaunched` создаёт `PrepareWindow` (кроме случая миграции с GoodbyeDPI) и вызывает `PipeClientService.Start`. `PrepareWindow` закрывается после стартовых действий. `App` также создаёт его, когда выполняется операция Магазина и другие окна не открыты.

Приложение закрывается в `ExitIfIdle`, когда ни одно окно не открыто и не выполняется ни одна операция Магазина или GoodCheck.

### Готовность локализатора

Окна читают свои строки из локализатора в своих конструкторах. Поэтому окно нельзя создавать до того, как создан локализатор.
Все перегрузки `SafeCreateNewWindow` и `UnsafeCreateNewWindow` ждут `_localizerReady`, прежде чем создать окно.

ВНИМАНИЕ: Не создавайте окно через `new` во время запуска. Используйте методы окон `App`, потому что они ждут локализатор.

## Окна

### Правило пространства имён

Все окна находятся в пространстве имён `CDPIUI`, а не в `CDPIUI.Windows`. Файлы находятся в папке `Windows`.
`WindowOpenHelper` находит тип окна по имени `CDPIUI.<windowName>`. Окно в другом пространстве имён нельзя открыть по ссылке.
Диалоговые окна в `Windows/Messages`, которые используют `CDPIUI.Messages`, нельзя открыть по ссылке. `TemplateWindow` находится в `CDPIUI.Default`.

### TemplateWindow

Большинство окон наследуется от `TemplateWindow` (`Windows/Default/TemplateWindow.cs`), а он наследуется от `WinUIEx.WindowEx`. Устаревшее `MainWindow` наследуется напрямую от `WindowEx`.
`TemplateWindow` даёт такие функции:

- Собственная строка заголовка (`CustomTitleBarUserControl`, `TitleBarMode` из `APPEARANCE/titleBarMode`).
- `WindowTitle`, `IconUri` и `WindowMinSize`.
- `Id`: идентификатор для окон, у которых может быть больше одного экземпляра.
- `MainFrame` и `NavigateToPageWithParameter`, который передаёт странице `NameValueCollection`.
- `NavigateBackWithParameter` и `RemoveAndGoBackTo` для навигации назад.

### Открытие окна

`App.OpenWindows` содержит все открытые окна. `RegisterWindow` добавляет окно в список, применяет тему и подписывается на `Closed`.
Когда окно закрывается, `App` сохраняет его размер и положение (`WindowsPositionHelper`) и удаляет его из списка.

| Метод | Применение |
| ------ | --- |
| `SafeCreateNewWindow<TWindow>()` | Один экземпляр типа окна. Если открыт один экземпляр, метод активирует его. Если открыто больше экземпляров, метод закрывает их и создаёт новое окно. |
| `SafeCreateNewWindow(Type)` | То же самое для типа из ссылки. |
| `UnsafeCreateNewWindow<TWindow>(activate, id)` | Несколько экземпляров одного типа. Окно с тем же `Id` активируется. Иначе создаётся новое окно. |
| `UnsafeCreateNewWindow(Type, activate, id)` | То же самое для типа из ссылки. |
| `ShowWindowModalAsync` / `MakeWindowNormal` | Отключают окна-владельцы, пока окно модальное, затем снова включают их. |
| `CloseWindow<TWindow>()` / `CloseWindow<TWindow>(id)` | Закрывают все окна типа или одно окно с `Id`. |

Не создавайте окно через `new` и не вызывайте `Activate` напрямую. Используйте методы `App`, иначе `OpenWindows`, тема и `ExitIfIdle` не учтут это окно.

### WindowOpenHelper

`Helper/WindowHelper/WindowOpenHelper.OpenAsync` открывает окно по сообщению `ShowWindow`. Параметры сообщения — `windowName`, `page`, `id` и параметры страницы.

- `MainWindow` заменяется на `ModernMainWindow`. `LegacyMainWindow` заменяется на `MainWindow`.
- Если открыто `WelcomeWindow` или мастер приветствия не завершён, запрос главного окна открывает стартовое окно.
- Тип должен находиться в сборке приложения и в пространстве имён `CDPIUI`. Он должен наследоваться от `Window`, не должен быть абстрактным и должен иметь публичный конструктор без параметров.
- С `id` вспомогательный класс использует `UnsafeCreateNewWindow`. Без `id` он использует `SafeCreateNewWindow`.
- Для `OfflineHelpWindow` параметр `helpUrl` передаётся в `NavigateToPage`.
- Страницу может получить только `TemplateWindow`. Вспомогательный класс находит тип страницы через `GetPageType`.

`GetPageType` пытается найти `CDPIUI.Views.<window name without "Window">.<page>`. Для `ModernMainWindow` он использует `CDPIUI.Views.Main.<page>` и `CDPIUI.Views.<page>`. `HomePage` даёт `ModernMainWindow.GetMainPage()`.
Для `StoreWindow` страница `HomePage` — это `CDPIUI.HomePage`. Имя страницы может содержать вложенное пространство имён, например `Settings.Memory.MemoryViewLogsDetailsPage`.

<a id="main-window"></a>
## Главное окно

В проекте есть два класса главного окна: `ModernMainWindow` и `MainWindow`. Приложение открывает только `ModernMainWindow`.
`MainWindow` (`Windows/MainWindow.xaml`) — устаревший код. Он компилируется, но ни один путь кода приложения его не открывает. Это мёртвый код.

### Какое окно открывается

| Запрос | Код | Результат |
| ------ | --- | --------- |
| Запуск без аргумента, который открывает окно | `App.PipeConnectedActions` вызывает `OpenStartupWindowAsync` | Стартовое окно (см. ниже) |
| Перенаправленная активация без результата | `App.PreferRequestedActions` вызывает `OpenStartupWindowAsync` | Стартовое окно |
| Завершение мастера приветствия | `WelcomeWindow.CompleteButton_Click` | `SafeCreateNewWindow<ModernMainWindow>()` |
| `cdpiui://Main` и все маршруты `cdpiui://Main/...` | `CommandUriMapper` передаёт `windowName=MainWindow` | `WindowOpenHelper` заменяет имя на `ModernMainWindow` |
| Сообщения `ShowWindow` от трея с `windowName=MainWindow` | `TrayMenuForm.MaximizeButton_MouseClick`, `EmptyForm.MaximizeApp`, `NotifyHelper` (действия уведомлений `SHOW_MAIN_WINDOW` и `UPDATE:OPEN_DOWNLOAD_PAGE`), `ComponentUserControl.OpenSettingsButton_Clicked`, условный запуск (`ConditionalActionExecutor.OpenMainPageAsync`) | `ModernMainWindow` |
| Код GUI, которому нужно главное окно | Например, `ItemViewPage`, `StoreLibraryItemButton`, `ComponentTileUserControl`, `ConnectTelegramProxyContentDialog` | `SafeCreateNewWindow<ModernMainWindow>()` |
| Сообщение `ShowWindow` с `windowName=LegacyMainWindow` | `WindowOpenHelper` заменяет имя на `MainWindow` | `MainWindow`. Ни маршрут, ни код трея не передают это имя. |
| `App.NavigateToUpdatesPage` | `SafeCreateNewWindow<MainWindow>()` | `MainWindow`. Никакой код не вызывает этот метод. |

`OpenStartupWindowAsync` выбирает стартовое окно в таком порядке:

1. Если открыто окно `WelcomeWindow`, метод активирует его.
2. Если `WELCOMEWIZARD/Shown` не равно `true`, метод открывает `WelcomeWindow`.
3. Иначе метод открывает `ModernMainWindow`.

`WindowOpenHelper` применяет то же правило к обоим именам главного окна. Если открыто `WelcomeWindow` или `WELCOMEWIZARD/Shown` не равно `true`, запрос `ModernMainWindow` или `MainWindow` вызывает `OpenStartupWindowAsync`. Тогда вспомогательный класс игнорирует `page` и параметры страницы из запроса.

Если GUI не подключён, трей запускает `CDPIUI.exe` с `--direct:<message>` (`PipeHelper.TrySendMessage` в трее). После подключения pipe GUI передаёт сообщение в `CommandsHandler.HandleCommand`. Поэтому сообщение `ShowWindow` от трея в обоих случаях проходит через `WindowOpenHelper`.

### Окно не выбирается настройкой

Ни одна настройка не выбирает `MainWindow` или `ModernMainWindow`. Настройка `APPEARANCE/mainPageMarkup` выбирает только домашнюю страницу в `ModernMainWindow`:

| `APPEARANCE/mainPageMarkup` | Домашняя страница (`ModernMainWindow.GetMainPage()`) |
| --------------------------- | ---------------------------------------------------- |
| `Classic` (значение по умолчанию в `Template/Settings/Settings.xml`) | `Views/MainPage` |
| `Modern` | `Views/ModernMainPage` |

`MainPageMarkupManager` (`ViewModels/MainPageMarkupViewModel.cs`) читает настройку один раз, в своём конструкторе. Пользователь меняет значение в `WelcomeWindow` и в `Settings.PersonalizePage`. Если сохранённое значение не равно `Classic` или `Modern`, `CurrentMarkup` равно `null`.

### Различия двух окон

| Функция | `ModernMainWindow` | `MainWindow` (устаревшее) |
| ------- | ------------------ | ------------------------- |
| Базовый класс | `TemplateWindow` | `WinUIEx.WindowEx` |
| Страница из ссылки (параметр `page`) | Да, через `NavigateToPageWithParameter` | Нет. `WindowOpenHelper` останавливается, потому что окно не является `TemplateWindow`. |
| Запрос с `id` | Да | Нет. `UnsafeCreateNewWindow` приводит новое окно к `TemplateWindow`, и приведение завершается ошибкой. |
| Домашняя страница | `GetMainPage()`: `MainPage` или `ModernMainPage` | Всегда `Views.MainPage` |
| Навигация | Верхняя панель: Home (`MAINPAGE`), Utilities, Settings, About | Левая панель: Home, Utilities, по одному пункту для каждого установленного компонента (Zapret, ByeDPI, GoodbyeDPI, SpoofDPI), пункт добавления (`AddNewComponent`) со всплывающим меню, которое открывает `StoreWindow`, Settings, About |
| Запрос перезагрузки после установки драйвера или среды выполнения (`CoreEvents.RestartRequired`) | Не обрабатывается | Показывает `ContentDialog`. Основная кнопка (`RestartNow`) запускает `shutdown.exe /r /t 0`. |
| Строка заголовка | `TitleBarUserControl` | Собственная строка заголовка с `PreviewTextBlock` |
| Минимальный размер | Не задан | 484 × 300 (`WM_GETMINMAXINFO`) |
| Размер и положение по умолчанию (`WindowsPositionHelper`) | `OnlyPositionWizard` (900 × 550, по центру) | `ResizeAndPositionMain` (800 × 600, по центру) |

Пункты навигации `MainWindow` имеют теги в старом пространстве имён (`CDPI_UI.Views.*`). `Type.GetType` возвращает для этих тегов `null`. Поэтому устаревшая навигация не меняет страницу.

ВНИМАНИЕ: Подписчик события `CoreEvents.RestartRequired` есть только в `MainWindow`. Так как `MainWindow` никогда не открывается, GUI не показывает запрос перезагрузки. `CoreEvents.IsRestartPending` остаётся `true`.

Не добавляйте новые функции в `MainWindow`. Добавляйте их в `ModernMainWindow` или в его страницы. Ссылки и сообщения трея должны использовать имя `MainWindow`, потому что `WindowOpenHelper` заменяет его на `ModernMainWindow`.

## Схема ссылок `cdpiui://`

Файлы находятся в `Commands`:

- `CommandUriMapper` преобразует ссылку в `IPipeMessage`.
- `CommandsHandler` выполняет сообщение.
- `ExternalUriLauncher` открывает веб-ссылки в XAML через `UrlOpenHelper`.

Сегменты хоста и пути не чувствительны к регистру. Сегмент `{name}` — это параметр. Сегмент `{*name}` забирает оставшуюся часть пути.

### Публичные маршруты

| Ссылка | Результат |
| ---- | ------ |
| `cdpiui://AppFeatures` | `AppFeaturesWindow` |
| `cdpiui://Welcome` | `WelcomeWindow` |
| `cdpiui://Main` | Главное окно |
| `cdpiui://Main/Home` | Главное окно, главная страница |
| `cdpiui://Main/Utilities` | Главное окно, `UtilsPage` |
| `cdpiui://Main/Settings` | Главное окно, `SettingsPage` |
| `cdpiui://Main/Settings/Autorun` | Главное окно, `Settings.AutorunPage` |
| `cdpiui://Main/Settings/Personalization` | Главное окно, `Settings.PersonalizePage` |
| `cdpiui://Main/About` | Главное окно, `AboutPage` |
| `cdpiui://Main/Updates` | `AboutPage` с `isUpdateRequested=True` |
| `cdpiui://Main/Components/{componentId}` | `Components.ViewComponentSettingsPage` с `componentId` |
| `cdpiui://Store` | `StoreWindow` |
| `cdpiui://Store/Home` | `StoreWindow`, `HomePage` |
| `cdpiui://Store/Catalog/{itemId}` | `ItemViewPage` с `itemId` и `setFocus=ItemActionButton` |
| `cdpiui://Store/Category/{categoryId}` | `CategoryViewPage` с `categoryId` |
| `cdpiui://Store/Downloads` | `DownloadsPage` |
| `cdpiui://Store/Updates` | `DownloadsPage` с `isUpdateRequested=True` |
| `cdpiui://Store/Library` | `LibraryPage` |
| `cdpiui://Store/Settings` | `SettingsPage` |
| `cdpiui://Store/Settings/Memory` | `Settings.MemoryViewPage` |
| `cdpiui://Store/Settings/Memory/Application` | `MemoryViewApplicationFilesDetailsPage` |
| `cdpiui://Store/Settings/Memory/InstalledItems` | `MemoryViewInstalledItemsDetailsPage` |
| `cdpiui://Store/Settings/Memory/Logs` | `MemoryViewLogsDetailsPage` |
| `cdpiui://Store/Settings/Memory/Settings` | `MemoryViewSettingsDetailsPage` |
| `cdpiui://Store/Settings/Memory/StoreCache` | `MemoryViewStoreCachePage` |
| `cdpiui://Store/Settings/Memory/ConditionalLaunch` | `MemoryViewConditionalLaunchDetailsPage` |
| `cdpiui://Tools/Console/{componentId}` | `ViewWindow` с `id` |
| `cdpiui://Tools/Service` | `ServiceWindow` |
| `cdpiui://Tools/ConditionalLaunch` | `ConditionalLaunchWindow` |
| `cdpiui://Tools/AutoConfig` | `CreateConfigUtilWindow` |
| `cdpiui://Tools/AutoConfig/Zapret2` | `CreateConfigUtilWindow`, `MainPage`, Zapret2, `action=BlockCheck2` |
| `cdpiui://Tools/AutoConfig/Zapret2/Reports` | То же самое, `action=BlockCheck2Reports` |
| `cdpiui://Tools/AutoConfig/{componentId}` | `CreateConfigUtilWindow`, `MainPage` с `componentId` |
| `cdpiui://Tools/ConfigEditor` | `ConfigMakerWindow` |
| `cdpiui://Tools/ConfigEditorLegacy` | `CreateConfigHelperWindow` |
| `cdpiui://Tools/ImportConfig` | `ConfigImportUtilWindow` |
| `cdpiui://Tools/ImportConfig/{componentId}` | `ConfigImportUtilWindow`, `MainPage` с `componentId` |
| `cdpiui://Tools/CreateConfig/{componentId}` | `ConfigMakerWindow` с `id` |
| `cdpiui://Tools/EditConfigKit/{kitId}` | `CreateConfigHelperWindow`, `EditConfigKitPage` с `kitId` |
| `cdpiui://Tools/Help` | `OfflineHelpWindow` |
| `cdpiui://Tools/Proxy` | `ProxySetupUtilWindow` |
| `cdpiui://Tools/Troubleshooting` | `TroubleshootingWindow` |
| `cdpiui://Tools/Troubleshooting/BasicCheck` | `WorkPage`, `action=BeginBasicCheck` |
| `cdpiui://Tools/Troubleshooting/StoreCheck` | `WorkPage`, `action=BeginStoreRepoCheck` |
| `cdpiui://Tools/PresetTest` | `ConfigTestWindow` |
| `cdpiui://Tools/Hosts` | `EditHostFileWindow` |
| `cdpiui://Help` | `OfflineHelpWindow` |
| `cdpiui://Help/{*helpUrl}` | `OfflineHelpWindow` с `helpUrl=/<path>` |
| `cdpiui://Actions/CheckForUpdates` | Проверка обновлений приложения с уведомлением |
| `cdpiui://Actions/CompatibilityCheck` | Запуск проверки совместимости |

Порядок маршрутов важен. Используется первый подходящий маршрут. Например, `Tools/AutoConfig/Zapret2` стоит перед `Tools/AutoConfig/{componentId}`.

### Сообщения pipe не принимаются из ссылок

У `CommandUriMapper.ConvertBack` есть параметр `allowPipeMessages`. Его значение по умолчанию — `false`.

- Если у хоста есть маршруты и ни один маршрут не подходит, результат — `null`.
- Если у хоста нет маршрутов, ссылка преобразуется через `PipeModelConvertor` только тогда, когда `allowPipeMessages` равен `true`.

`CommandsHandler.HandleCommandAsync(string)` предназначен для ссылок извне приложения: активации по протоколу, `----ms-protocol:`, встроенной справки. Он принимает только публичные маршруты.
`CommandsHandler.HandleCommand(string)` предназначен для ссылок, которые создаёт само приложение, например `CommandParameter` в XAML. Он также принимает сообщения pipe.

ПРЕДУПРЕЖДЕНИЕ: Не передавайте в `HandleCommand(string)` ссылку из браузера, файла или страницы справки. Иначе веб-страница сможет подделать сообщения трея.

`CommandsHandler` выполняет `PresentationMessageModel` (только `ShowWindow`), `GoodCheckMessageModel`, `UtilsMessageModel`, `UpdateMessageModel` и `CompatibilityCheckMessageModel`. Другие типы сообщений он игнорирует.

Чтобы привязать ссылку в XAML, используйте `Command="{x:Bind commands:CommandsHandler.Instance.Command}"` и поместите ссылку в `CommandParameter`.

### Добавление маршрута

1. Добавьте `CommandRoute` в `CommandUriMapper.Routes`. Для окна используйте `CreateWindowRoute`.
2. Ставьте конкретный маршрут перед маршрутом с параметром.
3. Убедитесь, что окно находится в пространстве имён `CDPIUI`.
4. Убедитесь, что страница находится в `CDPIUI.Views.<window prefix>`.

## Страницы и представления

Страницы находятся в `Views`. Большинство страниц наследуется от `TemplatePage` (`Controls/Default/TemplatePage.cs`).

| Папка | Окно |
| ------ | ------ |
| `Views` (корень) | Страницы главного окна: `MainPage`, `ModernMainPage`, `UtilsPage`, `SettingsPage`, `AboutPage` |
| `Views/Settings` | Страницы настроек главного окна |
| `Views/Main/Components` | Страницы настроек компонентов |
| `Views/Store`, `Views/Store/Settings`, `Views/Store/Settings/Memory` | `StoreWindow` |
| `Views/CreateConfigUtil` | `CreateConfigUtilWindow` |
| `Views/BlockCheck2` | Подбор пресета Zapret2 |
| `Views/ConfigImportUtil` | `ConfigImportUtilWindow` |
| `Views/CreateConfigHelper` | `CreateConfigHelperWindow` (устаревший редактор пресетов) |
| `Views/EditHostsFile` | `EditHostFileWindow` |
| `Views/SetupProxy` | `ProxySetupUtilWindow` |
| `Views/Troubleshooting` | `TroubleshootingWindow` |

Пространство имён страницы совпадает с её папкой, с одним исключением: `Views/Store/HomePage` находится в `CDPIUI`.
Страницы получают параметры как `NameValueCollection` в `OnNavigatedTo`.

## ContentDialogQueue

Окно может одновременно показывать только один `ContentDialog`. Второй вызов `ShowAsync` вызывает исключение.
`ContentDialogQueue.ShowQueuedAsync` (`Helper/Basic/ContentDialogQueue.cs`) — метод расширения. Он ждёт предыдущие диалоги в том же `XamlRoot`.

```csharp
dialog.XamlRoot = this.XamlRoot;
var result = await dialog.ShowQueuedAsync();
```

Задайте `XamlRoot` перед вызовом. Если `XamlRoot` равен `null`, метод вызывает `ShowAsync` без очереди.
Используйте `ShowQueuedAsync` для всех новых вызовов `ContentDialog`. `MessageDialog` — другой API, и он не использует эту очередь.

## Локализация

Приложение использует `WinUI3Localizer`. Строки находятся в `Strings/<language>/*.resw`. Языки — `en-us` и `ru`.

| Файл | Префикс в коде |
| ---- | -------------- |
| `Resources.resw` | Нет, например `GetLocalizedString("SomethingWentWrong")` |
| Другие файлы, например `Help.resw` | `/<file name>/`, например `GetLocalizedString("/Help/NotFoundTemplate")` |

В XAML используйте `l:Uids.Uid="Key"`. Ключ `Key.Text` или `Key.Content` задаёт свойство.

`App.InitializeLocalizer` читает `SYSTEM/language`. Если значение пустое, равно `NaN`, не является допустимым именем папки или не является папкой в `Strings`, метод использует культуру UI: `ru` даёт `ru`, другие культуры дают `en-us`.
`LocalizationHelper.SaveLanguageSettings` меняет язык и сохраняет `SYSTEM/language`.

Чтобы добавить строку:

1. Добавьте ключ в файл `.resw` в `Strings/en-us`.
2. Добавьте тот же ключ в тот же файл в `Strings/ru`.

Файлы `.resw` — это элементы `Content`, и они не компилируются в файл PRI (`PRIResource Remove` в `CDPIUI.csproj`). Если вы добавляете новый файл `.resw`, добавьте те же две записи в `CDPIUI.csproj`.

## Встроенная справка

Справка — это набор файлов Markdown в `Help/<EN|RU>/`. `CDPIUI.csproj` копирует все файлы через `<Content Include="Help\**">`. Новому файлу не нужна запись в проекте.
`OfflineHelpWindow` показывает файлы через `MarkdownTextBlock` (CommunityToolkit Labs).

### Язык

`OfflineHelpWindow` вызывает `HelpParser.GetHelpItemsForLanguage(StoreLocalizationHelper.GetStoreLikeLocale())`. `ru` даёт `RU`. Все остальные языки дают `EN`.
Для отдельной статьи запасного варианта нет. Если статьи нет в `RU`, русская справка её не показывает, а ссылка на неё показывает `/Help/NotFoundTemplate`. Если папки языка нет, меню пустое.

ВНИМАНИЕ: Добавляйте каждую статью в `Help/EN` и в `Help/RU` с одинаковым путём и именем файла.

### Структура меню

`HelpParser` читает папку языка один раз, когда окно открывается:

- Файл в корне папки языка — пункт меню верхнего уровня. При запуске выбран `WelcomeToHelp.md`.
- Вложенная папка — это категория. Файлы во вложенной папке — статьи категории. Более глубокие папки не читаются.
- Имя в меню — строка `/Help/<file or folder name without extension>` из `Help.resw`. Если строки нет, меню показывает имя файла или папки.
- Значок категории берётся из `HelpParser.HelpIconGlyphsPairs`. У категории без записи нет значка.
- `HelpParser` сортирует файлы и папки по имени (порядковое сравнение, без учёта регистра). Файлы корня идут перед категориями. Числовой префикс, например `1AboutConfigImport.md`, сохраняет статьи в правильном порядке.

Чтобы добавить статью:

1. Добавьте `Help/EN/<Category>/<Article>.md`.
2. Добавьте `Help/RU/<Category>/<Article>.md`.
3. Добавьте ключ `<Article>` в `Strings/en-us/Help.resw` и `Strings/ru/Help.resw`.
4. Для новой категории также добавьте ключ `<Category>` и значок в `HelpParser.HelpIconGlyphsPairs`.

Пример: у статьи `Help/EN/Utils/TroubleshootingUtility.md` есть ключ `TroubleshootingUtility` в обоих файлах `Help.resw`.

### Маршруты и ссылки

Маршрут справки — `cdpiui://Help/<Category>/<Article>/`. Имя статьи — имя файла без `.md`. Сравнение не чувствительно к регистру. `cdpiui://Help/<Category>/` выбирает категорию.
`OfflineHelpWindow.NavigateToPage` использует только первые два сегмента пути.

Окно сравнивает сегменты маршрута с именем файла или папки без `.md`. Поэтому статья из корня открывается по `cdpiui://Help/<Article>/`, например `cdpiui://Help/WelcomeToHelp/`. `cdpiui://Help/` открывает окно с `WelcomeToHelp.md`.

В статье ссылайтесь на другую статью через ссылку `cdpiui://Help/...`. Ссылки на другие публичные маршруты тоже возможны, например `cdpiui://Tools/ImportConfig`.
`MarkdownTextBlock_OnLinkClicked` передаёт ссылки `cdpiui://` в `CommandsHandler.HandleCommandAsync`. Поэтому на страницах справки работают только публичные маршруты.

Окно справки передаёт все остальные ссылки в `UrlOpenHelper.LaunchUrl`. Этот метод открывает только ссылки `http`, `https`, `mailto` и `tg`. Для других ссылок он пишет предупреждение в журнал.

Код открывает справку через `CommandsHandler.HandleCommand("cdpiui://Help/<Category>/<Article>/")`, через свойство `HelpUrl` у `UtilityButtonControls` или через `$INTERNAL_HELP(<Category>/<Article>)` в `Flashlight.resw`.

## ELUA

Условия для конечного пользователя находятся в `ELUA/EN/ELUA.md` и `ELUA/RU/ELUA.md`. `CDPIUI.csproj` копирует их как `Content`.
`WelcomeWindow.TryLoadLicense` загружает `ELUA/<EN|RU>/ELUA.md` из `Directories.ELUADirectory` (папка приложения). Язык берётся из `GetStoreLikeLocale`.
Если файл не загружается, флажок согласия отключён и показывается `/WelcomeWizard/UnableLoadLicense`. Когда пользователь принимает условия, `WELCOMEWIZARD/Shown` становится `true`.

## Настройки

`SettingsManager` (`CDPIUI.Core`) читает и записывает `Settings/Settings.xml` в `Directories.DataDirectory`. Папка данных — это папка приложения или `%LOCALAPPDATA%\CDPIUI`, если папка приложения защищена или приложение установлено через MSI (`State.IsApplicationBuildAsMsi`).
В файле есть группы и ключи: `GetValue<T>("GROUP", "key")`, `SetValue("GROUP", "key", value)` и `GetValueOrDefault`.

Значения по умолчанию находятся в `Template/Settings/Settings.xml`. Если ключа нет в пользовательском файле, `SettingsManager` читает значение по умолчанию из этого шаблона.

Чтобы добавить настройку:

1. Добавьте элемент `Setting` в нужный `Group` в `Template/Settings/Settings.xml`.
2. Читайте её через `SettingsManager.Instance.GetValue<T>`.

Часто используемые группы — `APPEARANCE`, `SYSTEM`, `NOTIFICATIONS`, `PSEUDOCONSOLE`, `CONFIGS` и `ZAPRET2`. `ADDONS/PRESETTEST/dpiSuiteUrl` заменяет адрес набора тестов DPI для проверки пресета. Значение должно быть абсолютным адресом `https`. Пустое значение означает встроенный адрес. Некоторых ключей, например `WELCOMEWIZARD/Shown`, нет в шаблоне. Читайте такие ключи через `GetValueOrDefault`.

## Структура папок

| Папка | Содержимое |
| ------ | -------- |
| `Assets` | Значки, изображения, значки файлов Магазина, превью |
| `Commands` | Сопоставление и обработка ссылок `cdpiui://` |
| `ConditionalLaunch` | Каталог UI для триггеров и действий условного запуска |
| `Controls` | Пользовательские элементы управления и диалоги (`Controls/Dialogs/<Area>`), `TemplatePage` |
| `Converters` | Преобразователи значений XAML |
| `ELUA` | Условия для конечного пользователя (`EN`, `RU`) |
| `Extensions` | Методы расширения |
| `Help` | Статьи встроенной справки (`EN`, `RU`) |
| `Helper` | Вспомогательные классы UI: окна, парсер справки, база данных, миграция, UI дополнений, `ContentDialogQueue` |
| `Properties` | `Resources.resx` и `launchSettings.json` |
| `Strings` | Строковые ресурсы `.resw` (`en-us`, `ru`) |
| `Template` | Настройки по умолчанию (`Template/Settings/Settings.xml`) |
| `ViewModels` | Модели представления и `RelayCommand` |
| `Views` | Страницы, по окнам |
| `Windows` | Окна (пространство имён `CDPIUI`), `Windows/Default/TemplateWindow.cs`, `Windows/Messages` (диалоговые окна), `Windows/Service` (`PrepareWindow`, `CriticalErrorHandlerWindow`) |

В корне находятся `App.xaml`, `App.xaml.cs`, `Program.cs`, `Package.appxmanifest`, `app.manifest` и `RemoveLanguageFolders.targets`. Фиды NuGet описаны в `nuget.config` в корне репозитория.

Проект `CDPIUI.Tests` не тестирует этот проект. См. `CDPIUI.Tests/README.ru.md`.
