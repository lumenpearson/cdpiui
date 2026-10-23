[English](README.md) | **Русский**

# CDPIUI.Tests

`CDPIUI.Tests` содержит тесты xUnit для `CDPIUI.Shared`, `CDPIUI.Core` и `CDPIUI.AddOns`. Целевая платформа — `net8.0`.

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
| `AddOns` | `ConfigImportServiceTests` | Разбор команд `set` в импортируемых скриптах |
| `AddOns` | `ConfigShareWriteOptionsTests` | Отклонение параметров, которые записывают за пределы пакета `.cdpiconfig` |
| `AddOns` | `ZapretConfigImportServiceTests` | Синтаксис пакетных файлов: хвост команды оболочки, каретки, модификаторы файлов |
| `Core` | `ArchivePathsTests` | Безопасные пути архива внутри целевого корня |
| `Core` | `BatchFileEncodingTests` | Кодировка пакетных файлов (кириллица, UTF-8 BOM) |
| `Core` | `ConfigCommandLineTests` | Разбиение командных строк пресетов на токены |
| `Core` | `ConfigFileReferencesTests` | Ссылки на файлы в аргументах пресетов (смещения blob, файлы LUA) |
| `Core` | `LScriptCoreTests` | Условия LScript, вложенные аргументы и разбор строк условий |
| `Core` | `ProcessServiceTests` | Флаг тихого режима в аргументах процесса |
| `Core` | `ProxyHelperTests`, `ProxySettingsTests` | Разбор адреса, порта и формата прокси |
| `Core` | `ReplaceVariablesTests` | Замена переменных |
| `Core` | `VersionHelperTests` | Сравнение версий |
| `Core` | `Zapret1ToZapret2ConverterTests` | Преобразование аргументов Zapret Legacy в Zapret2 |
| `Core` | `ZipServiceTests` | Слияние списков доменов при распаковке ZIP |
| `Shared` | `ApplicationPathMatcherTests` | Сопоставление путей файлов приложений |
| `Shared` | `CatalogVerifierTests` | Проверка подписи каталогов обновлений |
| `Shared` | `ConditionalTaskFileServiceTests` | Сохранение и загрузка файлов `.cdpitask` |
| `Shared` | `ErrorConvertorTests` | Преобразование кодов ошибок |
| `Shared` | `GuiActivationArgumentsTests` | Кодирование и декодирование `--gui-args:` и кавычки в командной строке |
| `Shared` | `OutputCleanupHelperTests` | Удаление escape-последовательностей из вывода консоли |
| `Shared` | `PipeMessageModelTests`, `PipePayloadCodecTests` | Сообщения pipe: сериализация и обратное чтение |
| `Shared` | `StreamStringTests` | Строки потока pipe |
| `Shared` | `TasksManageServiceTests` | Создание и остановка задач при параллельных вызовах |
| `Shared` | `TrustedPathValidatorTests` | Проверки доверенных путей |
| `Shared` | `XmlSettingsServiceTests` | Настройки XML, значения в разных региональных стандартах |

## Тестовые данные

`TestData` содержит файлы для тестов. Проект копирует все файлы из `TestData` в выходной каталог (`<None Include="TestData\**" CopyToOutputDirectory="PreserveNewest" />`).

- `release-catalog.cat`: каталог, подписанный сертификатом релиза. Его использует `CatalogVerifierTests`.

Чтобы прочитать тестовый файл, используйте `Path.Combine(AppContext.BaseDirectory, "TestData", "<file>")`.

## Добавление теста

1. Поместите тестовый класс в папку тестируемого проекта: `AddOns`, `Core` или `Shared`.
2. Используйте пространство имён `CDPIUI.Tests.<Folder>`.
3. Пометьте каждый тест атрибутом `[Fact]` или `[Theory]`. Проект импортирует `Xunit` глобально.
4. Если тест записывает файлы, используйте новый каталог в `Path.GetTempPath()`.
5. Удалите этот каталог в `Dispose`.
6. Если тесту нужен файл данных, поместите его в `TestData`.
7. Запустите тесты.

Не используйте в тесте реальный каталог данных приложения или установленные настройки.

## Что не покрыто

Тесты не покрывают проект GUI `CDPIUI` (WinUI 3). Тестовый проект не ссылается на него. Окна, страницы, `CommandUriMapper`, `WindowOpenHelper`, парсер справки и локализация требуют ручной проверки.

Тесты также не покрывают `CDPIUI.TrayIcon`, `EditHostFile`, `Update` и `NewSetup`.
