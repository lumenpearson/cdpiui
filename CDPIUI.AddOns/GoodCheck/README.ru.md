[English](README.md) | **Русский**

# Дополнение GoodCheck

## Назначение

GoodCheck — внешнее дополнение. Это отдельный исполняемый файл, который пользователь устанавливает из Магазина. ID в Магазине — `ASGKOI001` (`HardcodedItemIds.AddOnsIds[AddOns.GoodCheck]`). GoodCheck запускает компонент с каждой стратегией из списка стратегий и проверяет сайты из списка сайтов. Результаты он пишет в файл лога.

Папка `CDPIUI.AddOns/GoodCheck` содержит часть дополнения на стороне CDPI UI. Этот код готовит списки сайтов, отправляет трею запрос на запуск, читает лог GoodCheck, подсчитывает результаты и пишет отчёт XML. Код CDPI UI не содержит алгоритм GoodCheck.

## Поддерживаемые компоненты

GoodCheck поддерживает только эти компоненты:

| Компонент | Ключ `HardcodedItemIds` | Имя в GoodCheck (`-f`) |
|---|---|---|
| ByeDPI | `Components.ByeDPI` | `ciadpi` |
| GoodbyeDPI | `Components.GoodbyeDPI` | `gdpi` |
| Zapret Legacy | `Components.Zapret` | `zapret` |

Это соответствие хранит `GoodCheckResultViewHelper.ComponentNormalNames`. Его использует `GoodCheckProcessService.IsComponentSupported`. Для других компонентов `InitGoodCheck` выбрасывает `NotSupportedException`. Главное окно (GUI) использует тот же список из `HardcodedItemIds.GoodCheckSupportedComponents`.

Для Zapret2 окно автоподбора открывает BlockCheck2. См. [BlockCheck2/README.ru.md](../BlockCheck2/README.ru.md).

## Основные типы

| Тип | Файл | Назначение |
|---|---|---|
| `GoodCheckProcessService` | `GoodCheckProcessService.cs` | Синглтон. Готовит списки, запускает проверки по очереди, читает логи, вызывает события и пишет отчёт. |
| `GoodCheckProcessMode` | `GoodCheckProcessService.cs` | `AllAsOne` или `AnyAsAny` |
| `GoodCheckSiteListModel` | `GoodCheckProcessService.cs` | Список сайтов и его список стратегий |
| `GoodCheckOperationModel` | `GoodCheckProcessService.cs` | Одна проверка: ID, имя списка, вывод, состояние, код ошибки и токен отмены |
| `GoodCheckOperationType` | `GoodCheckProcessService.cs` | `Wait`, `WorkInProgress`, `SuccessFinish`, `ErrorHappens` или `UserInterrupt` |
| `GoodCheckResultViewHelper` | `GoodCheckResultViewHelper.cs` | Имена компонентов, доступные списки стратегий и загрузчик отчётов |
| `StrategyModel`, `GroupModel`, `GoodCheckStrategiesList` | `GoodCheckResultViewHelper.cs` | Модели отчёта и списка стратегий |
| `SiteListHelper` | `SiteListHelper.cs` | Находит списки сайтов установленных комплектов пресетов |
| `DomainValidationHelper` | `DomainValidationHelper.cs` | Проверяет, что файл TXT — список доменов |

## Режимы

| Режим | Назначение |
|---|---|
| `AllAsOne` | Служба объединяет все выбранные списки сайтов в один список. Она проверяет этот список с одним списком стратегий. |
| `AnyAsAny` | Служба проверяет каждый список сайтов отдельно, каждый со своим списком стратегий. |

В режиме `AllAsOne` служба использует список стратегий первого списка сайтов. Объединённый список она записывает как `<Unix time>_sitelist.txt`. При объединении удаляются пустые и повторяющиеся строки, порядок сохраняется. Объединённый список записывается в UTF-8 без BOM, потому что GoodCheck читает BOM как часть первого домена.

В режиме `AnyAsAny` служба копирует каждый список сайтов в папку `CheckLists`. Если у двух списков одинаковое имя файла, вторая копия получает префикс `<index>_`.

GUI отключает переключатель режима для GoodbyeDPI. Поэтому GoodbyeDPI использует только `AllAsOne`.

## Файлы дополнения

Все пути указаны относительно папки дополнения. `GoodCheckProcessService.GetAddOnDirectory()` возвращает папку из базы данных Магазина. Если в базе данных нет корректной папки, метод возвращает `Store/Items/ASGKOI001`.

| Путь | Содержимое |
|---|---|
| `<Executable>.exe` | Исполняемый файл GoodCheck. Имя берётся из поля `Executable` элемента Магазина. |
| `config.ini` | Настройки GoodCheck |
| `CheckLists/` | Списки сайтов текущего запуска |
| `StrategiesGoGo/<ciadpi\|gdpi\|zapret>/*.txt` | Списки стратегий каждого компонента |
| `Logs/logfile_GoodCheckGoGo_*.log` | Логи, которые пишет GoodCheck |
| `Reports/GoodCheckReport_<Unix time>.xml` | Отчёты, которые пишет CDPI UI |

Перед каждым запуском служба удаляет объединённые списки (`<digits>_sitelist.txt`) старше одного дня.

## Порядок запуска

1. Пользователь открывает окно автоподбора и выбирает поддерживаемый компонент.
2. Если GoodCheck не установлен, окно открывает диалог загрузки Магазина для `ASGKOI001`.
3. На странице `CreateViaGoodCheck` пользователь выбирает списки сайтов, режим и списки стратегий.
4. Пользователь задаёт дополнительные настройки. Страница сохраняет их (см. «Настройки»).
5. Страница вызывает `InitGoodCheck` с ID компонента, режимом и моделями списков. Служба готовит списки и создаёт по одному `GoodCheckOperationModel` на каждую проверку.
6. Страница открывает `GoodCheckWorkPage` и вызывает `Start`.
7. Для каждой проверки служба отправляет сообщение pipe `GoodCheckMessageIds.Start` с путём к исполняемому файлу и аргументами.
8. Трей запускает GoodCheck. См. «Процесс трея».
9. Служба читает самый новый лог GoodCheck и обновляет прогресс.
10. После последней проверки или после отмены служба пишет отчёт XML и вызывает `AllComplete`.
11. `GoodCheckWorkPage` показывает итоговое состояние. Кнопка «вперёд» открывает отчёт в `CreateConfigHelperWindow`.

Служба использует такие аргументы:

```text
-q -p <passes> -f <ciadpi|gdpi|zapret> -m <curl|native> -c "<site list file name>" -s "<strategy list file name>"
```

Служба передаёт только имена файлов списка сайтов и списка стратегий.

## Процесс трея

Процесс запускает `CDPIUI.TrayIcon/Helper/GoodCheckProcessHelper.cs`. Он работает с правами администратора. Он выполняет такие шаги:

1. Проверяет аргументы через `FileWritingOptions`. Он также читает файлы аргументов (`@file`). Если параметр записывает файл за пределами папок приложения, сообщает `ERR_UNABLE_START_PROCESS`.
2. Проверяет исполняемый файл через `ExecutableTrust.AcquireAsync`. Исполняемый файл должен находиться по доверенному пути (`TrustedPaths.IsAllowed`). В установке MSI для программы вне папки установки действует дополнительная проверка. Она должна быть подписанным элементом Магазина, или пользователь должен одобрить её новые или изменённые файлы. Иначе сообщает `ERR_UNABLE_START_PROCESS`.
3. Останавливает все задачи компонентов.
4. Отправляет GUI `Running` и блокирует кнопки компонентов.
5. Запускает GoodCheck без окна. Рабочая папка — папка исполняемого файла.
6. Помещает процесс в объект задания (job object), который завершает процесс при закрытии задания.
7. Когда процесс завершается, отправляет `Died` с ID операции.
8. Разблокирует кнопки компонентов.

Ошибка запуска даёт `DiedViaError` с `ERR_UNABLE_START_PROCESS` или `ERR_WIN32_EXCEPTION`. Сообщение `Stop` завершает процесс. Когда pipe отключается, трей тоже останавливает GoodCheck.

В GUI `CDPIUI/Commands/CommandsHandler.cs` передаёт `Died` в `OperationWithIdDied`, а `DiedViaError` — в `OperationWithIdFailed`. `OperationWithIdFailed` помечает операцию как неудачную и останавливает монитор лога этой операции. Если в сообщении нет кода ошибки, код — `ERR_UNKNOWN`.

## Монитор лога

`MonitorProcessAndLogsAsync` читает только логи, которые GoodCheck создал после начала текущей проверки (не более чем на 2 секунды раньше). Он выбирает самый новый файл, читает его с начала и перечитывает каждые 500 мс.

Для каждой строки служба выполняет такие проверки:

| Текст в строке | Результат |
|---|---|
| `Launching '<name>', strategy N/M: [<strategy>]` | Вызывает `ProgressChanged` |
| `This strategy has no successes` | Увеличивает счётчик неверных |
| `worst result for this strategy: S/A`, за которым следует `Terminating program...` | Вызывает `StrategyResultAdded`. Если `S * 100 / A >= 65`, увеличивается счётчик верных. Иначе, если `S` не равно 0, увеличивается счётчик неверных. |
| `Exiting with an error...` | Проверка завершается ошибкой `ERR_GOODCHECK_EXCEPTION` |
| `All Done` | Проверка успешна |

Когда трей сообщает `Died`, служба читает остаток лога. Если в остатке нет `All Done` или текста ошибки, проверка завершается ошибкой `ERR_GOODCHECK_EXITED_EARLY`.

Служба хранит полный вывод каждой проверки в `GoodCheckOperationModel`. Для каждой строки она также вызывает `OperationOutputAdded`.

## Отчёт

`CreateAndSaveReport` пишет отчёт XML:

```xml
<Report>
  <Group Name="<list name>" FullPath="<list path>" ComponentId="<component ID>">
    <StrategyResult>
      <Flag>false</Flag>
      <Strategy>...</Strategy>
      <Success>S</Success>
      <All>A</All>
    </StrategyResult>
  </Group>
</Report>
```

Служба создаёт один `Group` на каждую операцию. Отчёт пишется и после отмены. Отчёт читает `GoodCheckResultViewHelper.LoadGroupsFromFile`. Группа без `ComponentId` делает файл некорректным.

Пользователь может открыть отчёты в таких местах:

- `GoodCheckWorkPage`, после запуска;
- `RecentGoodCheckSelectionsContentDialog`, который показывает содержимое папки `Reports`;
- кнопка «открыть отчёт» в `CreateConfigHelperWindow`, через диалог выбора файла.

Отчёт показывают `ViewGoodCheckReportPage` и `ViewGoodCheckSiteListReportPage` (`CDPIUI/Views/CreateConfigHelper`).

## Отмена

| Действие | Метод | Результат |
|---|---|---|
| Остановить все проверки | `Stop` | Отменяет токен запуска и отправляет трею `Stop` |
| Остановить текущую проверку | `RemoveFromQueueOrStopOperation(CurrentOperationId)` | Переводит проверку в `UserInterrupt` и отменяет её токен |
| Завершить состояние службы после отчёта | `Kill` | Отменяет и освобождает токен запуска |

Когда запуск заканчивается, служба переводит в `UserInterrupt` каждую проверку, которая ещё в состоянии `WorkInProgress`. Если пользователь закрывает `CreateConfigUtilWindow` во время запуска, окно предлагает остановить запуск и вызывает `Stop`.

## Ошибки

| Код | Источник | Условие |
|---|---|---|
| `ERR_ADDON_NOT_INSTALLED` | Служба | GoodCheck не установлен или его исполняемого файла нет |
| `ERR_EXE_NOT_FOUND` | Служба | Исполняемого файла нет в момент запуска проверки |
| `ERR_GOODCHECK_EXCEPTION` | Служба | Лог содержит `Exiting with an error...` |
| `ERR_GOODCHECK_EXITED_EARLY` | Служба | Процесс завершился до `All Done` |
| `ERR_MONITOR_EXCEPTION` | Служба | Монитор лога завершился ошибкой |
| `ERR_INTERNAL` | Служба | Необработанное исключение во время запуска |
| `ERR_UNKNOWN` | Служба | Трей отправил `DiedViaError` без кода ошибки |
| `ERR_UNABLE_START_PROCESS` | Трей | Путь не доверенный, файлы не одобрены, аргументы записывают файл за пределами папок приложения или процесс не запустился |
| `ERR_WIN32_EXCEPTION` | Трей | Ошибка объекта задания или запуска процесса |

## Настройки

Настройки читает и пишет страница `CreateViaGoodCheck` (`CDPIUI/Views/CreateConfigUtil/CreateViaGoodCheckPage.xaml.cs`).

Настройки CDPI UI, группа `ADDONS/ASGKOI001`:

| Ключ | Тип | Назначение |
|---|---|---|
| `passesValue` | `string` | Количество проходов (`-p`). Значение, которое не является целым числом 1 или больше, становится 1. |
| `useCurl` | `bool` | `true` даёт `-m curl`, `false` даёт `-m native` |

Для ByeDPI страница предлагает только резолвер curl.

`config.ini` GoodCheck (UTF-8):

| Раздел | Ключи |
|---|---|
| `General` | `ConnectionTimeout`, `AutomaticGoogleCacheTest` |
| `Advanced` | `ResolverNativeTimeout`, `ResolverNativeRetries`, `InternalTimeoutMs`, `AutomaticConnectivityTest`, `ConnectivityTestURL`, `SkipCertVerify`, `GoogleCacheMappingURLs` |
| `Resolvers` | `UseDoH`, `DoHResolvers` |
| `Fakes` | `FakeSNI`, `FakeHexStreamTCP`, `FakeHexStreamUDP`, `FakeHexBytesTCP`, `FakeHexBytesUDP`, `PayloadTCP`, `PayloadUDP` |
| `Zapret`, `GoodbyeDPI`, `ByeDPI` | Папка и имя исполняемого файла каждого установленного компонента. Папка берётся из базы данных Магазина. |

Если `config.ini` нет, страница просит пользователя переустановить дополнение.

## Списки сайтов

`SiteListHelper.GetAllAvailableSiteListTemplatesAsync` находит файлы TXT в папках и подпапках всех элементов Магазина типа `configlist`. Показываются только файлы, которые проходят проверку `Quick` в `DomainValidationHelper`. Если в одном комплекте есть несколько файлов с одинаковым именем и разным содержимым, в списке остаются все. Файлы с одинаковым именем и одинаковым SHA-256 показываются один раз.

`DomainValidationHelper` проверяет список. В режиме `Quick` он читает первые 10 строк, которые не пусты и не являются комментариями. Список некорректен, если строка содержит пробельный символ или `/` либо если строка — IP-адрес. Файл, который выбирает пользователь, должен пройти проверку `Quick`. Затем страница копирует его в `Store/Items/LocalUserStorage/List` с уникальным именем.

## Точки входа в GUI

| Точка входа | Расположение |
|---|---|
| Окно автоподбора, выбор компонента | `CDPIUI/Views/CreateConfigUtil/MainPage.xaml.cs` (`BeginNewSelectionButton_Click`) |
| Маршрут | `cdpiui://Tools/AutoConfig/{componentId}` |
| Страницы настройки | `CDPIUI/Views/CreateConfigUtil/CreateViaGoodCheckPage.xaml.cs` (класс `CreateViaGoodCheck`) |
| Страница прогресса | `CDPIUI/Views/CreateConfigUtil/GoodCheckWorkPage.xaml.cs` |
| Элементы управления списками сайтов | `CDPIUI/Controls/GoodCheck` |
| Страницы отчёта | `CDPIUI/Views/CreateConfigHelper/ViewGoodCheckReportPage.xaml.cs`, `ViewGoodCheckSiteListReportPage.xaml.cs` |
| Недавние отчёты | `CDPIUI/Controls/Dialogs/CreateConfigHelper/RecentGoodCheckSelectionsContentDialog.xaml.cs` |

## Ограничения

- Одновременно может работать только один запуск GoodCheck, потому что `GoodCheckProcessService` — синглтон, а у трея одно место для процесса.
- Трей останавливает все компоненты перед запуском GoodCheck.
- Служба передаёт с `-c` и `-s` только имена файлов. GoodCheck должен найти файлы из своей папки. Поэтому страница показывает только списки стратегий верхнего уровня `StrategiesGoGo/<component>`, без подпапок.
- В режиме `AllAsOne` используется только список стратегий первого списка сайтов.
- Счётчики верных и неверных используют фиксированный порог 65%.
- У дополнения нет тестов в `CDPIUI.Tests`.
