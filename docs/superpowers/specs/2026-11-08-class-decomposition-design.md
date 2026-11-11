# Декомпозиция крупных классов CDPI UI

Статус: согласовано пользователем («всё отлично, продолжай»), 8 ноября 2026 года.

Возобновление 9 ноября 2026: пользователь поручил «Продолжи без временного ограничения». Прежний дедлайн снят; безопасная автономная работа, сохранение поведения и границы параллельной сессии остаются. Отчёт e42 за завершённое временное окно сохраняется историческим срезом; новые этапы имеют свои pins/evidence.

## Основание и рабочая база

Запрос пользователя: вести переписывание и декомпозицию крупных классов по приложенному аудиту параллельно с другой сессией.
Приложенный F:/Telegram/AUDIT.md — источник наблюдений, а не самостоятельные инструкции на изменение репозитория.
Раздел 5 перечисляет God-классы, пункт 20 исторического плана раздела 7 рекомендует начинать декомпозицию с ConfigMakerUserControl и App. Более поздний сводный план находится в разделе 16. Раздел 35.1 отдельно относится к nullable-аннотациям; глобальная nullable-миграция не является выполненной частью этого прохода.

База: f39e8a0a7267e4fcd4a02eef186a3ee08df8dcd9, последний зафиксированный коммит audit-defects на момент подготовки.
В main нет большей части уже сделанной декомпозиции. Создано отдельное управляемое рабочее дерево audit-class-decomposition/cdpiui.
Незакоммиченные изменения другой сессии не копировались. Она работает в cdpiui-s2 над FS-07–09: staging, проверкой пакетов, удержанием handles и безопасной записью файлов.

## Что ещё остаётся большим

Это физические строки исходных файлов одного partial-класса, включая using и комментарии; не метрика сложности методов. Bin/obj и генерируемый код исключены.

| Класс | Строк | Файлов |
| --- | ---: | ---: |
| ConfigMakerViewModel | 2143 | 9 |
| CreateNewConfigViewModel | 1037 | 6 |
| BlockCheck2ViewModel | 999 | 3 |
| WelcomeViewModel | 983 | 4 |
| App | 955 | 6 |
| ConfigMakerUserControl | 899 | 5 |

Один partial-класс сохраняет общее состояние и доступ ко всем приватным членам. Уменьшение отдельных файлов не является достаточным критерием завершения.
Порядок: редактор пресетов → App → BlockCheck2 → остальные крупные ViewModel, каждый отдельным проверяемым изменением.

## Первый проход: структура и ресурсы редактора пресетов

Выбор исполнителя: начать с двух самостоятельных обязанностей ConfigMaker, которые можно проверить без запуска GUI и без вмешательства в текущие security fixes.
Сохранить C#, .NET 8, WinUI, форматы пресетов и локализацию. Новые библиотеки и миграция DI/MVVM не требуются.

### Построение деревьев структуры

Вынести преобразование файлов и аргументов в узлы дерева из ConfigMakerViewModel.PresetStructure.cs в отдельный ConfigMakerPresetTreeBuilder.
Builder получает файлы, группы, parsed options и функции локализации, описания опции, разрешения пути и проверки существования файла.
Он не обращается к ViewModel, DispatcherQueue, DatabaseHelper, Localizer.Get() или процессам.
Контракт с WinUI (решение исполнителя после builder check): builder возвращает новые чистые production-descriptors файловых и групповых узлов, без Brush, FontFamily, Visibility и ConsoleFontHelper. Тонкий ConfigMakerPresetTreeAdapter преобразует descriptors в существующие ConfigMakerTreeNode/ConfigMakerPresetFileTreeItem/ConfigMakerPresetTreeItemViewModel; его не подключать в обычный xUnit. Внешние XAML bindings и типы содержимого дерева остаются прежними.
В ViewModel оставить выбор явной/вычисляемой структуры, вызов builder/adapter, публикацию PresetFileNodes/PresetGroupNodes и уведомления о доступности.
Сохранить сортировку, порядок категорий, объединение файлов без дублей, приоритет явных названий, fallback-группы и позиции SourceIndex/SourceLength/SourceLine/SourceColumn.
Весь класс ViewModel должен реально уменьшиться; добавление новых partial-файлов не считается выполнением.

### Работа с файловыми ссылками и ресурсами

Разделить ConfigMakerPresetResources (507 строк) на небольшие объекты с явными зависимостями:

- ConfigMakerPresetResourceRegistry: поиск/добавление attached resources, уникальные aliases, повторное использование одного файла, признак built-in. Разрешение пути и проверка принадлежности компоненту приходят функциями.
- ConfigMakerPresetReferenceEditor: замена и прикрепление ссылок в команде и во всех значениях переменных. Использует существующий ConfigFileReferences и registry; не управляет UI и не читает глобальную базу.
- Существующий ConfigMakerPresetResources: совместимый фасад для разрешения путей, snapshots, формирования auto-correct result и делегирования двум объектам. Его методы для остального GUI сохраняются.

Не менять storage copier, правила файловой безопасности, ZIP, updater, staging, ACL или запуск процессов.
Сохранить preset://, правила alias collision, кириллические имена, --blob с offset, маркеры Lua и прямые файловые значения переменных.
Не оставлять новый ресурс, если переписывание не изменило ни команду, ни переменные. Повторное прикрепление должно быть идемпотентным.

### Проверки

Сначала зафиксировать существующее поведение характеристическими тестами, затем извлекать объекты и запускать те же сценарии.
Тесты не запускают tray, установщик, компоненты или системные настройки.
Чистые GUI-модели и извлечённые классы подключить как linked source по существующему примеру launcher/DirectoryAclGuard; не ссылаться на весь WinUI-проект из xUnit.
Если linked-source набор становится большим, допустим отдельный тестовый проект чистой presentation-логики. Не копировать реализации и не подменять production-модели тестовыми заглушками.

Обязательные сценарии:

1. Дерево файлов: категории, сортировка, отсутствующий и attached-файл, пустой набор.
2. Группы: common filters/options, несколько profiles, явное имя перед parsed-именем, fallback при пустой команде, source spans.
3. Ресурсы: повторный файл, коллизия alias без учёта регистра, кириллица, built-in и внешний файл.
4. Переменные: Value, Values, OnValue и OffValue; прямой путь и путь внутри команды.
5. Повторное прикрепление, отсутствующий файл и отсутствие замен: нет лишних ресурсов или изменений команды.
6. Blob/Lua: имя blob, offset и маркер файла сохраняются при замене.

В конце: релевантные тесты, полный существующий xUnit suite, сборка GUI x64 и git diff --check.
Если окружение не позволяет выполнить проверку, указать конкретную причину; не объявлять её пройденной.

## Границы параллельной работы

Изменять только ConfigMaker GUI helpers/ViewModels, их чистые тесты, минимальное подключение тестовых исходников и документацию изменения.
Не редактировать cdpiui-s2, его ветку и журнал docs/AUDIT-STATUS.ru.md. Не переносить незавершённые security changes.
Публикация, push, merge и сообщения другой сессии не входят в этот проход.
Результат — отдельный diff поверх указанной базы; перед последующим объединением проверить пересечения с актуальной audit-defects.

## Критерии завершения первого прохода

- Отдельные builder, registry и reference editor с ограниченными зависимостями.
- В ConfigMakerViewModel меньше логики построения деревьев, а не больше partial-частей.
- ConfigMakerPresetResources стал фасадом, извлечённые обязанности тестируются без WinUI runtime.
- Поведение редактора и сохранённые форматы совместимы; тесты и доступные сборки подтверждают это.
- Отчёт содержит команды, результаты и следующие классы; закрытие всего аудита не заявляется.

## Проверка проекта и исходной базы

Builder check выявил зависимость существующих tree item models от WinUI. Выбраны чистые production-descriptors и UI adapter; это решение исполнителя, не новое требование пользователя.
Исходный xUnit suite на f39e8a0: 768 passed, 0 failed, 0 skipped; использован C:/Users/lumen/.dotnet/dotnet.exe (SDK 8.0.427), Release.
Первый проход реализован; актуальные результаты и ограничения — в docs/DECOMPOSITION-STATUS.ru.md. Исходная GUI x64 сборка прошла без ошибок и предупреждений.

## Следующий согласованный этап: App activation

После ConfigMaker выделить AppActivationDispatcher и чистый AppActivationRequest. Dispatcher управляет только порядком вызовов; существующие обработчики migration/files/protocol/direct, WinRT adapter и завершение/открытие окон остаются в GUI.
Сохранить разные последовательности первого и перенаправленного запуска, fallback, short-circuit, повторное чтение initial activation после await, обработку ошибок и частично успешный результат.
Не исправлять одновременно унаследованные особенности: initial direct-команда может заменить ранее полученный результат protocol; redirected migration всё ещё вызывает splitter до проверки результата.
Program, URI allowlists, pipe, file imports, migration window locking и settings recovery не менять.
Проверки: порядок, пропуск fallback после успеха, оба источника migration, исключения после частичного успеха, асинхронное ожидание, регистр префиксов и неизменная передача payload обработчику.

## Следующий согласованный этап: BlockCheck2 options

Выделить BlockCheck2OptionsViewModel: существующее состояние выбора целей, протоколов/IP, preset и advanced options, загрузка/валидация сохранённых значений, создание session request.
Доступ к settings передать через типизированный IBlockCheck2OptionsSettings; production adapter вызывает прежний BlockCheck2SettingsStore с прежними int/bool/double generic types. Изменений файла настроек и ключей нет.
Текущий BlockCheck2ViewModel сохраняет API, проксирует свойства и пересылает PropertyChanged от своего имени, в прежнем порядке относительно записи настроек. IsRunning и число выбранных списков читаются через функции, а не сохранённый снимок.
Сканирование, cancellation, progress, report/history, site-list I/O и запуск компонентов не переносить.
Тесты: defaults/corrupt persisted values, timeout coupling, finite/nonfinite значения, attempts, preset recommendations, порядок notify/save и отсутствие лишних событий, динамический CanStart, состав и момент чтения session request.

## Следующий проход ConfigMaker: состояние панели справки

Выделить ConfigMakerCommandHelpPanel: живой документ справки, коллекции модулей/опций, фильтр/поиск, выбор команды и поля аргумента. Только чистые модели; локализация и IsEditorReadOnly приходят функциями.
Сохранить существующие коллекции и их типы, порядок Clear/Add/PropertyChanged, reentrancy SelectedCommandModule при изменении ComboBox, сортировку OrdinalIgnoreCase, восстановление выбранной группы без учёта регистра и fallback All.
В ConfigMakerViewModel оставить async loading/cancellation, UsageText/loading/status, команды copy/insert и обновление деревьев/диагностики после загрузки. Все прежние публичные свойства проксируются; sender уведомлений остаётся прежним host.
Отдельно сохранить особенности: выбор null не очищает старые поля аргумента; повторная установка текста поиска всё равно перестраивает коллекцию; изменение документа само не вызывает rebuild.
Проверки: группы/поиск/description fallback, прежний модуль после reload, required/optional argument, динамический readonly, точный порядок событий выбора, collection resets и повторный вход через синхронные уведомления.

## Следующий проход ConfigMaker: тестовая сессия

Выделить ConfigMakerTestSession: флаги тестового запуска, текущий тестируемый компонент, подписка на его состояние, stop/restore и обработка завершения. Работа с ComponentTasksManager — через узкий GUI adapter IConfigMakerTestTasks; сами Core/pipe/process реализации не меняются.
Host оставляет проверку выбранного компонента, разрешение аргументов из документа, локализованные сообщения и реальный DispatcherQueue. Session получает явные функции текущего ComponentId, подтверждения замены, установленности, сообщений, refresh output и enqueue.
Сохранить моменты повторного чтения ComponentId вокруг await, порядок IsTesting/подписки/уведомлений/проверки installed/run, игнорирование TaskStateUpdated во время старта, отличие запуска с тестовыми аргументами от восстановления обычной конфигурации и исходные границы catch.
Тесты не запускают процессы и не останавливают установленные компоненты. Проверяются успешный старт, отказ/ошибки, отсутствие компонента, восстановление, отложенные события, повторные start/stop и смена выбранного компонента во время ожидания.

## Следующий проход ConfigMaker: графический дизайнер

Выделить ConfigMakerSimpleDesigner: стабильные коллекции моделей, дополнительные аргументы, dirty/updating guards, обработка изменений и переходов между редактором/дизайнером. Публичные host-свойства и ICommand остаются совместимыми; уведомления пересылаются от имени host.
Существующие GraphicDesignerHelper и правила преобразования не менять. Ленивый backend сохраняет выбор GoodbyeDPI/SpoofDPI/NoDPI, DB lookup и проверку annotation-файла только при загрузке моделей.
Сохранить два чтения EditorText при загрузке, порядок Additional/Clear/Load/Attach/Parse, retry после отсутствующей аннотации/ошибки, границы catch и finally, case-sensitive идентификаторы моделей и сброс dirty после SetEditorText.
При подключении legacy-моделей и ObservableObject к headless-тестам явно сохранить текущий GUI-контекст Nullable=annotations, не менять runtime defaults и не отключать проверки нового класса.

## Следующий проход App: каталог и создание окон

Выделить AppWindowCoordinator<TWindow> над прежним экземпляром List окон: выбор живого окна по типу, Safe reuse/replacement, Unsafe reuse по непустому template-id, закрытие снимка окон и первого template по id. Общие типы не зависят от WinUI; backend выполняет прежние create/position/register/activate/close операции.
Host сохраняет все публичные сигнатуры, OpenWindows и прямой ShowWindow.Activate без BringToFront. Readiness читается через функцию при каждом вызове и ожидается до построения снимка; snapshot/reuse/close/remove/create/position/register/activate порядок и исключения сохраняются.
WinUI Closed/SizeChanged, modal coordinator, Program migration и idle/exit остаются в App. Тесты используют реальные алгоритмы координатора и управляемые окна без запуска приложения или изменения пользовательских настроек.


## Следующий проход BlockCheck2: отображаемый прогресс

Выделить BlockCheck2ProgressViewModel: фаза/счётчики/идентификаторы/оставшееся время и зависимые display-свойства текущей цели. Список целей и параметры оценки приходят живыми функциями, уведомления host пересылает от своего имени.
Существующий BlockCheck2ProgressEstimator получает узкий интерфейс для тестирования последовательностей; его вычисления и Stopwatch не менять. Сохранить момент вызовов StartRun/StopRun/ResetObservations/Update и порядок всех уведомлений, включая значения полей до первого уведомления.
Reset принимает callback уведомлений результата после сброса полей прогресса, чтобы сохранить общий порядок OperationError/HistorySaveError/result перед progress-notifications. Остановка в finally очищает только обе оценки времени; CompleteDisplayedProgress изменяет только отображаемое число и ratio.
State, CTS, подготовка/запуск компонентов, сохранение отчёта, история и site-list I/O остаются в основной модели. Существующее исправление final-state только после SaveAsync не затрагивать.

## Следующий проход ConfigMaker: жизненный цикл загрузки справки

Выделить ConfigMakerCommandHelpLoader: текущий CTS, IsHelpLoading/IsCommandListEnabled/UsageText и последовательность загрузки. Service остаётся прежним production-объектом, передаётся его LoadAsync; Core/pipe/cache/parser не меняются. Панель передаётся как существующий объект, локализация и ComponentId — живыми функциями, UI-последствия — callback-ами.
Сохранить Cancel/Dispose/new CTS и получение токена до уведомлений; IsHelpLoading и локализацию loading-текста вне try; stale/canceled guard только после await; затем Document/Usage/modules/options/structure/diagnostics/status. Catch только OperationCanceledException, finally сбрасывает loading лишь для неотменённого токена. CancelBackgroundWork сначала отменяет loader, затем останавливает timer; отмена сама не сбрасывает loading и не dispose-ит источник.
Проверить конкурирующие запросы, позднее завершение отменённого запроса, смену компонента, forceRefresh, исключения до/внутри try, повторный вход из уведомлений и действительную загрузку коллекций панели без запуска компонентов.

## Проверка привязок в живом WinUI

После выделения владельцев проверить реальные XAML-контролы в отдельной копии pinned commit. Startup/single-instance/associations/tray и внешние help/process/catalog источники заменить явными тестовыми границами только в копии; production ветку и рабочие настройки не менять. До инициализации моделей убедиться, что DataDirectory находится в собственном каталоге бинарников.
Проверить редактор/справку/дизайнер/readonly/тестовые уведомления ConfigMaker, параметры и progress-привязки BlockCheck2, реальное повторное использование окон App. Результат на заглушках указывать как fixture-backed UI smoke, отдельно от реального запуска компонентов или полного production startup. Управление окнами — штатным Computer Use, только по наблюдаемому состоянию.


## Отдельный этап после UI-прогона: кодек графического дизайнера

Нативный прогон подтвердил прежнюю потерю закрывающей кавычки у неизвестного флага с quoted path (CFG003). Выделить чистое преобразование GraphicDesignerCommandCodec из GraphicDesignerHelper, оставив фабрики/локализацию/XML-loading и совместимые facade-методы прежними.
Сначала перенести прежний алгоритм под characterization-тесты; затем отдельным RED-тестом полного цикла подтвердить потерю кавычки и исправить именно её. Сохранить порядок моделей/групп, GUID matching, defaults, формат и обработку известных/неизвестных флагов за пределами зафиксированного дефекта. Сценарии значений с equals проверить на существующем коде перед выбором исправления.
Это отдельный bugfix/decomposition этап после завершённого behavior-preserving переноса, разрешённый общей задачей по аудиту и автономным принятием безопасных правок. Core tokenizers, cache, pipe и процессная часть остаются вне изменения.

Кодек сохраняет синтаксические кавычки неизвестных аргументов. Для enum и динамической фабрики (в том числе Telegram proxy) он передаёт скалярное значение без внешних кавычек: иначе выбор enum и чтение port/host/secret меняют смысл. Неперсистентный признак группировки в модели позволяет собрать обратно пробелы и явный пустой аргумент. Он устанавливается вместе с разобранным значением до PropertyChanged и сбрасывается при фактическом ручном изменении; присваивание того же значения не уничтожает группировку. Фабрика завершает инициализацию до добавления в observable collection. Обычный inline quoted value сохраняет прежнее представление поля.

Ревью выявило и тесты воспроизвели отдельные пограничные случаи: полностью quoted следующая опция после флага, скалярный port в динамической фабрике, enum lookup и очистка поля после quoted parse. Исправления прошли повторное независимое ревью и 985 тестов; нативная проверка и совместная сборка учитываются отдельно.

Общий review всего диапазона дополнительно выявил неполноту этого исправления при повторных переходах text/designer. В этом fix pass сохранить группировку quoted root-relative `/Program Files/...` и dash-leading `-team lists/...` путей с раздельным и inline аргументом. Пробелы до первого `=` внутри quoted токена означают grouped value, а пробелы после `=` не мешают распознаванию настоящей целиком quoted опции. У неизвестного inline аргумента сохранить `=`, если значение само похоже на опцию: иначе `--blacklist="--port=1443"`, целиком quoted `"--blacklist=--port=1443"` или bare inline вариант при следующем parse могут включить известную настройку port. Явная inline принадлежность важнее прежней нормализации её в пробел. Раздельно записанные quoted значения, похожие на опции и не содержащие пробелов до первого =, остаются лексически неоднозначными; не вводить schema/arity inference или новый общий parser. Прежние unquoted separate dash/slash boundaries и `-p "--port=1443"` сохранить. Верифицировать не менее трёх последовательных Parse/Build посещений. Это уточнение уже согласованного исправления потери quoted values, не новый формат пресетов.

## Следующий этап: результаты и отчёты BlockCheck2

Выделить из BlockCheck2ViewModel самостоятельный BlockCheck2RunResultViewModel: OperationError, HistorySaveError, Issues, LastResult/LastReport/LastSession, вычисляемые признаки отчёта, lazy text/JSON и экспорт. Существующие BlockCheckReportBuilder и BlockCheckReportSerializer вызываются через IBlockCheck2ReportOperations и тонкий production-адаптер. Публичный конструктор и свойства основной ViewModel не меняются; sender уведомлений остаётся основная ViewModel.

ApplyRunResult сохраняет исходный порядок: сначала LastResult, затем Build с актуальным SelectedRunPreset, затем LastReport и session с актуальными timeout/grace, затем Issues и прежняя последовательность уведомлений. Не превращать это в атомарную транзакцию и не делать ранний snapshot параметров. Prepared Issues назначаются без уведомления, чтобы host по-прежнему уведомлял Targets → TargetCount → Issues. NoBypassRequired читает именно Issues отчёта с ordinal-сравнением кода; HTTP-only требует непустой набор targets, только Http.

Reset принимает callback существующего progress.Reset: сначала очищает все поля результата/ошибок, затем progress сбрасывает поля, после этого идут OperationError → HistorySaveError → результат → уведомления прогресса. Одинаковые ошибки и повторный пустой Reset продолжают уведомлять. Отсутствующий отчёт при экспорте бросает то же синхронное InvalidOperationException до вызова serializer; путь, token, task identity и ошибки backend не меняются.

Host сохраняет orchestration StartAsync/Cancel, CTS, историю/логирование, generated-site-list cleanup, выбор финального состояния и установку финального состояния только после await истории. Файловые форматы, report builder/serializer, AddOns/Core и границы другой сессии не изменяются. Проверить pure owner и production-адаптер linked source, полный suite, GUI и независимое сравнение с aeeba14.

## Редактор ручных назначений окна результатов BlockCheck2

Вынести из существующей публичной BlockCheck2ResultViewModel (565 строк) редактор BlockCheck2ManualAssignmentEditor: добавление/удаление/перемещение/очистку назначений, совместимые site-list targets и пути, seed автоматических назначений, ключи автоматического выбора, порядок строк и отметки выбора evidence. Он использует существующий Draft.Assignments, live-провайдеры session/evidence и callbacks очистки acknowledged issues и RebuildManualPreset. Форматирование действий остаётся прежним Zapret2ConfigWriter; тесты могут подставить функцию, чтобы проверить исключения и порядок.

Сохранить различия ordinal/ignore-case в duplicate matching и ключах, фильтрацию ignored targets, текущую культуру сортировки имён файлов, исходный порядок primary/fallback/scope, пропуск отсутствующих strategy/target и последовательности коллекций/callbacks/Draft.LoadAutomatic. При повторном входе читать актуальные session/evidence там же, где host читал поля; не делать ранние snapshots. ClearAutomaticKeys отдельно вызывается при Load(report) на прежнем месте, чтобы не менять collection-event timing.

Публичная модель и её constructor/method/property signatures сохраняются; она продолжает строить manual preset, группировать/локализовать issues, запускать manual tests и публиковать прежние PropertyChanged. Существующий BlockCheck2ManualIssueItem переносится целиком в собственный файл без изменений конструктора/локализации; остальные модели общего файла остаются чистыми production-моделями для linked-source тестов. Новые файлы создавать только после проверки отсутствия пути; существующие классы не заменять.

Проверки: absent-API RED, реальные модели Draft/assignments/evidence/rules, mutation/order/seed/failure/reentrancy/path/culture scenarios, полный suite, GUI x64, независимое ревью против 59a5e8f и релевантный нативный проход в изолированном стенде. Процессные сервисы, AddOns algorithms, форматы и security-код не менять.

## Координатор операций документа ConfigMaker

Вынести из ConfigMakerViewModel.Document.cs порядок LoadPresetDocument, LoadConfigItem, CreateConfigItem, SaveToApplicationAsync, NewDocumentAsync, SaveTextAsync и синхронизацию документа/сохранённых ресурсов в ConfigMakerDocumentCoordinator. Существующий экземпляр ConfigMakerPresetDocument и обе observable collections сохраняются. Host предоставляет узкий IConfigMakerDocumentHost для актуальных свойств редактора, UI/callback-ов и прежних методов attach/autocorrect; сам coordinator не зависит от WinUI, базы данных или файловой системы.

Сохранить последовательность metadata/clone/ComponentId/editor/structure/designer/event при загрузке, включая self-load и повторный вход из событий; два Sync вокруг AttachExistingPresetFiles перед диалогом; создание нового storage service только после ненулевого ответа диалога. IConfigMakerDocumentStorage повторяет SaveAsync(name,document)/OverwriteAsync(document); существующий сервис реализует этот интерфейс без изменения тел. ExportAsync передаётся прежним статическим методом. Не добавлять snapshot, блокировку параллельных сохранений или rollback: backend продолжает сам делать прежний clone, а host применяет результат к актуальному документу после await.

NewDocument проверяет readonly только до await, подтверждает только текст/variables/resources, сохраняет прочую metadata и очищает editor/base/name/collections/structure/undo в прежнем порядке. SaveText ловит исключения chooser/sync/export/успешного уведомления; SaveToApplication по-прежнему их не перехватывает. CANCELED сравнивается ordinal; сохранённые resources сопоставляются по первому alias ignore-case, пустой path не обновляется. Сообщения и локализация остаются вызваны в прежних точках; DocumentStateChanged публикуется от host.

Для linked-source тестов подключить настоящие PresetDocument/Converter/SaveResult/Request. Единственный вызов унаследованного static GetNameOnOffValuesFromConditionString в converter квалифицировать непосредственно объявляющим LScriptCore, чтобы убрать зависимость чистой конверсии от WinUI-локализатора; само тело метода и алгоритмы converter не менять. Проверить legacy conditional-конверсию реальным тестом. Автокорректор, storage/export I/O, форматы, Core/AddOns и security WIP остаются прежними.

## Замена отсутствующих файлов пресета

Выделить ConfigMakerPresetFileReplacer из наиболее сложной части ConfigMakerViewModel.PresetFiles.cs: ReplacePresetFileAsync, auto/manual выбор, TryCreateEmptyPresetFile и TryApplyPresetFileReplacement, включая три сообщения результата. Это общий workflow диалога и тихого импорта. Открытие/add/remove/insert остаются в host для отдельного следующего прохода; переносить весь блок в новый столь же смешанный класс не требуется.

Replacer зависит от IConfigMakerPresetFileReplacementHost (прежний RunReportingErrorsAsync, выбор файла, localization/status, structure и explicit file array, setter CommandText, событие) и IConfigMakerPresetFileReplacementBackend (TryResolvePath, FileExists, CreateEmptyFile, TryUpdateAttachedResource, ReplaceReferences, CreateAutoCorrector). AutoCorrector предоставляет ShouldSuggestEmptyFile и FindReplacementAsync; production wrapper создаёт прежний ConfigImportResult синхронно, затем вызывает тот же FindReplacement на Task.Run. Создавать wrapper/ConfigImportAutoCorrector только после выбора AutoCorrect, как исходный код.

Сохранить null/blank/exists guards, неизвестное значение выбора как отмену, отсутствие manual fallback после ненулевой неудачной suggestion, CreateNew semantics, точки catch и silent showStatus=false. Attached update не меняет command/explicit array и не публикует PresetFileReplaced. При переписывании ссылок сначала обновить актуальный explicit array через ReferenceEquals или kind+PathsEqual, затем при CommandChanged отформатировать и назначить CommandText, перестроить структуру и вызвать прежнее событие от host с актуальной командой. Чтение CommandText и new EventArgs остаются внутри условного PresetFileReplaced?.Invoke в host, чтобы сохранить точку чтения и отсутствие чтения при отсутствии подписчика.

Новые интерфейсы не добавляют файловую политику, snapshots, транзакции или rollback. Настоящие filesystem/resource-registry/autocorrect/shell алгоритмы не менять; backend только делегирует прежним операциям. Проверки выполняют ветки и порядок на фиктивных внешних операциях с настоящими FileInfo/Replacement/Core path/formatter типами; не открывают приложения и не создают/удаляют физические файлы.

## Остальные действия дерева файлов пресета

Выделить ConfigMakerPresetFileActions для AddPresetFiles, OpenPresetFileAsync, ShowPresetFileInFolder, RemovePresetFileAsync, InsertPresetFileReference, picker и missing notice. Он получает прежнюю стабильную document.Resources как IList, узкий UI-host и ленивый backend для facade/File.Exists, text-file preferences и shell. Backend constructor только сохраняет facade; существующие filesystem/settings/shell реализации не меняются. Host сохраняет публичные методы, AddPresetFileCommand и private picker forward, замену файлов и её уже проверенные adapters.

Add передаёт исходный IEnumerable без раннего перечисления/snapshot, использует исходный added count и порядок attach/rebuild/panel/status только при count>0. Open сохраняет site-list-or-txt правило, ожидание application dialog, RememberDialogShown только при true и отсутствие shell-вызова после любого результата диалога. Show-in-folder остаётся синхронным и не добавляет reporting catch.

Remove находит resource один раз до await, подтверждает только referenced, после await удаляет именно прежний объект из той же коллекции и затем rebuild/schedule даже при неуспешном Remove. Это удаление metadata из памяти, не физического файла. Insert сохраняет readonly/null short-circuit, прежний trim и preset:// bypass, порядок exists/full-path/GetOrCreate; resource.Reference читается после rebuild при resourceAdded, чтобы сохранить alias, изменённый обработчиком события. Не добавлять readonly guards к остальным операциям, rollback или новые проверки путей.

Проверки используют настоящие document/resource/file модели и реальные BCL collections/path operations, но фиктивные filesystem/shell/preferences. Сохранить области synchronous exception propagation и RunReportingErrorsAsync; отдельно проверить mutation/await/reentry и ошибки collection subscribers. Actual-host интеграцию с fake внешними границами учитывать отдельно от интерактивных контролов.

## Следующий проход App: файловая маршрутизация активации

Первый App dispatcher оставил file imports на месте. Теперь выделить только ProcessFiles и список пяти поддерживаемых extensions в AppFileActivationHandler, с узким IAppFileActivationOperations и production AppFileActivationOperations. Это выбор маршрута и прежний порядок окон; реальные ConfigShare/conditional/store/update importer implementations, форматы, security/path/trust checks, Program, URI allowlists, migration lock и settings recovery не меняются.

Handler получает исходный string[] и последовательно читает его без snapshot: для каждого элемента Replace всех двойных кавычек на пустую строку (не Trim), затем исходный debug log, Path.Exists и extension whitelist OrdinalIgnoreCase. Path.Exists остаётся Path.Exists, не File.Exists. До существования/whitelist нельзя вызывать ConfigShare.IsSupported. Сначала ConfigShare, затем cdpitask, затем cdpipatch, иначе store pack. Обрабатывается только первый подходящий файл; следующий не читается, пока ожидается его маршрут, и после успеха больше не читается. Null array/element и backend failures остаются faulted async task без нового catch/fallback.

Production operations constructor только сохраняет App и прежний private conditional callback. Share: await UnsafeCreate<ConfigShareImportDialog>(activate:false), Activate, await SetFileAsync. Patch: await SafeCreate<ApplicationUpdateFileDialogWindow>(activate:false), SetUpdateFilePath, Activate. Store: await SafeCreate<StoreLocalItemInstallingDialog>() с прежним activate default и fire-and-forget SetPackFilePath; не ждать completion самого импорта. Conditional delegate сохраняет исходный private метод целиком. Log source остаётся nameof(App). В App остаётся ProcessFiles forward для прежнего activation dispatcher; новый owner/backend создаются до InitializeComponent без обращения к settings/windows/importers.

Characterization проверяет whitelist/case/quote normalization, guard/order/first-file, route precedence, async task and exception/cancellation propagation, ожидание всех четырёх route tasks и живое чтение массива после reentry. Тесты используют fake operations и не создают окон/файлов, не импортируют пакеты, не запускают updater. WinUI adapter проверяется компиляцией и отдельным source comparison, без утверждения реального production import. Глубина этого прохода — перенос оркестрации, не изменение политики импорта.

## Следующий проход BlockCheck2: ручной preset state

Выделить BlockCheck2ManualPresetState из публичного BlockCheck2ResultViewModel: RebuildManualPreset, DescribeManualIssueSubject, current manual preset result/Arguments и factory ManualOptions. Owner получает тот же Draft, ту же stable IReadOnlyList ручных ProbeResult, narrow host и Build operations. Production operations лишь делегирует прежнему экземпляру BlockCheckManualPresetService. BuildEvidence/strategy testing, ручной список probe results, acknowledged issue keys и локализованный ManualIssueItem остаются host; сетевые/component callbacks не переносить.

Host сохраняет коллекции и публичный API, private rebuild forward, порядок Load и result clear. ClearResult вызывается ровно на месте прежнего `_manualPreset = null` после ManualIssues.Clear; никакой объединённый reset не переставляет события. В constructor owner/backend только сохраняют зависимости; assignment editor callbacks остаются прежними host callbacks. ManualPresetArguments читает owner.Arguments без новых уведомлений. ManualOptions в host становится forward к одной чистой factory OptionsFor, сохраняя fresh allocation, Math.Max(1,MinimumAttempts) и неизменённый MinimumSuccessRate.

Rebuild начинает с host.ClearIssues, затем читает актуальный session и assignments. Пустая ветка очищает result, refresh indexes, опционально ApplyStructuredChange(empty), refresh evidence, NotifyManualState. Непустая ветка формирует deferred probe overlay (StrategyId OrdinalIgnoreCase, TargetId Ordinal), append ручных результатов, вызывает Build, назначает result ДО issue callbacks, группирует issue по точным Severity/Code/Message в исходном порядке. Host.AddIssueGroup создаёт прежний локализованный item и проверяет тот же ordinal acknowledged key до добавления. Describe subjects остаётся deferred до вызова конструктора item: scope match раньше strategy match, затем slash-список и исходный fallback; существующие culture/path правила не менять.

Непустая ветка опционально применяет current owner result к draft ПОСЛЕ issue callbacks, затем indexes/evidence/notify. Не сохранять ранний snapshot session, assignment list или current result: reentrant callback может изменить их в прежний момент. Deferred targets predicate читает актуальный session ignored ids, а source targets/probes и options вычисляются как в исходном вызове. Исключения выходят синхронно, ранее выполненные мутации не откатываются. Assignment edit/selection, QuickFix/ack semantics, test result replacement, IssueItem/localization и domain builder не менять.

Unit-тесты используют настоящие Draft/assignment/domain types и fake host/build ports; real IssueItem и WinUI localizer не подменять ради компиляции. Проверить обе разные последовательности веток, updateDraft true/false, expert/automatic state, overlay/comparers/live deferred enumeration, options/grouping/subjects, reentry и partial exceptions. GUI build и independent source review подтверждают host adapters; actual public-model runtime с настоящим localizer/pure builder рассматривается отдельным этапом, без live strategy tests или сети.

## Следующий проход BlockCheck2: ручной strategy test controller

Выделить GetFullStrategyArguments и TestStrategyAsync в BlockCheck2ManualStrategyTestController. Передать тот же Draft, ту же List<ProbeResult>, getter текущего session, исходный RebuildManualPreset callback и узкий operations port. Production adapter только хранит прежний BlockCheckManualStrategyTestService и напрямую делегирует его sync GetFullStrategyArguments / TestWithCdpiuiAdaptersAsync. Конструкторы не обращаются к зависимостям. Public model signatures и исходная service initialization остаются прежними.

Синхронный GetFullStrategyArguments по-прежнему без session guard: сначала item.Evidence.Strategy/Target, затем текущий session.Scan.AttemptsPerTarget с Math.Max(1,...), fresh options. Async Test сначала проверяет session, при отсутствии возвращает fresh result/issues с исходным MANUAL_SESSION_UNAVAILABLE, даже для null item или canceled token. Затем исходный порядок item/options/session timeouts/grace/token, await ConfigureAwait(true). Никаких Task.Run, locks, validation, catch, snapshot, дополнительной проверки отмены или смены session.

Если результат содержит ProbeResult: RemoveAll по StrategyId OrdinalIgnoreCase и TargetId Ordinal, Add того же probe, item.ApplyTestResult, затем чтение текущего Draft.IsAutomatic и вызов rebuild(!IsAutomatic). Все уведомления item идут до rebuild; observer может изменить draft/session/list. Pending Load и перекрывающиеся тесты сохраняют прежнее поведение применения по порядку завершения, включая stale item и current host session. Ошибки/отмена/частичные мутации выходят без rollback. Probe-null result не меняет состояния.

Characterization использует настоящие EvidenceItem/Draft/domain, fake operations и управляемые tasks, без real backend execution/network/components. Проверить sync/async failure boundaries, identity/options/evaluation order, cancellation ownership, current-context continuation, overlay comparers/order, live draft/session/list changes и partial callback failures. Compile реального production adapter и source review подтверждают прежний service call; фактический запуск стратегии остаётся вне проверок.

## Следующий проход ConfigMaker: операции с переменными

Выделить ConfigMakerVariableActions с тем же ConfigMakerPresetDocument и узким IConfigMakerVariableActionsHost. Owner содержит Edit/Remove/Create/Insert; host сохраняет public signatures, RunReportingErrorsAsync вокруг Edit/Remove и CreateVariableCommand, private Create forward и исходный error logger/status. Конструктор owner только сохраняет зависимости. Реальные document/variable models и коллекции не меняются. Host adapter делегирует прежние view/localizer/editor/attach/tree/diagnostics callbacks, не переносит их побочные эффекты в owner.

Edit: null guard, await view.Edit(document,originalVariable), null result guard, IndexOf исходного объекта ПОСЛЕ await, missing guard. Затем ordinal сравнение result.OriginalName с result.Variable.Name, ReplaceVariableReference в том же документе, SetEditorText с текущим document.CommandText ПОСЛЕ document events, присваивание по ранее вычисленному index, Attach, Rebuild, Schedule. Не клонировать result, не искать повторно index после reentry, не валидировать имена/дубликаты сверх прежнего UI.

Remove: null guard, host.CommandText.Contains(variable.Reference, OrdinalIgnoreCase) до await; только при referenced ждать исходное localized confirmation. После согласия/отсутствия ссылки Remove исходного объекта, Rebuild, Schedule, даже если Remove вернул false. Ссылки из command не удалять. Create: await view.Edit(document,null), null guard, Add того же result.Variable, Attach, ShowVariablesTool, Rebuild, Schedule. Insert: IsEditorReadOnly читается раньше null guard, затем текущий Reference и прежний InsertAtCursor.

Никаких snapshots, новых guards, locks, rollback или catch в owner. Документ может перезагрузиться/измениться во время await; collection/content observers могут изменить коллекцию после вычисления edit index. Сохранить исходную частичную мутацию и порядок исключений. Unit characterization использует настоящие document/variable events, fake host и managed tasks без dialogs/files/network. GUI compile и независимое source review подтверждают реальные adapter/localizer/error wrapper/command bindings.

## Следующий проход BlockCheck2: run lifecycle controller

Выделить StartAsync, Cancel, CTS field и private GetFinalState в BlockCheck2RunController. Host сохраняет State, Targets, options/progress/results и все backend/history/generated-root/cleanup endpoints. IBlockCheck2RunHost предоставляет только исходные шаги workflow и текущие CanStart/CanCancel/LastReport; constructor controller только сохраняет host, создаётся в конце исходного host constructor. Публичные StartAsync/Cancel становятся forwards, sender и порядок всех host notifications прежние.

Start сначала читает CanStart и при false возвращает null без finally. При прежнем LastReport обновляет generated root, затем ResetResultState и progress.StartRun ВНЕ try. В try Prepare с исходным CreateSessionRequest; ApplyPrepared adapter содержит прежние assignments Targets/Issues и три Notify в исходном порядке. При invalid — InputInvalid/null. Иначе локальный CTS назначается в owner field ДО State.Running, await прежнего RunPrepared endpoint, ApplyRunResult, CompleteDisplayedProgress, вычисление final state ДО await SaveHistory текущего LastReport, затем SetState(final) и возврат того же result. Пока history pending, прежний running state блокирует новый запуск.

Catch OCE назначает Canceled/null; catch Exception — прежний SetOperationError(message), Failed/null. Никаких новых catch/validation/rollback/locks/cancellation checks. Finally: FinishRun, dispose локального CTS, field clear ТОЛЬКО по ReferenceEquals текущего field и local, CleanupIfUnviewable, NotifyActionState. Сохранить порядок даже при исключениях callbacks и reentry нового запуска на final-state event; finally старого запуска не очищает CTS нового. Cancel читает CanCancel до null field guard, устанавливает Canceling, затем вызывает текущий field.Cancel, сохраняя reentry и synchronous callback exceptions.

Unit characterization использует реальные Prepared/RunResult/Report types, fake host и управляемые tasks. Покрыть prelude вне try, invalid/success/final-state precedence, pending history и captured final state/current report, exception/cancellation boundaries, reentry/CTS lifetime, cancel before backend и cancel callback exceptions, partial finally и captured SynchronizationContext. Реальные Prepare/runner/network/history/logger/cleanup/deletion не исполнять. Source review и GUI compile подтверждают host adapters; optional actual-host runtime требует отдельного безопасного fixture design.

## Следующая крупная ViewModel: CreateNewConfig condition draft

Выделить из CreateNewConfigViewModel.Variables.cs четыре condition draft fields/properties, ConditionPreview, IsConditionSaveAvailable, ResetConditionDraft, AddCondition и draft notifications в CreateConfigConditionDraft. Получить ту же ObservableCollection<ConditionVariableModel>, lazy callbacks исходных validation/CreateCondition/localizer и SetExitAskAvailable(true). Constructor только сохраняет зависимости. Host сохраняет public API/collections, forward property notifications с прежним sender, RemoveCondition/RemoveVariable/VariableChanged и все save/import/test workflows.

Сеттеры нормализуют null в пустую строку, публикуют собственное имя при изменении, затем Preview/IsSave для Name/On/Off; Description только своё имя. Preview сначала текущий IsSave, затем вновь читает текущие Name без пробелов/On/Off для formatter; invalid branch lazily localizes NothingToPreview. Не добавлять guards/snapshots/caching. Reset вызывает четыре setters в прежнем порядке Name/Description/On/Off, затем безусловный Preview. Add сначала exit-ask callback, затем создаёт fresh model из текущих RAW полей и добавляет в ту же collection, без IsSave guard/reset/trim; callback может изменить draft до чтения.

Новый owner strict nullable; исходная POCO model сохраняет annotations-only GUI context при source-link в tests. Unit tests используют настоящую collection/model, spies для validation/format/localizer и один настоящий Core formatter; без WinUI stubs, dialogs, storage, Logger или component execution. Public getters/setters остаются plain forwards без implicit interfaces, чтобы сохранить CLR flags.

## Возобновление: test session CreateNewConfig

Выбор исполнителя: следующим отдельным владельцем станет CreateConfigTestSession из CreateNewConfigViewModel.Testing.cs. Он владеет running/current-id, button text/glyph, подпиской и start/stop/process-state orchestration. Конструктор хранит callbacks; начальный текст присваивается без уведомления в прежней точке host constructor. Host сохраняет публичные getters/private setters и sender, AsyncRelayCommand, SelectedComponent, конвертацию designer/CreateConfig, startup argument formatter, localizer и DispatcherQueue через ленивые callbacks. Реальные ComponentTasksManager endpoints остаются в узком adapter с event/StopAsync/RunAsync, без инициализации singleton при создании adapter.

Сохранить прежний порядок и quirks: unsubscribe перед guard StopTestRun и повторный unsubscribe при reset; локальный runId для fire-and-forget stop; start устанавливает running до remove/add подписки и читает selection после них; существование selection отдельно от null/empty Id. Await Stop перед conversion/create, аргумент runId читается до formatter; возвращённый Run Task не ожидается. State tuple ставится в очередь без раннего guard, сравнивается с текущим id при исполнении case-sensitive; glyph затем localized text затем running. Reset glyph/text до flags/unsubscribe/id-clear. Никаких новых try/finally, locks, cancellation, rollback, restore или guards; прежние частичные состояния при fault/reentry остаются. Никаких изменений Save/Import/GraphicDesigner/service implementations.

Проверка: linked pure production owner/port с настоящим Core ConfigItem; managed fake service и controlled tasks/queue без WinUI, IO или процессов. RED до owner, focused/full suite, GUI x64, metadata comparison на normal DLL, независимый review нового диапазона. Actual host/adapter runtime proof — отдельный следующий gate с inert service boundary; native15/16 и настоящий backend этим не закрываются.

## Возобновление: состояние graphic designer CreateNewConfig

Следующий выбор исполнителя — CreateConfigGraphicDesigner, самостоятельный владелец loaded component id, additional settings и изменений двух существующих коллекций моделей. Host сохраняет коллекции/bindings, всю последовательность переключения вкладок/visibility/view calls и точные чтения/присваивания EditorText. Load получает прежний captured selected id; живой getter HardcodedItemIds передаётся функцией, чтобы позднее чтение id после factory/collection callbacks не превратилось в snapshot. AdditionalSettings продолжает null->empty, equality suppression и host sender. Constructor владельца и backend только сохраняют зависимости.

В отдельном CreateConfigGraphicDesignerBackend оставить прежние конкретные Goodbye/Spoof factories, NoDPI DB directory + Path.Combine(edannotationfile.xml) + File.Exists + XML(nodpi), Parse/Build через неизменённый GraphicDesignerHelper. Не использовать case-insensitive TryLoad от ConfigMaker: у CreateNew исходные сравнения case-sensitive, конкретный порядок branch/cache и иное поведение при неподдерживаемом id. Это тонкие adapters к общему helper, не новая реализация codec.

Сохранить строки алгоритмов: при отсутствии NoDPI annotation cache не обновляется и попытка повторяется; cache после successful callback читает текущий mapped id; exception/reentry не получает rollback. Text/bool выбирают первый standalone либо первый matching child первого matching exclusive group; GUID case-sensitive, group selection без дополнительной validation/check state; прежние null/unknown/partial-model эффекты сохраняются. Parse возвращённое additional значение присваивается после model callbacks; Build получает additional до backend callback. Никаких новых dirty/readonly/normalization/guards. Реальные модели + fake backend тестируют contract без WinUI/DB/XML, код adapter компилируется и проверяется review; следующий actual-host gate использует реальные safe factories/codec и инертные NoDPI IO targets.

## Возобновление: component selection CreateNewConfig

Выбор исполнителя: CreateConfigComponentSelection владеет SelectedComponent, DisplayName, тремя связанными visibility flags (additional/not-ready/tabbar), вычислением IsDesignerSupported/IsSaveAvailable и порядком CheckViewType/dirty/audit. Другие visibility, folder buttons, title/mode/navigation/save/IO остаются в host. Семь ленивых callbacks: supported-id predicate, select designer/text tab, HandleSelectionChange(bool), mark exit ask, audit commands, read current EditorText. Это coordinator выбора компонента, не общий property bag. Конструктор не вызывает view/predicate/command callbacks.

Host сохраняет прежние public types/private setters, bindings и sender через plain forwards. Приватный CheckViewType остаётся forward для Navigation. Дополнительные уведомления computed properties не добавляются. Сохранить equality по identity, null->empty только DisplayName, пустой/null selected ID отдельно от отсутствующего объекта; CanSave проверяет наличие объекта/name/editor без trim/ID validation и читает editor только после shortcircuit. Selected setter сначала notify, затем CheckViewType, dirty, audit. Check сначала null guard, затем additional/notready, затем текущий supported predicate; supported выбирает tab до tabbar=true и Handle(false), unsupported вызывает Handle(true) до selectText и tabbar=false. Callback/reentry/observer exception сохраняют прежние частичные состояния и продолжение внешнего setter без нового rollback/revalidation.

Linked tests используют настоящий ConfigComponentOption; его annotations-only GUI context явно сохраняется при source link, без изменения модели/nullable policy проекта. Нового интерфейса на public host не вводить (public CLR flags сохраняются). Реальный view и методы designer остаются ленивыми host targets; actual-host следующий gate использует текущие16inert boundaries без новых backend/service cuts.

## Следующий проход ConfigMaker: diagnostics panel

Выделить сбор и обновление Diagnostics, error/warning counts и локализованное представление диагностик в ConfigMakerDiagnostics<TItem>. Generic internal owner сохраняет ту же ObservableCollection<TItem>; actual host закрывает TItem настоящим ConfigMakerDiagnosticViewModel с WinUI Brush. Unit tests используют производственные diagnostic/help/variable/validator types и явную factory projection, без WinUI stubs или изменения модели UI. Конструктор только хранит collection, lazy EditorText/options getters, ту же Variables collection, resource predicate, item factory, localizer и highlighting callback. Timer/debounce, private RebuildDiagnostics entrypoint/callers и GUI adapter остаются в host.

Rebuild читает EditorText один раз, затем текущие options; Validate(...).ToList, затем AddDiagnostics с текущими variables/resource resolver, всё ДО Clear. После Clear создаёт и добавляет по одному item в прежнем порядке. Host item factory сохраняет object initializer Source, SeverityText, Description; локализация severity/description принадлежит owner, host лишь присваивает результаты. Для неизвестного severity используется warning label; для неизвестного kind прежняя unterminated-quote строка. Все прежние форматные строки/параметры и культура String.Format сохраняются. После collection callbacks вычислить error count по Severity.Error, warning=results.Count-error (включая неизвестный severity), локализовать/присвоить ErrorCountText, затем WarningCountText, затем RefreshEditorHighlighting. Никаких новых notifications/equality policy/sort/cache/catch/rollback/guards/snapshots.

Synchronous collection/property/localizer/view callbacks могут reenter Rebuild или изменить текущие model inputs. Сохранить локальный outer results и частичные mutation/exception paths; при Validate/reference failure предыдущие collection/counts остаются, при factory/Add/localizer/counter/highlight failures присвоенное сохраняется. Counter notifications пересылаются синхронно с прежним host sender, private setters plain forwards сохраняют CLR shape. Аннотационный контекст существующего pure validation source при source-link сохраняется явно; глобальная nullable migration не входит. Actual view/Timer/native callback/runtime integration доказываются отдельным gate, новые реальные IO/DB/logger endpoints не исполняются.

## Следующий проход исходного God-control: resizable panel layout

Выделить из ConfigMakerUserControl.Layout.cs алгоритм UpdateLayoutBounds и состояния layoutInitialized/updatingLayoutBounds/savedEditorHeight в ConfigMakerPanelLayoutController. Чистый IConfigMakerPanelLayoutView передаёт только bool/double properties, причём все чтения/присваивания остаются в прежней последовательности, без входного snapshot или предварительного расчёта всех выходов. Отдельный WinUI ConfigMakerPanelLayoutView хранит девять lazy getters для Root/files/command/bottom/workspace/editor FrameworkElement и двух ColumnDefinition/одной RowDefinition; constructor только сохраняет callbacks. В control owner и adapter создаются первой строкой constructor ДО ViewModel/InitializeComponent, чтобы ранние XAML callbacks видели тот же default state. Реальные controls через callbacks читаются только при обращении. Host сохраняет public flags/панельные show-hide/tab/events, ShowPanel, dead savedEditorWidth assignment и private UpdateLayoutBounds forward, без нового host interface/partial.

Controller guard сохраняет shortcircuit updating || rootWidth<=0 || rootHeight<=0, затем флаг true и прежний try/finally. Константы360/340/180/110, files max=Math.Max(150, rootWidth-editorMin-(commands?commandMin+sizer:0)-filesSizer), cap45%, clamp/current min/max, затем available main width/editor MaxWidth→Width и height MaxHeight→Height точно как раньше. Command-panel width остаётся прежним max-width, dead savedEditorWidth не начинает влиять на поведение. Bottom height выбирает текущую Height только после initialized, иначе min(saved420,max); CompletePanelChange invalidate перед Update и event. Hide bottom запоминает ActualHeight в прежней точке. SizeChanged guard использует owner.IsUpdating, finally сбрасывает его даже при property/callback exception; initialized=true только после всех assignments. Не добавлять нормализацию NaN/inf, min/max guards, rollback или catch.

Unit characterization использует pure scalar port/spies/reentrant controlled callbacks без WinUI stubs или view IO: all branches/minimums/caps/clamps/initial-vs-current height/hidden panels/repeated live reads/order/shortcircuit/exception-recovery/reentry. Реальные UI property validation и Window visual delivery этим не заменяются. Normal GUI compile/metadata/source plus fresh review проверяют mapping к исходным XAML elements. Actual adapter with standalone WinUI elements в следующем безопасном gate, full control/native15/16/visual resize remain separate.

## Следующий проход App: window close lifecycle

Выделить workflow Window_ClosedHandler в AppWindowCloseCoordinator<TWindow> с той же List<TWindow> и узким IAppWindowCloseOperations<TWindow>. Host оставляет sender is Window/e.Handled guards и приватный forward, Window_SizeChanged/Register/Activate/modal implementation/migration policy не меняются. Separate AppWindowCloseOperations для настоящего Window хранит modal coordinator, detach callback, createPrepare callback и ExitIfIdle callback; constructor только сохраняет зависимости и создаётся в App до InitializeComponent, без обращений к Window/IO.

HandleClosed сначала захватывает enqueue closure с текущим window.DispatcherQueue, затем вычисляет bool migrationWindowClosed через прежний WelcomeWindow/Id OrdinalIgnoreCase. Try SavePosition, catch warning, finally: detach Closed→SizeChanged в одной последовательной host callback; try ReleaseModal(isClosing:true)/catch warning; try sameList.Remove(window)/catch warning; if captured migration bool CompleteMigration; затем ForgetOwner. Сохранять частичные finally и исключения самого logger/cleanup. После finally captured migration: попытка Low enqueue ExitIfIdle, при false sync ExitIfIdle, return. Иначе при текущем list.Count==0 && current StoreWorking fire-and-forget original SafeCreateNewWindow<PrepareWindow>(activate:false); иначе тот же enqueue/fallback. Никаких новых guards/await/locks/catch/snapshot/rollback и актуализации captured migration/dispatcher после callbacks.

Pure owner не знает WinUI, Window handles, settings или TaskMonitor. Real adapter вызывает прежние WinUI/event/modal/position/logger/Program/backend methods; исходный ExitIfIdle policy остаётся App. Сама List фиксирована readonly currentApp constructor и уже передаётся WindowCoordinator; private OpenWindows setter в приложении после initial assignment не используется. UI event args/handler guard и реальные backend effects проверяются отдельно, не через unit stubs. Unit tests используют настоящую List, managed tasks/objects/closures с ожидаемыми fault/reentry boundaries. New public host interface не вводится, CLR flags не меняются.

## Следующий проход BlockCheck2: site-list workflow

Выделить loading/loaded/error, две существующие коллекции и Load/AddCustom/UpdateCustom/Select/Remove/SetSelected и computed counts из SiteLists partial в BlockCheck2SiteListController<TItem>. Generic owner работает с настоящими ObservableCollection<TItem> и реальным SiteListElement из AddOns, через IBlockCheck2SiteListBackend<TItem> выполняет legacy/factory/normalization/path/write/localizer и чтение свойств TItem. Production closed TItem=BlockCheck2SiteListItem; новый тонкий backend вызывает те же SiteListHelper/BlockCheck2SiteListStorage/real item методы. Не менять SiteListItem/Storage ради compile unit tests; тесты явно проецируют fake row port, без WinUI stubs. Generated root, preparation preview/run/cleanup/CreateSessionRequest остаются host; generated-root getter читается лениво на каждой factory call.

Load guard loaded||loading. Вне try назначить loading=true/error=null и безусловно Notify IsSiteListLoading затем SiteListLoadError. В try await LoadAvailable, Clear, затем ленивые Where nonblank Directory/!legacy и OrderBy PackName/ThenBy Name CurrentCultureIgnoreCase над теми же mutable SiteListElement. Не создавать DTO snapshots и не сортировать раньше Clear. Factory получает текущий Name??Path.GetFileName(Directory)??fallback/PackName??empty/Directory/generated root; затем DomainCount>0 && !DisplayName.Contains("exclude") case-sensitive before Add. loaded=true после всех rows; catch Exception (включая cancellation) error=Message/Notify; finally loading=false/Notify. Partial/reentrant/notification exceptions сохраняются, включая prelude outside try.

AddCustom сначала Path.GetFullPath, поиск Available.Concat(Selected) OrdinalIgnoreCase по текущим paths; existing возвращается по reference. Иначе new factory item из filename/source/fullpath/current generated root, без добавления в коллекции. Update нормализует, empty→тот же localized InvalidDataException; находит первый текущий IsCustom ДО await, captured path либо CreateCustomPath, await Write с исходным token. Затем если item absent создать real new custom row с normalized content/current root и Insert(0), иначе UpdateCustomContent исходного captured item (даже если он удалён за время await); затем прежний selection notify, return sameitem. Никаких locks/snapshots/reselection/cancellation-rechecks/rollback.

Select/Remove сохраняют ThrowIfNull(item), case-insensitive path dedup, Notify только после фактической mutation; SetSelected Clear перед ленивым DistinctBy, включая same-source self-clear/null input partial effect. Selected counts живые Count/Sum(DomainCount). Selection callback остаётся исходным host NotifySiteListSelectionChanged (четыре имена/порядок), loading/error notifications пересылаются с host sender. Backend/owner constructor store-only, same collections; нового host interface/partial нет. Реальные IO/DB/cleanup remain source-only до отдельного безопасного runtime gate.

## Оставшиеся native gates на текущем pin

Свежий Sky list_windows после Task52 показывает5targetable windows, без consent/CDPIUI/Smoke candidates; прежний UAC blocker больше не наблюдается. Перенести corrected921 native-сценарии в НОВУЮ копию текущего214b4b3 на F, старую не запускать/не менять. Standalone SmokeWindow создаётся вручную, с собственным Closed→App.Exit и записью в собственный App.OpenWindows, без App.Register/position/modal lifecycle. Все actual production controls/pages/owners остаются. Program/App startup/DirectoriesManager/help/task manager/output/settings/DB-relative resources/catalog/scan/manual tests/storage/export/filepicker/shell границы ниже инертны, без userdata/backend/actions. Root data ровно own executable bin, без fallback. Real Frame+MenuBarHost.SetFrame и nonnull MenuBarSession обязательны ДО mounting ResultPage (исправление известного harness-only stackoverflow).

Native Task15: LoadSession controlled two-strategy TLS12/IPv4 report, сохранить actual collection aliases и references. Четыре observation phases: empty; click реального Add for site split; click Add disorder; click реального Move down для split. Проверить order/1-based indexes/same objects/Draft origin/evidence flags/CanUseConfig/canonical serialized actions/порядок host notifications. Helper лишь загружает и проверяет; assignments меняются исключительно production UI actions через Sky.

Native Task16: реальный ConfigMakerUserControl и document coordinator, actual dependency-property/editor binding, коллекции/variables/resource tree. Controlled preset содержит одну variable MODE и attached sites resource, command на собственный fixture file. Реальная SavePreset ContentDialog: cancel без persistence и success с отредактированным именем; public real storage SaveAsync/OverwriteAsync+Clone/Task.Run остаются, только private SaveCore backend memory result. Export path fakeown .cdpiconfig и exporter memory recorder, no zip/text write; validate allowText=false/current document. Реальная New confirmation cancel/continue, readonly no-op и cleared text/name/variables/resources с сохранением collection identity/остальной metadata. Один initial CreateNew own input file и append-only own log допустимы; input retained byte-identical/no physical export. No user settings/real databases/shell/components/probes.

Native UI inputs только через Sky в точном observed own window; сначала snapshot/focus, потом один ввод/клик+refresh. Не взаимодействовать с чужими UAC/auth/security windows. Ошибки sticky, Finish возможен только после всех11distinctPASS и noFAIL, затем COMPLETE/native process exit0. Own30minute watchdog exits2/failure on abandoned fixture, не общее временное ограничение задачи; watcher checks in<=1s intervals. Full source/build input/manifest/ownroot/fresh files independently reviewed beforefirstlaunch. Native warnings/bugs must distinguish harness/baseline/product via evidence, no automatic green from programmatic86.

Предпусковое ревью добавило семнадцатую явную границу: вложенный SearchUserControl создаётся из реального XAML и читает SEARCH settings в ctor даже при Collapsed. Два чтения заменены false, два persistence Click target отказом; XAML и сам search control сохранены, реальный SettingsManager не запускается. Первые16cut manifest/DLL сохранены, до исправления ни одного запуска.

Первый17cut native run дал11UI PASS/COMPLETE/exit0, но postgate обнаружил собственный Settings.xml/bak из ConsoleFontHelper x:Bind. Общий gate не принят. В НОВОЙ native2копии добавляются18ConsoleFontHelper6settings-targets(defaulttrue/Consolas/16) и19Core.SettingsManager failfast baseargument доXMLbase. Он выставляет process-local AppContext switch и бросает исключение; GUI проверяет sticky switch вFinish и ПОСЛЕ Application.Start доreturnexitcode, чтобы swallowed binding/closing ошибки не дали green. Никакой IO в guard. Все17старыхcuts/реальные XAML/UIсценарии сохраняются; перваякопия/evidenceнеперезапускается/неудаляется.

## Продолжение: владелец состояния деревьев ConfigMaker

Пользователь снова просит продолжить после28ceef4. Следующая самостоятельная обязанность исходного ConfigMaker — хранение explicit/detected/attached структуры и порядок публикации. ConfigMakerPresetStructure<TNode> хранит состояние, использует реальные FileInfo/GroupInfo/CoreConfigCommandLine/KeyComparer; преобразование в WinUI nodes остаётся существующим TreeBuilder/Adapter, callbacks ленивые. Без изменения файлов/профилей/UI/порядка уведомлений или защиты от reentry. Host публичные объявления и оба прежних private node setters сохраняются; PropertyChanged sender остаётся host. Current command читается отдельно до extract и после file-node publication; explicit groups и component читаются на прежнем позднем этапе. Частичные изменения при исключении сохраняются. Flags назначаются до group-node publication, availabilityEvent после всех3derivednotifications. Сохранить cold IsVisible/HasGroups true и новые emptyprojectioncollections. Дальнейшая безопасность/доказательство — managedRED/GREEN, обычныйGUI/API, actualcomposition91 и новый native11 на текущемpin; прежние native2/wholebranchreview pins остаются историческими, не объявлять их автоматически проверкой нового продукта. Никакие parallel security/settings/backend реализации не меняются.
