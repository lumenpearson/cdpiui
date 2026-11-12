# BlockCheck2: жизненный цикл ручного теста

Продолжение аудита крупных классов после принятого0d8ba6f. Агент самостоятельно выбирает перенос
всего ручного теста из ResultPageViewModel.ManualTesting.cs в internal BlockCheck2ManualTestSession.
Пользователь прямо разрешил автономное продолжение без подтверждений. Форматы, XAML и политика
запуска/остановки не меняются; новая синхронизация, validation, cancellation или исправление старых
гонок не добавляются. Работа остаётся в dev/audit-class-decomposition, без интеграции переписанного main.

Owner хранит три прежних bool состояния (_isRunning/_isStartingTest/_restoreComponentAfterTest),
без новых PropertyChanged/StateChanged. IsRunning обслуживает прежний public IsManualTestRunning
и lazy selection port. Host сохраняет все public Task методы, private StopTestOnLeaveAsync и
ComponentTasksManager_TaskStateUpdated. Подписан тот же private host method group: Target=Page,
Method=ComponentTasksManager_TaskStateUpdated. Три прежних private host поля удаляются как внутреннее
устройство; публичный API не меняется. Единственный известный потребитель поля _isManualTestRunning —
старый actual selection helper — получает явно проверенный bridge к owner._isRunning. Старый helper
и архивы сохраняются, все его остальные проверки и тексты115PASS остаются прежними.

Конструктор owner только сохраняет четыре порта: IBlockCheck2ManualTestHost,
Func<IConfigMakerTestTasks> resolveTasks, Func<string> currentComponentId и hostStateHandler.
Каждая операция сначала вызывает resolveTasks, затем вычисляет ID и вызывает метод уже полученного
receiver. ComponentTasksManager.Instance имеет инициализацию с побочными эффектами; прежний порядок
receiver-before-ID обязателен. Новый BlockCheck2ManualComponentTestTasks хранит переданный manager,
переиспользуя существующий IConfigMakerTestTasks. Существующий ConfigMakerComponentTestTasks не подходит:
он разрешает Instance внутри метода, после получения ID. Его код/потребители не меняются.
Интерфейс host описывает SelectedStrategy,
IsStrategyTestRunning, IsManualTestChecked, GetFullStrategyArguments(item), ConfirmReplacementAsync,
UpdateSelectedStrategy, UpdateAssignmentView, Text, ShowMessage, Warn(Exception), Enqueue(Func<Task>).
Приватный вложенный ManualTestHost только проксирует поля/методы Page; публичный класс не реализует
новый интерфейс. Constructor host не обращается к view/manager/ID, в том числе при null view.

ToggleFromButton: nullable checked==true вызывает Start, остальные значения Stop(true). Toggle
выбирает по _isRunning. Start захватывает item, затем guards null/running/strategy по старому short
circuit. UpdateSelection предшествует GetFullArguments текущего Result с captured item. Guard
!Success/IsNullOrEmpty сохраняет Warning/ManualTesting и ErrorCode либо EmptyArguments. Whitespace
аргументы не нормализуются. Stop(false) вызывается до IsTaskRunning(id); confirm только если running,
отказ возвращает без нового Stop. Restore snapshot=componentWasRunning; Stop(id), flags true/true,
subscribe host handler, UpdateSelection и Started notice остаются ДО try. Try охватывает только
Run(id, captured result.Result) и сброс starting. Catch сбрасывает starting, показывает Error с
string.Format(Text(StartFailed),exception.Message), затем Stop(false). Ошибки notice/format/cleanup
выходят из прежних границ. Pending/конкурентные start/stop не сериализуются и не подавляются заново.

Stop всегда начинает с UpdateSelection, даже когда уже stopped. Если running: capture restore,
flags running/starting=false, unsubscribe, whitespace guard с первым чтением ID, затем Stop(вновьID),
Restore(вновьID) при captured restore. После этого restore=false, optional Stopped notice, selection,
assignments. Все ID чтения живые, исключения до final updates сохраняют частичное состояние.
Leave при stopped ничего не делает; при running capture restore и сразу сбрасывает ВСЕ три flags,
unsubscribe, один раз читает ID вне try, whitespace возвращает. Только Stop/Restore внутри try,
при ошибке Warn(exception). Ни notice, ни selection/assignments тут не добавляются. Logger host
сохраняет LogSource/строку/receiver-before-exception-formatting и прежний uncaught warning fault.

StateUpdated short-circuit: running, !starting, OrdinalIgnoreCase currentID, state.Item2==false.
При совпадении очередь принимает async callback: Exited warning, затем Stop(false). Guard только
при постановке, не при доставке; нет dedup/captured component ID. Host Enqueue адаптирует Func<Task>
в прежний dispatcher async-void callback с игнорируемым TryEnqueue результатом. Public/private
forwards возвращают owner Task без раннего вызова пользовательских портов; Task fault timing сохраняется.

Проверки: absent-owner RED, literal traces/guards/partial failures/pending/concurrent/reentry/live IDs/
restore vs leave difference/captured item/current arguments/real canceled tasks/subscription identity/
queued callback faults and delivery-time state. TCS ожидания ограничены и освобождаются finally.
Full managed suite, GUIx64 Release, source451/compiled452 shapes без потерь, прежние+7 additions.
Новый bounded actual121=115старых+6manual, realPage/Result/session/localizer/privateforwards/dispatcher,
контролируемые tasks/confirm/status/selection и logger. Один новый boundary файл ManualTesting.cs
заменяет только resolveTasks/currentID и logger; всего19cuts,24bootstrap sources. Один старый
selection helper меняется только на bridge к новому state owner, без ослабления старых asserts.
Новых IO нет: старые3inputs/log/2reportoutputs=6newbinfiles, все прежние source/binary/input/output/
absence/process checks. Independent prelaunch READY, явный UI handoff, exact security joint и finalreview.
Это composition/lifecycle proof с inert component tasks, не настоящий запуск winws/restore/OS isolation.

Никаких удалений/reset/rebase/push/merge/publication; чужие worktrees и прежние evidence сохраняются.
