[English](README.md) | **Русский**

# CDPIUI.AddOns

`CDPIUI.AddOns` — библиотека внутренних дополнений CDPI UI. У каждого дополнения есть своя папка. Проект главного окна (GUI) `CDPIUI` содержит страницы, диалоги и переводы дополнений.

## Проект

| Элемент | Значение |
|---|---|
| Файл проекта | `CDPIUI.AddOns/CDPIUI.AddOns.csproj` |
| Целевая платформа | `net8.0` (не целевая платформа Windows, поэтому типов WinUI нет) |
| Ссылки на проекты | `CDPIUI.Core`, `CDPIUI.Shared` |
| Ссылки на пакеты | `System.ServiceProcess.ServiceController` |
| Встроенные ресурсы | `BlockCheck2/Catalog/strategy-catalog.v1.json` |
| Внутренние типы видны для | `CDPIUI.Tests` (`InternalsVisibleTo`) |
| Проекты, которые ссылаются на него | `CDPIUI`, `CDPIUI.Tests` |

Пространство имён дополнения — `CDPIUI.AddOns.<Folder>`, например `CDPIUI.AddOns.ConfigImport`.

## Дополнения

### BlockCheck2

BlockCheck2 подбирает стратегии Zapret2 для набора сайтов с помощью проверок `curl`. Оно объединяет успешные стратегии в один мультипрофильный пресет, проверяет весь пресет и при необходимости исправляет его. Оно также хранит историю отчётов и позволяет пользователю собрать и сохранить пресет вручную. Окно автоподбора (`cdpiui://Tools/AutoConfig/Zapret2`) открывает его для Zapret2. См. [BlockCheck2/README.ru.md](BlockCheck2/README.ru.md).

### ConfigImport

ConfigImport преобразует файлы `.bat`, `.cmd`, `.txt` и `.json` в конфиг CDPI UI с помощью статического анализа. Оно никогда не запускает исходный скрипт. У него есть специальный анализатор для `winws` и `winws2`. Оно находит недостающие ресурсы, позволяет пользователю протестировать результат и копирует ресурсы в `LocalUserStorage`. Оно не копирует ресурс вне папки импортируемого файла. Пакеты `.cdpiconfig` оно передаёт в ConfigShare. Его использует окно импорта (`cdpiui://Tools/ImportConfig`). См. [ConfigImport/README.ru.md](ConfigImport/README.ru.md).

### ConfigShare

ConfigShare экспортирует один пресет вместе с его ресурсами в ZIP-архив `.cdpiconfig`. Оно проверяет, распаковывает и устанавливает такие архивы. Оно ограничивает размер архива, число записей и степень сжатия и отклоняет параметры, которые записывают файлы вне пакета. Его используют диалог экспорта `ConfigSelector`, редактор пресетов (`ConfigMakerUserControl`) и активация по файлу. См. [ConfigShare/README.ru.md](ConfigShare/README.ru.md).

### GoodCheck

GoodCheck — сторона CDPI UI для внешнего дополнения GoodCheck из Магазина. Оно готовит списки сайтов, отправляет в трей запрос на запуск, читает журналы GoodCheck и записывает отчёт XML. Оно поддерживает только ByeDPI (`ciadpi`), GoodbyeDPI (`gdpi`) и Zapret Legacy (`zapret`) в режимах `AllAsOne` и `AnyAsAny`. Окно автоподбора открывает его для этих компонентов. См. [GoodCheck/README.ru.md](GoodCheck/README.ru.md).

### HostsFileEditor

HostsFileEditor запускает `EditHostFile.exe` с правами администратора, чтобы добавить, удалить или восстановить блок CDPI UI в файле hosts Windows. Оно возвращает код выхода утилиты. Его использует окно файла hosts (`cdpiui://Tools/Hosts`). См. [HostsFileEditor/README.ru.md](HostsFileEditor/README.ru.md).

### Troubleshooting

Troubleshooting проверяет службы Windows, сетевые настройки и программы, которые могут остановить обход DPI. Оно также проверяет доступ к репозиториям Магазина и к папкам Магазина. Оно может исправить метки времени TCP, службу WinDivert и доступ к папкам Магазина. Его использует окно устранения неполадок (`cdpiui://Tools/Troubleshooting`). См. [Troubleshooting/README.ru.md](Troubleshooting/README.ru.md).

## Добавление нового внутреннего дополнения

### Логика в `CDPIUI.AddOns`

1. Создайте папку `CDPIUI.AddOns/<Name>`.
2. Используйте пространство имён `CDPIUI.AddOns.<Name>` и вложенные в него пространства имён.
3. Помещайте в папку только логику без UI. Проект нацелен на `net8.0` и не может использовать типы WinUI или XAML.
4. Используйте публичные типы `CDPIUI.Core` и `CDPIUI.Shared` напрямую. Не добавляйте ссылку на проект `CDPIUI`.
5. Добавьте в `CDPIUI.AddOns.csproj` элемент `EmbeddedResource` для каждого файла, который дополнение встраивает. SDK включает файлы `.cs` автоматически.
6. Напишите `README.md` и `README.ru.md` в папке. Добавьте абзац и ссылку в этот файл.

Соблюдайте такие правила:

- Дайте UI точку входа, которая принимает свои зависимости как параметры, например `BlockCheckAddOnService`.
- Скрывайте код, который запускает процессы, за интерфейсом, например `IBlockCheckStrategyRunner`. Тогда тесты смогут использовать поддельную реализацию.
- Возвращайте стабильные коды проблем или ошибок. Не возвращайте переведённые тексты.
- Запускайте компоненты и исполняемые файлы внешних дополнений через трей, например с помощью `ComponentTasksManager` или сообщения через именованный канал (pipe). Трей запускает только файлы, которые проходят его проверки доверенных путей. Процесс GUI может сам запускать системные утилиты, например `curl.exe` в BlockCheck2. Запускайте системную утилиту по полному пути, например `Path.Combine(Environment.SystemDirectory, "curl.exe")`. Windows ищет имя файла без пути также в текущей папке. Поэтому файл с тем же именем в этой папке может запуститься вместо системной утилиты.

ПРЕДУПРЕЖДЕНИЕ: Трей работает с правами администратора. Не передавайте в трей путь или аргументы из недоверенного файла без проверки. Например, ConfigShare и BlockCheck2 отклоняют параметры, которые записывают файлы.

### Части GUI в `CDPIUI`

| Часть | Папка |
|---|---|
| Окно | `CDPIUI/Windows` |
| Страницы окна | `CDPIUI/Views/<WindowName>` |
| Пользовательские элементы управления | `CDPIUI/Controls/<Name>` |
| Диалоги содержимого | `CDPIUI/Controls/Dialogs/<Name>` |
| Модели представления | `CDPIUI/ViewModels` |
| Вспомогательные классы GUI, которые используют переводы или типы XAML | `CDPIUI/Helper/AddOns/<Name>` |
| Строки UI | `CDPIUI/Strings/en-us/Resources.resw` и `CDPIUI/Strings/ru/Resources.resw` |
| Статьи справки | `CDPIUI/Help/EN` и `CDPIUI/Help/RU` |
| Маршруты `cdpiui://` | `CDPIUI/Commands/CommandUriMapper.cs` |
| Карточка входа на странице инструментов | `CDPIUI/Views/UtilsPage.xaml(.cs)` |

### Тесты

Добавляйте модульные тесты в `CDPIUI.Tests/AddOns`. `CDPIUI.Tests` уже ссылается на `CDPIUI.AddOns` и видит его внутренние типы.

### Внешние дополнения

Внешнее дополнение, например GoodCheck, — это исполняемый файл из Магазина. Его ID добавляется в `HardcodedItemIds.AddOnsIds` в `CDPIUI.Core/Store/Data/HardcodedItemIds.cs`. Код в `CDPIUI.AddOns` настраивает его, запускает через трей и читает его результаты.
