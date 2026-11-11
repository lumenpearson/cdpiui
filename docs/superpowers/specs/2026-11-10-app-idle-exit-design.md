# App: реакция на Store и выход при простое

Продолжение согласованной декомпозиции ConfigMaker → App → BlockCheck2 после checkpoint2400dee.
Пользователь разрешил самостоятельные решения, проверки и локальные обратимые изменения без срока.
Аудит остаётся входными данными. Отдельная security-сессия сохраняет свою область и рабочее дерево.

Выделить `internal sealed AppIdleExitController` в `Helper/Basic`. Он отвечает за две связанные политики:
реакцию на StoreStateChanged и проверку возможности Exit. Состояния у него нет; восемь ленивых портов
читают число окон, migration pending, Store и GoodCheck, создают/закрывают PrepareWindow, вызывают Exit
и записывают предупреждение. Конструктор только сохраняет делегаты. Новых валидаций, кэшей, блокировок,
ожидания Task, rollback, reentry guards и политики потока не добавлять.

Существующие private `App.StoreStateChanged(bool)` и `App.ExitIfIdle()` остаются делегирующими методами.
Store подписывается на прежний метод App, WindowCloseOperations сохраняет App-bound callback ExitIfIdle.
Count читается через `() => OpenWindows.Count`, остальные статические состояния также через lambda.
Prepare создаётся с `activate:false`; Task отбрасывается. Logger получает исходный Exception через порт,
а прежняя категория `nameof(App)` и текст `Cannot update background operation state: {ex.Message}`
формируются в App. Остальные activation/migration/startup/pipe/close/modal/settings правила не меняются.

Сохранить короткое замыкание и повторные чтения. Pending migration с нулём окон не завершает App;
при ненулевом числе окон следующая проверка Count читает состояние заново. Остановленный Store сначала
закрывает Prepare, затем читает актуальные условия выхода. Исключение из тела Store вызывает Warn,
затем повторный ExitIfIdle; ошибка Warn прерывает fallback, ошибка fallback выходит наружу. Прямой
ExitIfIdle исключения не перехватывает. Синхронные вложенные события выполняются без ограничения.

Доказательства: linked-source managed characterization порядка, актуальных чтений, ошибок, reentry
и необжидаемого Task; полный suite; обычная GUI x64 Release; source/compiled recorded API. Затем новый
actual-host стенд с настоящими App/owner и инертными конечными действиями, отдельным source manifest,
prelaunch review и postchecks. Нативный ввод допускается только после явной передачи общей UI-очереди.
Сейчас очередь у другого чата; код и CLI-проверки независимы от неё. Проверки не являются доказательством
отсутствия всех регрессий или полной OS isolation. Все старые неуспешные стенды сохраняются.
