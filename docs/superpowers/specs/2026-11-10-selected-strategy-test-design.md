# BlockCheck2: одиночная проверка выбранной стратегии

Продолжение принятого checkpoint e1bcdf8, в рамках аудита больших UI-классов. Выделить из
BlockCheck2ResultPageViewModel выполнение TestSelectedStrategyAsync и представление
ShowStrategyTestResult в internal sealed BlockCheck2SelectedStrategyTest. Решение о границе принято
агентом по прямому разрешению пользователя продолжать самостоятельно; новые функции не добавляются.

Host сохраняет публичный Task TestSelectedStrategyAsync(), private ShowStrategyTestResult(result),
поле _strategyTestRunning, private setter IsStrategyTestProgressVisible, Text/ShowActionError/
ShowActionMessage и все остальные методы. Новый объект не владеет компонентами или процессами.
Его конструктор только сохраняет одиннадцать портов: selected item, running getter/setter, progress
setter, update selection, test(item), update assignments, text, issue text, error, show notice.
Тестовый порт lambda обращается к текущему Result во время вызова, а не захватывает модель конструктора.

TestAsync читает выбранную строку один раз; null прекращает работу без чтения running. Затем guard
running; captured item используется даже при изменении выбора из уведомления. Установка running,
progress и первый UpdateSelectedStrategy остаются ДО try. Исключение здесь не запускает cleanup.
Внутри try await Test(item), повторный UpdateSelectedStrategy и ShowResult. Catch преобразует любое
исключение этой области в ShowActionError(exception.Message). Finally сбрасывает running, progress,
обновляет selection, assignments именно в таком порядке. Исключения catch/finally сохраняют обычную
семантику C#; новые catch/retry/ConfigureAwait/cancellation/сериализация не добавляются. Pending task
держит running, повторный вызов подавляется, cleanup не происходит до завершения.

ShowResult: без ProbeResult берётся первое issue независимо от severity, иначе fallback failure key.
С ProbeResult сначала вычисляется реальный ProbeSummary и локализуется/форматируется результат,
даже если позднее найден Error issue. Первый Error передаётся issue-localizer/error; без Error
severity Success когда successes == attempts (включая 0 == 0), иначе Warning. Заголовок локализуется
после format и после поиска Error. Используется текущая культура string.Format, issue/localization/
show callbacks могут бросать и повторно входить. Не применять result.Success вместо старого условия.

Проверки: literal traces/state, captured row/current ports, pending/faulted/cancelled tasks, pre-try
faults, catch/finally interruption/exception identity, real ProbeSummary/first issue/first Error/empty
attempts/culture/format failure/reentry. Затем full managed suite, GUI x64 Release, существующие
source/metadata inventory checks. Новый bounded actual host сохраняет прежние107 групп и добавляет4:
реальный host/private forward/порты/empty issues; probe/issue presentation matrix; настоящая
sessionless Result.TestStrategyAsync с guards/finally; pre-try и show fault publication. Реальный
backend не вызывается, новые native cuts не нужны. Перед запуском независимое prelaunch review,
эксклюзивная очередь всех UI API, pins и полные postchecks. Это проверка композиции, не visual/full startup.

Не менять XAML, настройки, форматы, manual component testing, security ветку или чужие worktrees.
Сохранить все старые evidence copies. Коммиты локальные; без удаления/reset/push/merge/publish.
