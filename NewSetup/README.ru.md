[English](README.md) | **Русский**

# NewSetup

NewSetup — это проект WiX Toolset 6 (`WixToolset.Sdk/6.0.2`). Он собирает MSI для x64 с установкой для всех пользователей (per-machine).
Релиз содержит этот MSI под именем `Setup.msi`.

## Файлы

| Файл | Содержимое |
|------|---------|
| `NewSetup.wixproj` | SDK-проект только для x64. Содержит расширение WiX UI (`WixToolset.UI.wixext`), изображения диалогов и лицензию. Подавляет ICE64. |
| `Product.wxs` | Пакет, правило обновления, папка установки, её права доступа, значение реестра `InstallLocation` и пользовательские действия. |
| `NewSetupFiles.wxs` | `ComponentGroup NewSetupFiles`: один компонент WiX для каждого устанавливаемого файла. Также содержит ярлыки. |
| `UI.wxs` | Набор диалогов `WixUI_FeatureTree`. |
| `Resources/` | `Banner.jpg`, `Dialog.jpg`, `EULA.rtf` (страница лицензии), `Icon.ico` (значок в «Установка и удаление программ»). |

## Исходная папка

`NewSetupFiles.wxs` задаёт исходную папку:

```xml
<?define SourceDir="..\CDPIUI\bin\x64\Release\net8.0-windows10.0.22621.0\publish"?>
```

Эту папку создаёт `.github/scripts/build_portable.ps1`. Она содержит результат публикации GUI,
`CDPIUI_TrayIcon.exe`, `Update.exe` и `EditHostFile.exe`. Эти три инструмента — однофайловые
исполняемые файлы .NET 8. Своих файлов `.config` или `.dll` в папке у них нет.

ВНИМАНИЕ: Запустите `build_portable.ps1` с конфигурацией по умолчанию `Release` до сборки MSI.
Исходный путь содержит `Release`.

## Правило для списка файлов

У каждого файла в папке publish должен быть свой компонент WiX в `NewSetupFiles.wxs`.
Единственное исключение — файлы с расширением `.pdb`. Компонент должен использовать
`Source="$(var.SourceDir)\<relative path>"`.

WiX завершается с ошибкой, если указанного в списке файла нет. Но WiX не находит опубликованный файл, которого
нет в списке. Тогда MSI не содержит этот файл, и WiX не показывает ошибку.

`.github/scripts/check_setup_files.ps1` сравнивает два списка без учёта регистра. Скрипт завершается с ошибкой,
если файла нет в одном из списков. CI и релизный workflow запускают его перед сборкой MSI.

Когда результат публикации изменяется, выполните следующие шаги:

1. Запустите `build_portable.ps1`.
2. Добавьте или удалите соответствующие компоненты WiX в `NewSetupFiles.wxs`.
3. Запустите `check_setup_files.ps1`.

`Product.wxs` использует два фиксированных ID файлов:

| ID | Файл | Назначение |
|----|------|-----|
| `CdpiUiExe` | `CDPIUI.exe` | Версия пакета. Ярлыки в меню «Пуск» и на рабочем столе. |
| `TrayIconExe` | `CDPIUI_TrayIcon.exe` | Пользовательское действие `UninstallCleanup`. |

## Пакет и обновления

- `Version` пакета — это версия файла `CDPIUI.exe` (`!(bind.FileVersion.CdpiUiExe)`).
- `NewSetup.wixproj` не задаёт свойство версии. Версию пакета задаёт только версия файла `CDPIUI.exe`.
- `UpgradeCode` равен `{AB14590E-0D10-416F-9A0E-07E63074B603}`.
- Каждая сборка получает новый код продукта. Поэтому каждая установка — это крупное обновление (major upgrade) предыдущей.
- `MajorUpgrade` запрещает установку более старой версии с сообщением «A newer version of [ProductName] is already installed.»
- `MajorUpgrade` задаёт `AllowSameVersionUpgrades="yes"`. Windows Installer игнорирует четвёртое поле версии. С этой настройкой две сборки, которые различаются только этим полем, могут заменять друг друга.
- MSI содержит файл cabinet. `MSIUSEREALADMINDETECTION` равен `1`.

## Папка установки и права доступа

Папка по умолчанию — `ProgramFiles6432Folder\Storik4\CDPI UI`. Пользователь может изменить папку в
дереве функций (`ConfigurableDirectory="INSTALLLOCATION"`).

Трей, работающий с правами администратора, запускает файлы из этой папки. Поэтому компонент `InstallLocationPermissions` задаёт
для папки явный защищённый список управления доступом:

```
D:PAI(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)(A;OICI;0x1200a9;;;BU)
```

| Учётная запись | Права |
|---------|--------|
| SYSTEM | Полный доступ |
| Администраторы | Полный доступ |
| Пользователи | Чтение и выполнение |

Папка не наследует записи от родительской папки. Поэтому пользовательская папка, например `C:\CDPI UI`,
не даёт право записи всем пользователям.

## Расположение установки в реестре

Компонент `InstallLocationRegistration` записывает папку установки в реестр:

| Ключ | Имя значения | Данные |
|-----|------------|------|
| `HKLM\Software\Storik4\CDPI UI` | `InstallLocation` | `[INSTALLLOCATION]` |

Релизный workflow упаковывает одну и ту же сборку `Release` и в MSI, и в портативный ZIP. GUI
(`CDPIUI.Core/State.cs`) и трей (`CDPIUI.TrayIcon/Helper/Utils.cs`) сравнивают это значение со
своей папкой. Если папки совпадают, приложение работает как установка из MSI.

ВНИМАНИЕ: Не удаляйте компонент `InstallLocationRegistration`. Без него установка из MSI
работает как портативная установка и скачивает обновления-патчи.

## Пользовательские действия

| Действие | Последовательность и условие | Функция |
|--------|------------------------|----------|
| `LaunchApp` | После `InstallFinalize`. Условие: `NOT Installed AND NOT REMOVE AND UILevel >= 4`. | Выполняет `[WindowsFolder]explorer.exe "[INSTALLLOCATION]CDPIUI.exe"` с `asyncNoWait`. |
| `UninstallCleanup` | Перед `RemoveFiles`. Условие: `REMOVE="ALL" AND NOT UPGRADINGPRODUCTCODE`. | Выполняет `CDPIUI_TrayIcon.exe --uninstall-cleanup`. Отложенное, без олицетворения (выполняется от имени SYSTEM), `Return="ignore"`. |

Замечания о `LaunchApp`:

- Проводник запускает GUI. Поэтому GUI работает от имени вошедшего пользователя, а не с правами установщика.
- `UILevel >= 4` означает сокращённый или полный интерфейс. Тихая установка (`/qn`) или установка с базовым интерфейсом (`/qb`) не запускает GUI.

Замечания о `UninstallCleanup`:

- Действие не выполняется, когда крупное обновление удаляет старый продукт.
- `--uninstall-cleanup` (`CDPIUI.TrayIcon/Helper/UninstallHelper.cs`) выполняет следующие шаги:

1. Устанавливает событие `Global\CDPIUI_TrayShutdown`.
2. Ждёт не более 20 с, пока работающие треи завершатся. Треи останавливают компоненты и восстанавливают настройки прокси.
3. Принудительно завершает оставшиеся процессы, которые работают из папки установки или из пользовательской папки `\AppData\Local\CDPIUI\` (`ApplicationPathMatcher`).
4. Останавливает и удаляет каждую службу `WinDivert*`, образ которой находится в одной из этих папок.
5. Удаляет задачу автозапуска.
6. Выполняет `EditHostFile.exe /remove`.
7. Удаляет файлы времени выполнения, о которых MSI не знает: `Logs`, `.update-staging`, `.update-backup`, `.old`, `.staging`, `.trust`, `.session`, `update.log`, `.update-manifest`.

## Сборка

Выполните эти команды из корня репозитория:

```powershell
pwsh .github/scripts/build_portable.ps1
pwsh .github/scripts/check_setup_files.ps1
dotnet build NewSetup/NewSetup.wixproj -c Release -p:Platform=x64
```

MSI помещается в `NewSetup/bin/`. Его выходное имя — `NewSetup`. Релизный workflow копирует его
как `Setup.msi` и подписывает его сертификатом каталогов. Трей устанавливает обновление MSI только
с этой подписью (`PackageStaging.HasPinnedSignature`). Локальная сборка не подписана.
