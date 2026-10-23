# CDPIUI

`CDPIUI` is the WinUI 3 GUI project (.NET 8, Windows App SDK). It shows the windows of the application.
The tray process `CDPIUI_TrayIcon.exe` keeps the components in operation. The GUI connects to the tray through a named pipe.
The logic that does not need the UI is in `CDPIUI.Core`, `CDPIUI.Shared` and `CDPIUI.AddOns`.

## Startup

### Program.cs

`Program.Main` is the entry point. The XAML compiler does not add its own `Main` method to this project.

1. `Main` calls `DecideRedirection`.
2. `DecideRedirection` registers the key `CDPI_GUIApp` with `AppInstance.FindOrRegisterForKey`.
3. If the current process owns the key, it keeps the activation arguments in `InitialActivationArguments`. Then it starts `App`.
4. If a different process owns the key, `RedirectActivationTo` sends the activation to that process. The wait limit is 30 seconds.
5. If the running instance does not accept the activation, the process tries to register the key again. This occurs when the running instance closes.

The running instance receives a redirected activation in `OnActivated`. Activations that arrive before the UI thread is ready go into a queue.
After `App` is created, `DispatchPendingActivations` sends them to `App.PreferRequestedActions` on the UI thread.

`Program` also finds the GoodbyeDPI migration argument (`GoodbyeDpiMigrationActivation`). While a migration is pending, the application does not open the usual startup window.

### Tray start and `--gui-args:`

At startup, `PipeClientService` connects to the tray pipe. If it cannot connect, it starts `CDPIUI_TrayIcon.exe` as administrator. Then the GUI process stops.

- The GUI gives `--autorun` to the tray if the setting `APPEARANCE/hideToTrayOnStartup` is true. Else it gives `--show-ui`.
- The GUI encodes its own arguments with `GuiActivationArguments.Encode` (`CDPIUI.Shared/Basic/CommandLine`). The result is one `--gui-args:` argument.
- Each argument is UTF-8 and Base64. A dot separates the arguments. The maximum encoded length is 16 KiB. A longer value is not sent.
- The tray decodes the value with `GuiActivationArguments.TryDecode`. It starts `CDPIUI.exe` again with the original arguments.

Thus an opened file, a `cdpiui://` link or a `--direct:` command is not lost when the tray is not running.

### `--direct:` and other arguments

The tray starts the GUI with `--direct:<pipe message>` when the GUI is not connected. This occurs for all message targets, for example `ShowWindow` messages.
For background actions, the tray also adds `--create-no-window`. The tray does not use `----ms-protocol:`.

`App.PipeConnectedActions` processes the arguments after the pipe connects:

| Argument or activation | Handler |
| ---------------------- | ------- |
| GoodbyeDPI migration argument | `TryHandleMigrationActivationAsync`, opens `WelcomeWindow` with `WelcomeWindow.MigrationWindowId` |
| File activation or file path | `ProcessFiles` |
| Protocol activation (`cdpiui://`) | `CommandsHandler.HandleCommandAsync` (public routes only) |
| `----ms-protocol:<uri>` | `CommandsHandler.HandleCommandAsync` (public routes only) |
| `--direct:<message>` | `CoreCommandsHandler.HandleCommandAsync`, then `CommandsHandler.HandleCommand` (pipe messages accepted) |

`ProcessFiles` accepts `.cdpisignedpack`, `.cdpiconfigpack`, `.cdpipatch`, `.cdpitask` and `.cdpiconfig`. It processes only the first supported file.

If no argument opens a window, `OpenStartupWindowAsync` opens `WelcomeWindow` or `ModernMainWindow`. `WelcomeWindow` opens until the setting `WELCOMEWIZARD/Shown` is true.

A redirected activation goes to `App.PreferRequestedActions`. It processes protocol links, files, the migration argument and `--direct:` in the same way.

### App.xaml.cs

The `App` constructor does these steps:

1. It registers handlers for unhandled exceptions. A XAML exception is logged and shown in `ErrorContentDialog`. The application does not close.
2. It connects `PipeClientService.MessageReceived` to `CommandsHandler.HandleCommand`.
3. It applies the theme from `APPEARANCE/Theme`.
4. It starts `PipeClientService` and subscribes to `Connected`.
5. It calls `GetReadyFeatures`.

`GetReadyFeatures` starts `GetReadyFeaturesAsync` and keeps the task in `_localizerReady`. The task restores the database (`DatabaseInitializationService.QuickRestore`), builds the localizer and registers the file associations and the `cdpiui` protocol (`FileAssociationService`).

`OnLaunched` creates `PrepareWindow` and calls `PipeClientService.Start`. `PrepareWindow` closes after the startup actions. `App` also creates it when a Store operation runs and no other window is open.

The application closes in `ExitIfIdle` when no window is open and no Store or GoodCheck operation runs.

### Localizer readiness

The windows read their strings from the localizer in their constructors. Thus a window must not be created before the localizer is built.
All overloads of `SafeCreateNewWindow` and `UnsafeCreateNewWindow` wait for `_localizerReady` before they create a window.

CAUTION: Do not create a window with `new` during startup. Use the `App` window methods, because they wait for the localizer.

## Windows

### Namespace rule

All windows are in the `CDPIUI` namespace, not in `CDPIUI.Windows`. The files are in the `Windows` folder.
`WindowOpenHelper` finds a window type by the name `CDPIUI.<windowName>`. A window in a different namespace cannot open from a link.
The dialog windows in `Windows/Messages` that use `CDPIUI.Messages` cannot open from a link. `TemplateWindow` is in `CDPIUI.Default`.

### TemplateWindow

Most windows derive from `TemplateWindow` (`Windows/Default/TemplateWindow.cs`), which derives from `WinUIEx.WindowEx`. The legacy `MainWindow` derives directly from `WindowEx`.
`TemplateWindow` gives these functions:

- The custom title bar (`CustomTitleBarUserControl`, `TitleBarMode` from `APPEARANCE/titleBarMode`).
- `WindowTitle`, `IconUri` and `WindowMinSize`.
- `Id`: an identifier for windows that can have more than one instance.
- `MainFrame` and `NavigateToPageWithParameter`, which sends a `NameValueCollection` to the page.
- `NavigateBackWithParameter` and `RemoveAndGoBackTo` for back navigation.

### Open a window

`App.OpenWindows` contains all open windows. `RegisterWindow` adds a window to the list, applies the theme and subscribes to `Closed`.
When a window closes, `App` saves its size and position (`WindowsPositionHelper`) and removes it from the list.

| Method | Use |
| ------ | --- |
| `SafeCreateNewWindow<TWindow>()` | One instance of a window type. If one instance is open, the method activates it. If more instances are open, the method closes them and makes a new window. |
| `SafeCreateNewWindow(Type)` | The same, for a type from a link. |
| `UnsafeCreateNewWindow<TWindow>(activate, id)` | More instances of one type. A window with the same `Id` is activated. Else a new window is created. |
| `UnsafeCreateNewWindow(Type, activate, id)` | The same, for a type from a link. |
| `ShowWindowModalAsync` / `MakeWindowNormal` | Disable the owner windows while a window is modal, then enable them again. |
| `CloseWindow<TWindow>()` / `CloseWindow<TWindow>(id)` | Close all windows of a type, or one window with an `Id`. |

Do not create a window with `new` and `Activate` directly. Use the `App` methods, else `OpenWindows`, the theme and `ExitIfIdle` do not include the window.

### WindowOpenHelper

`Helper/WindowHelper/WindowOpenHelper.OpenAsync` opens a window from a `ShowWindow` message. The message parameters are `windowName`, `page`, `id` and page parameters.

- `MainWindow` changes to `ModernMainWindow`. `LegacyMainWindow` changes to `MainWindow`.
- If `WelcomeWindow` is open or the welcome wizard was not completed, a request for the main window opens the startup window.
- The type must be in the application assembly and in the `CDPIUI` namespace. It must derive from `Window`, must not be abstract and must have a public constructor without parameters.
- With `id`, the helper uses `UnsafeCreateNewWindow`. Without `id`, it uses `SafeCreateNewWindow`.
- For `OfflineHelpWindow`, the `helpUrl` parameter goes to `NavigateToPage`.
- Only a `TemplateWindow` can receive a page. The helper finds the page type with `GetPageType`.

`GetPageType` tries to find `CDPIUI.Views.<window name without "Window">.<page>`. For `ModernMainWindow`, it uses `CDPIUI.Views.Main.<page>` and `CDPIUI.Views.<page>`. `HomePage` gives `ModernMainWindow.GetMainPage()`.
For `StoreWindow`, `HomePage` is `CDPIUI.HomePage`. A page name can contain a sub-namespace, for example `Settings.Memory.MemoryViewLogsDetailsPage`.

## The `cdpiui://` link scheme

The files are in `Commands`:

- `CommandUriMapper` converts a link to an `IPipeMessage`.
- `CommandsHandler` runs the message.
- `ExternalUriLauncher` opens web links in XAML with `UrlOpenHelper`.

The host and path segments are not case-sensitive. A `{name}` segment is a parameter. A `{*name}` segment takes the rest of the path.

### Public routes

| Link | Result |
| ---- | ------ |
| `cdpiui://AppFeatures` | `AppFeaturesWindow` |
| `cdpiui://Welcome` | `WelcomeWindow` |
| `cdpiui://Main` | Main window |
| `cdpiui://Main/Home` | Main window, main page |
| `cdpiui://Main/Utilities` | Main window, `UtilsPage` |
| `cdpiui://Main/Settings` | Main window, `SettingsPage` |
| `cdpiui://Main/Settings/Autorun` | Main window, `Settings.AutorunPage` |
| `cdpiui://Main/Settings/Personalization` | Main window, `Settings.PersonalizePage` |
| `cdpiui://Main/About` | Main window, `AboutPage` |
| `cdpiui://Main/Updates` | `AboutPage` with `isUpdateRequested=True` |
| `cdpiui://Main/Components/{componentId}` | `Components.ViewComponentSettingsPage` with `componentId` |
| `cdpiui://Store` | `StoreWindow` |
| `cdpiui://Store/Home` | `StoreWindow`, `HomePage` |
| `cdpiui://Store/Catalog/{itemId}` | `ItemViewPage` with `itemId` and `setFocus=ItemActionButton` |
| `cdpiui://Store/Category/{categoryId}` | `CategoryViewPage` with `categoryId` |
| `cdpiui://Store/Downloads` | `DownloadsPage` |
| `cdpiui://Store/Updates` | `DownloadsPage` with `isUpdateRequested=True` |
| `cdpiui://Store/Library` | `LibraryPage` |
| `cdpiui://Store/Settings` | `SettingsPage` |
| `cdpiui://Store/Settings/Memory` | `Settings.MemoryViewPage` |
| `cdpiui://Store/Settings/Memory/Application` | `MemoryViewApplicationFilesDetailsPage` |
| `cdpiui://Store/Settings/Memory/InstalledItems` | `MemoryViewInstalledItemsDetailsPage` |
| `cdpiui://Store/Settings/Memory/Logs` | `MemoryViewLogsDetailsPage` |
| `cdpiui://Store/Settings/Memory/Settings` | `MemoryViewSettingsDetailsPage` |
| `cdpiui://Store/Settings/Memory/StoreCache` | `MemoryViewStoreCachePage` |
| `cdpiui://Store/Settings/Memory/ConditionalLaunch` | `MemoryViewConditionalLaunchDetailsPage` |
| `cdpiui://Tools/Console/{componentId}` | `ViewWindow` with `id` |
| `cdpiui://Tools/Service` | `ServiceWindow` |
| `cdpiui://Tools/ConditionalLaunch` | `ConditionalLaunchWindow` |
| `cdpiui://Tools/AutoConfig` | `CreateConfigUtilWindow` |
| `cdpiui://Tools/AutoConfig/Zapret2` | `CreateConfigUtilWindow`, `MainPage`, Zapret2, `action=BlockCheck2` |
| `cdpiui://Tools/AutoConfig/Zapret2/Reports` | The same, `action=BlockCheck2Reports` |
| `cdpiui://Tools/AutoConfig/{componentId}` | `CreateConfigUtilWindow`, `MainPage` with `componentId` |
| `cdpiui://Tools/ConfigEditor` | `ConfigMakerWindow` |
| `cdpiui://Tools/ConfigEditorLegacy` | `CreateConfigHelperWindow` |
| `cdpiui://Tools/ImportConfig` | `ConfigImportUtilWindow` |
| `cdpiui://Tools/ImportConfig/{componentId}` | `ConfigImportUtilWindow`, `MainPage` with `componentId` |
| `cdpiui://Tools/CreateConfig/{componentId}` | `ConfigMakerWindow` with `id` |
| `cdpiui://Tools/EditConfigKit/{kitId}` | `CreateConfigHelperWindow`, `EditConfigKitPage` with `kitId` |
| `cdpiui://Tools/Help` | `OfflineHelpWindow` |
| `cdpiui://Tools/Proxy` | `ProxySetupUtilWindow` |
| `cdpiui://Tools/Troubleshooting` | `TroubleshootingWindow` |
| `cdpiui://Tools/Troubleshooting/BasicCheck` | `WorkPage`, `action=BeginBasicCheck` |
| `cdpiui://Tools/Troubleshooting/StoreCheck` | `WorkPage`, `action=BeginStoreRepoCheck` |
| `cdpiui://Tools/PresetTest` | `ConfigTestWindow` |
| `cdpiui://Tools/Hosts` | `EditHostFileWindow` |
| `cdpiui://Help` | `OfflineHelpWindow` |
| `cdpiui://Help/{*helpUrl}` | `OfflineHelpWindow` with `helpUrl=/<path>` |
| `cdpiui://Actions/CheckForUpdates` | Application update check with a notification |
| `cdpiui://Actions/CompatibilityCheck` | Start of the compatibility check |

The order of the routes is important. The first route that matches is used. For example, `Tools/AutoConfig/Zapret2` is before `Tools/AutoConfig/{componentId}`.

### Pipe messages are not accepted from links

`CommandUriMapper.ConvertBack` has the parameter `allowPipeMessages`. Its default value is `false`.

- If a host has routes and no route matches, the result is `null`.
- If the host has no routes, the link is converted with `PipeModelConvertor` only when `allowPipeMessages` is `true`.

`CommandsHandler.HandleCommandAsync(string)` is for links from outside the application: protocol activation, `----ms-protocol:`, the in-app help. It accepts only public routes.
`CommandsHandler.HandleCommand(string)` is for links that the application makes itself, for example `CommandParameter` in XAML. It also accepts pipe messages.

WARNING: Do not send a link from a browser, a file or a help page to `HandleCommand(string)`. A web page could then forge tray messages.

`CommandsHandler` runs `PresentationMessageModel` (`ShowWindow` only), `GoodCheckMessageModel`, `UtilsMessageModel`, `UpdateMessageModel` and `CompatibilityCheckMessageModel`. It ignores other message types.

To bind a link in XAML, use `Command="{x:Bind commands:CommandsHandler.Instance.Command}"` and put the link in `CommandParameter`.

### Add a route

1. Add a `CommandRoute` to `CommandUriMapper.Routes`. For a window, use `CreateWindowRoute`.
2. Put a specific route before a route with a parameter.
3. Make sure that the window is in the `CDPIUI` namespace.
4. Make sure that the page is in `CDPIUI.Views.<window prefix>`.

## Pages and views

The pages are in `Views`. Most pages derive from `TemplatePage` (`Controls/Default/TemplatePage.cs`).

| Folder | Window |
| ------ | ------ |
| `Views` (root) | Main window pages: `MainPage`, `ModernMainPage`, `UtilsPage`, `SettingsPage`, `AboutPage` |
| `Views/Settings` | Main window settings pages |
| `Views/Main/Components` | Component settings pages |
| `Views/Store`, `Views/Store/Settings`, `Views/Store/Settings/Memory` | `StoreWindow` |
| `Views/CreateConfigUtil` | `CreateConfigUtilWindow` |
| `Views/BlockCheck2` | Zapret2 preset selection |
| `Views/ConfigImportUtil` | `ConfigImportUtilWindow` |
| `Views/CreateConfigHelper` | `CreateConfigHelperWindow` (legacy preset editor) |
| `Views/EditHostsFile` | `EditHostFileWindow` |
| `Views/SetupProxy` | `ProxySetupUtilWindow` |
| `Views/Troubleshooting` | `TroubleshootingWindow` |

The namespace of a page agrees with its folder, with one exception: `Views/Store/HomePage` is in `CDPIUI`.
The pages get parameters as a `NameValueCollection` in `OnNavigatedTo`.

## ContentDialogQueue

A window can show only one `ContentDialog` at a time. A second `ShowAsync` call causes an exception.
`ContentDialogQueue.ShowQueuedAsync` (`Helper/Basic/ContentDialogQueue.cs`) is an extension method. It waits for the previous dialogs in the same `XamlRoot`.

```csharp
dialog.XamlRoot = this.XamlRoot;
var result = await dialog.ShowQueuedAsync();
```

Set `XamlRoot` before the call. If `XamlRoot` is `null`, the method calls `ShowAsync` without the queue.
Use `ShowQueuedAsync` for all new `ContentDialog` calls. `MessageDialog` is a different API and does not use this queue.

## Localization

The application uses `WinUI3Localizer`. The strings are in `Strings/<language>/*.resw`. The languages are `en-us` and `ru`.

| File | Prefix in code |
| ---- | -------------- |
| `Resources.resw` | None, for example `GetLocalizedString("SomethingWentWrong")` |
| Other files, for example `Help.resw` | `/<file name>/`, for example `GetLocalizedString("/Help/NotFoundTemplate")` |

In XAML, use `l:Uids.Uid="Key"`. The key `Key.Text` or `Key.Content` sets the property.

`App.InitializeLocalizer` reads `SYSTEM/language`. If the value is empty, `NaN`, not a valid folder name or not a folder in `Strings`, it uses the UI culture: `ru` gives `ru`, other cultures give `en-us`.
`LocalizationHelper.SaveLanguageSettings` changes the language and saves `SYSTEM/language`.

To add a string:

1. Add the key to the `.resw` file in `Strings/en-us`.
2. Add the same key to the same file in `Strings/ru`.

The `.resw` files are `Content` items and are not compiled into the PRI file (`PRIResource Remove` in `CDPIUI.csproj`). If you add a new `.resw` file, add the same two entries in `CDPIUI.csproj`.

## In-app help

The help is a set of Markdown files in `Help/<EN|RU>/`. `CDPIUI.csproj` copies all files with `<Content Include="Help\**">`. A new file does not need a project entry.
`OfflineHelpWindow` shows the files with `MarkdownTextBlock` (CommunityToolkit Labs).

### Language

`OfflineHelpWindow` calls `HelpParser.GetHelpItemsForLanguage(StoreLocalizationHelper.GetStoreLikeLocale())`. `ru` gives `RU`. All other languages give `EN`.
There is no fallback for one article. If an article is missing in `RU`, the Russian help does not show it, and a link to it shows `/Help/NotFoundTemplate`. If the language folder is missing, the menu is empty.

CAUTION: Add each article to `Help/EN` and to `Help/RU` with the same path and file name.

### Menu structure

`HelpParser` reads the language folder once, when the window opens:

- A file in the root of the language folder is a top-level menu item. `WelcomeToHelp.md` is selected at the start.
- A subfolder is a category. The files in the subfolder are the articles of the category. Deeper folders are not read.
- The menu name is the `/Help/<file or folder name without extension>` string from `Help.resw`. If the string is missing, the menu shows the file or folder name.
- The category icon comes from `HelpParser.HelpIconGlyphsPairs`. A category without an entry has no icon.
- `HelpParser` sorts the files and the folders by name (ordinal, not case-sensitive). The root files come before the categories. A number prefix, for example `1AboutConfigImport.md`, keeps the articles in the correct sequence.

To add an article:

1. Add `Help/EN/<Category>/<Article>.md`.
2. Add `Help/RU/<Category>/<Article>.md`.
3. Add the `<Article>` key to `Strings/en-us/Help.resw` and `Strings/ru/Help.resw`.
4. For a new category, also add the `<Category>` key and an icon in `HelpParser.HelpIconGlyphsPairs`.

Example: the article `Help/EN/Utils/TroubleshootingUtility.md` has the key `TroubleshootingUtility` in both `Help.resw` files.

### Routes and links

The help route is `cdpiui://Help/<Category>/<Article>/`. The article name is the file name without `.md`. The comparison is not case-sensitive. `cdpiui://Help/<Category>/` selects the category.
`OfflineHelpWindow.NavigateToPage` uses only the first two path segments.

The window compares the route segments with the file or folder name without `.md`. Thus a root article opens with `cdpiui://Help/<Article>/`, for example `cdpiui://Help/WelcomeToHelp/`. `cdpiui://Help/` opens the window with `WelcomeToHelp.md`.

In an article, link to a different article with a `cdpiui://Help/...` link. Links to other public routes are also possible, for example `cdpiui://Tools/ImportConfig`.
`MarkdownTextBlock_OnLinkClicked` sends `cdpiui://` links to `CommandsHandler.HandleCommandAsync`. Thus only public routes operate in help pages.

The help window sends all other links to `UrlOpenHelper.LaunchUrl`. This method opens only `http`, `https`, `mailto` and `tg` links. It writes a warning to the log for other links.

Code opens help with `CommandsHandler.HandleCommand("cdpiui://Help/<Category>/<Article>/")`, with the `HelpUrl` property of `UtilityButtonControls`, or with `$INTERNAL_HELP(<Category>/<Article>)` in `Flashlight.resw`.

## ELUA

The end user terms are in `ELUA/EN/ELUA.md` and `ELUA/RU/ELUA.md`. `CDPIUI.csproj` copies them as `Content`.
`WelcomeWindow.TryLoadLicense` loads `ELUA/<EN|RU>/ELUA.md` from `Directories.ELUADirectory` (application directory). The language comes from `GetStoreLikeLocale`.
If the file cannot load, the agreement check box is disabled and `/WelcomeWizard/UnableLoadLicense` shows. When the user accepts, `WELCOMEWIZARD/Shown` becomes `true`.

## Settings

`SettingsManager` (`CDPIUI.Core`) reads and writes `Settings/Settings.xml` in `Directories.DataDirectory`. The data directory is the application directory, or `%LOCALAPPDATA%\CDPIUI` if the application directory is protected or the application has an MSI installation (`State.IsApplicationBuildAsMsi`).
The file has groups and keys: `GetValue<T>("GROUP", "key")`, `SetValue("GROUP", "key", value)` and `GetValueOrDefault`.

The default values are in `Template/Settings/Settings.xml`. If a key is missing in the user file, `SettingsManager` reads the default value from this template.

To add a setting:

1. Add the `Setting` element to the correct `Group` in `Template/Settings/Settings.xml`.
2. Read it with `SettingsManager.Instance.GetValue<T>`.

Frequent groups are `APPEARANCE`, `SYSTEM`, `NOTIFICATIONS`, `PSEUDOCONSOLE`, `CONFIGS` and `ZAPRET2`. Some keys, for example `WELCOMEWIZARD/Shown`, are not in the template. Read these keys with `GetValueOrDefault`.

## Folder structure

| Folder | Contents |
| ------ | -------- |
| `Assets` | Icons, images, Store file icons, previews |
| `Commands` | `cdpiui://` link mapping and handling |
| `ConditionalLaunch` | UI catalog of Conditional Launch triggers and actions |
| `Controls` | User controls and dialogs (`Controls/Dialogs/<Area>`), `TemplatePage` |
| `Converters` | XAML value converters |
| `ELUA` | End user terms (`EN`, `RU`) |
| `Extensions` | Extension methods |
| `Help` | In-app help articles (`EN`, `RU`) |
| `Helper` | UI helpers: windows, help parser, database, migration, add-on UI, `ContentDialogQueue` |
| `Strings` | `.resw` string resources (`en-us`, `ru`) |
| `Template` | Default settings (`Template/Settings/Settings.xml`) |
| `ViewModels` | View models and `RelayCommand` |
| `Views` | Pages, by window |
| `Windows` | Windows (`CDPIUI` namespace), `Windows/Default/TemplateWindow.cs`, `Windows/Messages` (dialog windows), `Windows/Service` (`PrepareWindow`, `CriticalErrorHandlerWindow`) |

The root contains `App.xaml`, `App.xaml.cs`, `Program.cs`, `Package.appxmanifest`, `app.manifest` and `RemoveLanguageFolders.targets`.

The `CDPIUI.Tests` project does not test this project. See `CDPIUI.Tests/README.md`.
