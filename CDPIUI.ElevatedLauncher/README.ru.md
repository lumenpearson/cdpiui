[English](README.md) | **Русский**

# CDPIUI.ElevatedLauncher

`CDPIUI_ElevatedLauncher.exe` запускает программы CDPI UI с правами администратора: трей (`CDPIUI_TrayIcon.exe`)
и редактор файла hosts (`EditHostFile.exe`). Это программа Native AOT для `win-x64`. Её манифест
требует права администратора (`app.manifest`). Поэтому запрос UAC показывает программу запуска, а
программа, которую она запускает, получает её токен.

## Зачем нужна программа запуска

UAC создаёт среду процесса с правами администратора из профиля пользователя. Поэтому на процесс
действуют переменные пользователя (`HKCU\Environment`), а их может изменить любой процесс пользователя.
Трей и `EditHostFile.exe` — программы .NET, зависящие от платформы (framework-dependent). Узел .NET и
CoreCLR читают некоторые переменные. Например, `DOTNET_ROOT` выбирает среду выполнения,
`CORECLR_PROFILER_PATH` загружает DLL профилировщика, а `DOTNET_STARTUP_HOOKS` загружает сборку. Тогда
запущенная программа выполнила бы код пользователя с правами администратора.

У программы запуска нет узла .NET и нет CLR. Поэтому она не читает переменные среды выполнения,
профилировщика и startup hooks. Она удаляет их перед запуском программы.

## Аргументы

```
CDPIUI_ElevatedLauncher.exe <target> [arguments of the target]
```

| Цель | Программа | Программа запуска ждёт завершения |
|---|---|---|
| `tray` | `CDPIUI_TrayIcon.exe` | Нет. Она завершается с кодом `0` после запуска. |
| `hosts` | `EditHostFile.exe` | Да. Она завершается с кодом выхода `EditHostFile.exe`. |

- Имя цели должно совпадать с именем в таблице. Сравнение учитывает регистр.
- Программа находится в папке программы запуска. Программа запуска никогда не берёт путь из аргументов.
- Программа запуска передаёт остальные аргументы программе без изменений. Она добавляет каждый аргумент в `ProcessStartInfo.ArgumentList`, поэтому программа получает те же аргументы.
- Рабочая папка программы — папка программы запуска.
- Программа запуска запускает программу с `UseShellExecute = false`. Программа получает токен программы запуска и не показывает второй запрос UAC.

## Среда

Программа запуска копирует свою среду в программу. Она удаляет каждую переменную, имя которой
начинается с одного из этих префиксов, без учёта регистра:

- `DOTNET_`
- `COREHOST_`
- `CORECLR_`
- `COR_`
- `COMPlus_`

Префиксы включают `DOTNET_ROOT`, `DOTNET_ROOT(x86)` и `DOTNET_ROOT_X64`. `LaunchPolicy` также
перечисляет эти три переменные, поэтому изменение префиксов не может их оставить.

Все остальные переменные программа запуска сохраняет, например `PATH` и `TEMP`.

## Коды выхода

| Код | Значение |
|---|---|
| `0` | Трей запущен. |
| Код выхода `EditHostFile.exe` | Цель `hosts`. См. [EditHostFile/README.ru.md](../EditHostFile/README.ru.md#коды-выхода). |
| `0x80070057` (`E_INVALIDARG`) | Нет цели или цели нет в таблице. |
| `0x80070002` | Программы нет в папке программы запуска. |
| `0x8007xxxx` | Windows не запустила программу. Младшие 16 бит — ошибка Win32. |
| `0x80004005` (`E_FAIL`) | Другая ошибка. |

GUI преобразует коды цели `hosts` в ошибки с префиксом `HOSTEDIT`.

## Вызывающий код

| Вызывающий код | Команда |
|---|---|
| GUI, когда он не может подключиться к трею (`CDPIUI.Core/Communication/PipeClientService.cs`) | `tray --autorun` или `tray --show-ui [...]`, глагол `runas` |
| Задача автозапуска `CDPIUI_Autostart` (`CDPIUI.TrayIcon/Helper/Settings/AutoStartManager.cs`) | `tray --autorun` |
| Редактор файла hosts (`CDPIUI.AddOns/HostsFileEditor/HostsFileEditorService.cs`) | `hosts /add`, `hosts /remove` или `hosts /recover`, глагол `runas` |

Вызывающий код берёт имя файла и имена целей из `ElevatedLauncherCommand`
(`CDPIUI.Shared/Security/ElevatedLauncherCommand.cs`).

Процессы, которые запускает трей (компоненты, GoodCheck, `Update.exe`), получают среду трея.
Программа запуска удалила из этой среды переменные среды выполнения. См.
[CDPIUI.TrayIcon/README.ru.md](../CDPIUI.TrayIcon/README.ru.md#environment-of-an-elevated-start).

## Файлы

| Файл | Содержимое |
|---|---|
| `Program.cs` | Точка входа: находит цель, удаляет переменные, запускает программу |
| `LaunchPolicy.cs` | Таблица целей и фильтр переменных |
| `app.manifest` | `requireAdministrator` |

## Сборка

`CDPIUI.ElevatedLauncher.csproj` задаёт `OutputType` `WinExe` (без окна консоли), `net8.0`, `win-x64`,
`PublishAot`, `InvariantGlobalization` и `StartupHookSupport` `false`.

Чтобы проверить код, соберите проект. Сборка запускает анализаторы обрезки (trim) и AOT:

```
dotnet build CDPIUI.ElevatedLauncher/CDPIUI.ElevatedLauncher.csproj -c Release -p:Platform=x64
```

Чтобы получить машинный файл, опубликуйте проект в Windows. Для Native AOT нужны средства сборки C++
из Visual Studio (рабочая нагрузка «Разработка классических приложений на C++»):

```
dotnet publish CDPIUI.ElevatedLauncher/CDPIUI.ElevatedLauncher.csproj -c Release -p:Platform=x64 -r win-x64 -o artifacts/tools/launcher
```

`.github/scripts/build_portable.ps1` использует ту же команду, проверяет, что файл не является сборкой
.NET, и копирует его в макет релиза. Задание `tools` в CI публикует его с `TreatWarningsAsErrors`,
поэтому предупреждение обрезки или AOT завершает задание с ошибкой.

В Linux и macOS шаг компоновки машинного кода недоступен. Чтобы увидеть предупреждения AOT, добавьте
`-p:EnableWindowsTargeting=true -p:DisableUnsupportedError=true`. Тогда компилятор сообщает
предупреждения, а шаг компоновки завершается с ошибкой.

## Тесты

`CDPIUI.Tests` компилирует `LaunchPolicy.cs` как связанный файл. Он не ссылается на проект Native AOT.
`CDPIUI.Tests/Tools/ElevatedLauncherTests.cs` проверяет таблицу целей, фильтр переменных и аргументы
`ElevatedLauncherCommand`.

## Правила

- Сохраняйте программу запуска маленькой. Не добавляйте пакеты NuGet, отражение (reflection), JSON и другой код, который Native AOT не компилирует без предупреждений.
- Не добавляйте цель, которая берёт путь или имя файла из аргументов. Каждая цель — постоянное имя файла в папке программы запуска.
- Изменяйте имена целей в `LaunchPolicy` и `ElevatedLauncherCommand` вместе. Тест сравнивает их.

ВНИМАНИЕ: Не удаляйте `requireAdministrator` из `app.manifest` и не публикуйте программу запуска без
Native AOT. Программа запуска с узлом .NET читает переменные, которые она должна удалить.
