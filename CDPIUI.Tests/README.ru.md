[English](README.md) | **Русский**

# CDPIUI.Tests

`CDPIUI.Tests` содержит тесты xUnit для `CDPIUI.Shared`, `CDPIUI.Core`, `CDPIUI.AddOns`, `Update` и `EditHostFile`. Целевая платформа — `net8.0-windows`, потому что `Update` и `EditHostFile` используют `net8.0-windows`. Проект задаёт `DisableTransitiveFrameworkReferences`: тестам не нужна среда Windows Desktop от `Update`, и они работают также в Linux.

## Запуск тестов

В Windows:

```
dotnet test CDPIUI.Tests/CDPIUI.Tests.csproj
```

В Linux или macOS:

```
dotnet test CDPIUI.Tests/CDPIUI.Tests.csproj -p:EnableWindowsTargeting=true
```

Запустите команду из корня репозитория. Проекты, на которые ссылается тестовый проект, используют пакеты Windows. Поэтому для сборки в другой ОС нужен `EnableWindowsTargeting`.

Тестам не нужны трей, GUI, права администратора или сетевое подключение. Тесты, которые записывают файлы, используют каталоги в `Path.GetTempPath()`.

## Покрытые области

| Папка | Тестовый класс | Предмет |
| ------ | ---------- | ------- |
| `AddOns` | `ConfigImportGoldenTests` | Импорт пакетных файлов Flowseal и сравнение с ожидаемыми файлами в `TestData\ConfigImport` |
| `AddOns` | `ConfigImportServiceTests` | Разбор команд `set` в импортируемых скриптах |
| `AddOns` | `ConfigShareWriteOptionsTests` | Отклонение параметров, которые записывают за пределы пакета `.cdpiconfig` |
| `AddOns` | `ZapretConfigImportServiceTests` | Синтаксис пакетных файлов: хвост команды оболочки, каретки, модификаторы файлов |
| `Core` | `BatchFileEncodingTests` | Кодировка пакетных файлов (кириллица, UTF-8 BOM) |
| `Core` | `ConfigCommandLineTests` | Разбиение командных строк пресетов на токены |
| `Core` | `ConfigFileReferencesTests` | Ссылки на файлы в аргументах пресетов (смещения blob, файлы LUA) |
| `Core` | `IniDocumentTests` | Чтение и изменение INI-файлов через `IniDocument` |
| `Core` | `LScriptCoreTests` | Условия LScript, вложенные аргументы и разбор строк условий |
| `Core` | `ProcessServiceTests` | Флаг тихого режима в аргументах процесса |
| `Core` | `ProxyHelperTests`, `ProxySettingsTests` | Разбор адреса, порта и формата прокси |
| `Core` | `ReplaceVariablesTests`, `ReplaceFilesPathTests` | Замена переменных и замена путей к файлам в пресетах |
| `Core` | `VersionHelperTests` | Сравнение версий |
| `Core` | `Zapret1ToZapret2ConverterTests` | Преобразование аргументов Zapret Legacy в Zapret2 (с ожидаемыми файлами в `TestData\Zapret1ToZapret2`) |
| `Core` | `ZipServiceTests` | Слияние списков доменов при распаковке ZIP |
| `Shared` | `ArchiveLimitsTests` | Ограничения архивов: число записей, степень сжатия, ограниченная распаковка, ссылки в целевом корне |
| `Shared` | `ArchivePathsTests` | Безопасные пути архива внутри целевого корня |
| `Shared` | `ApplicationPathMatcherTests` | Сопоставление путей файлов приложений |
| `Shared` | `CatalogVerifierTests` | Проверка подписи каталогов обновлений |
| `Shared` | `ConditionalTaskFileServiceTests` | Сохранение и загрузка файлов `.cdpitask` |
| `Shared` | `ErrorConvertorTests` | Преобразование кодов ошибок |
| `Shared` | `FileWritingOptionsTests` | Параметры в аргументах компонентов, которые записывают файлы за пределы разрешённых папок |
| `Shared` | `ExecutableLeaseTests` | Заблокированные файлы и отпечаток программы, файл разрешений, папка элемента Магазина, файл ключа pipe |
| `Shared` | `GuiActivationArgumentsTests` | Кодирование и декодирование `--gui-args:` и кавычки в командной строке |
| `Shared` | `OutputCleanupHelperTests` | Удаление escape-последовательностей из вывода консоли |
| `Shared` | `PipeMessageModelTests`, `PipePayloadCodecTests` | Сообщения pipe: сериализация и обратное чтение |
| `Shared` | `StreamStringTests` | Строки потока pipe |
| `Shared` | `TasksManageServiceTests` | Создание и остановка задач при параллельных вызовах |
| `Shared` | `TrustedPathValidatorTests` | Проверки доверенных путей |
| `Shared` | `XmlSettingsServiceTests` | Настройки XML, значения в разных региональных стандартах |
| `Tools` | `EditHostFileTests` | `EditHostFile`: определение кодировки, переносы строк, добавление, удаление и восстановление блока CDPI UI, резервная копия |
| `Tools` | `UpdateInstallHelperTests` | `Update`: проверка путей, порядок слоёв, пропускаемые файлы (`catalog.cat`, `Update.exe`), применение и откат |

## Тестовые данные

`TestData` содержит файлы для тестов. Проект копирует все файлы из `TestData` в выходной каталог (`<None Include="TestData\**" CopyToOutputDirectory="PreserveNewest" />`).

- `release-catalog.cat`: каталог, подписанный сертификатом релиза. Его использует `CatalogVerifierTests`.
- `ConfigImport\<case>\`: пакетные файлы Flowseal и ожидаемые результаты импорта (`*.zapret.expected.txt`, `*.import.expected.txt`). Их использует `ConfigImportGoldenTests`.
- `Zapret1ToZapret2\`: аргументы Zapret Legacy и ожидаемые аргументы Zapret2. Их использует `Zapret1ToZapret2ConverterTests`.

Чтобы прочитать тестовый файл, используйте `Path.Combine(AppContext.BaseDirectory, "TestData", "<file>")`. Чтобы прочитать файл ожидаемых результатов, используйте `GoldenFile.Load(...)`. В формате этого файла строка `[name]` начинает секцию. Строки после неё — значения секции. Класс пропускает пустые строки и строки, которые начинаются с `#`.

## Добавление теста

1. Поместите тестовый класс в папку тестируемого проекта: `AddOns`, `Core` или `Shared`. Тесты `Update` и `EditHostFile` помещайте в `Tools`.
2. Используйте пространство имён `CDPIUI.Tests.<Folder>`.
3. Пометьте каждый тест атрибутом `[Fact]` или `[Theory]`. Проект импортирует `Xunit` глобально.
4. Если тест записывает файлы, используйте новый каталог в `Path.GetTempPath()`.
5. Удалите этот каталог в `Dispose`.
6. Если тесту нужен файл данных, поместите его в `TestData`.
7. Запустите тесты.

Не используйте в тесте реальный каталог данных приложения или установленные настройки.

## Что не покрыто

Тесты не покрывают проект GUI `CDPIUI` (WinUI 3). Тестовый проект не ссылается на него. Окна, страницы, `CommandUriMapper`, `WindowOpenHelper`, парсер справки и локализация требуют ручной проверки.

Тесты также не покрывают `CDPIUI.TrayIcon` и `NewSetup`. Для `Update` окно, остановка приложения и запуск трея требуют ручной проверки.
